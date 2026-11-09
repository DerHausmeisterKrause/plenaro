using System.Collections.Concurrent;
using Npgsql;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Two separate visibility rules:
/// 1. Explicit personal assignment/segment participation can always display
///    locally cached ticket metadata (owner, responsible or planned/tagged).
/// 2. Unassigned candidate tickets are visible only in queues that a user's
///    personal Znuny session can actually read. Never infer queue rights from
///    the global sync account or from "show all new" preferences.
/// </summary>
public sealed class ZnunyTicketVisibility(
    IConfiguration config, SecretVault vault, ZnunyReader reader)
{
    private sealed record QueueSnapshot(DateTimeOffset ValidUntil,
        IReadOnlySet<string> Queues);
    private readonly ConcurrentDictionary<Guid,QueueSnapshot> _queues=new();
    private readonly ConcurrentDictionary<Guid,SemaphoreSlim> _locks=new();
    private static readonly TimeSpan Lifetime=TimeSpan.FromSeconds(60);

    // Old tests use this exact-intersection helper.
    public static string[] Intersect(IEnumerable<string> candidates,
        IReadOnlySet<string> permitted)=>
        candidates.Where(permitted.Contains).Distinct(StringComparer.Ordinal).ToArray();

    public void Invalidate(Guid user)=>_queues.TryRemove(user,out _);
    public void InvalidateAll()=>_queues.Clear();

    /// <summary>Local assignment is independent of Znuny API connectivity.
    /// Participant emails must match the current local account exactly;
    /// arbitrary search terms/partial names cannot grant access.</summary>
    public async Task<IReadOnlySet<string>> PersonalTicketsAsync(Guid user,
        CancellationToken ct)
    {
        await using var db=await OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT DISTINCT t.ticket_id
            FROM znuny_cached_tickets t
            JOIN znuny_approved_agent_bindings b ON b.user_id=@user
            JOIN "AspNetUsers" account ON account."Id"=b.user_id
            WHERE t.owner_agent_id=b.agent_id
               OR t.responsible_agent_id=b.agent_id
               -- A previously handled/closed ticket is still relevant to
               -- this user even after owner/responsible has been reassigned.
               OR EXISTS(SELECT 1 FROM znuny_ticket_time_entries e
                   WHERE e.user_id=@user AND e.ticket_id=t.ticket_id)
               OR EXISTS(SELECT 1 FROM znuny_ticket_time_bookings bkg
                   WHERE bkg.user_id=@user AND bkg.ticket_id=t.ticket_id)
               OR EXISTS(SELECT 1 FROM znuny_ticket_time_adjustments adj
                   WHERE adj.user_id=@user AND adj.ticket_id=t.ticket_id)
               OR EXISTS(SELECT 1 FROM znuny_ticket_time_corrections corr
                   WHERE corr.user_id=@user AND corr.ticket_id=t.ticket_id)
               OR EXISTS(SELECT 1 FROM znuny_ticket_workspaces w
                   WHERE w.user_id=@user AND w.ticket_id=t.ticket_id AND
                       (w.description<>'' OR w.ticket_url<>''))
               OR EXISTS (
                   SELECT 1 FROM znuny_ticket_segments s
                   WHERE s.ticket_id=t.ticket_id AND
                     (s.user_id=@user OR
                      (account."NormalizedEmail" IS NOT NULL AND EXISTS (
                          SELECT 1 FROM unnest(s.participants) AS participant(email)
                          WHERE upper(trim(participant.email))=
                              account."NormalizedEmail"
                      )))
               )
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        var ids=new HashSet<string>(StringComparer.Ordinal);
        await using var rows=await cmd.ExecuteReaderAsync(ct);
        while(await rows.ReadAsync(ct))ids.Add(rows.GetString(0));
        return ids;
    }

    /// <summary>
    /// Znuny grants ro/rw via queue groups. Test an unassigned ticket in each
    /// candidate queue with the PERSONAL agent's TicketGet; a successful read
    /// is the positive queue proof. Without credentials/working connector,
    /// the unassigned queue list stays empty, NOT HTTP 503 or all queues.
    /// </summary>
    public async Task<IReadOnlySet<string>> NewQueuesAsync(Guid user,CancellationToken ct)
    {
        if(_queues.TryGetValue(user,out var existing)&&
           existing.ValidUntil>DateTimeOffset.UtcNow)return existing.Queues;
        var gate=_locks.GetOrAdd(user,_=>new SemaphoreSlim(1,1));
        await gate.WaitAsync(ct);
        try
        {
            if(_queues.TryGetValue(user,out existing)&&
               existing.ValidUntil>DateTimeOffset.UtcNow)return existing.Queues;
            Invalidate(user);
            var result=new HashSet<string>(StringComparer.Ordinal);
            await using var db=await OpenAsync(ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT DISTINCT ON (t.queue) t.queue,t.ticket_id,
                    COALESCE(p.username,'')
                FROM znuny_cached_tickets t
                JOIN znuny_approved_agent_bindings b ON b.user_id=@user
                JOIN znuny_user_settings p ON p.user_id=b.user_id
                WHERE p.candidate_user_id IS NOT NULL
                  AND t.queue<>'' AND lower(t.state_type)<>'closed'
                  AND (t.owner_agent_id IS NULL OR
                      t.owner_agent_id IN (0,p.candidate_user_id))
                  AND (t.responsible_agent_id IS NULL OR
                      t.responsible_agent_id IN (0,p.candidate_user_id))
                ORDER BY t.queue,t.ticket_id DESC
                LIMIT 100
                """,db);
            cmd.Parameters.AddWithValue("user",user);
            var samples=new List<(string Queue,string TicketId)>();
            var username="";
            await using(var rows=await cmd.ExecuteReaderAsync(ct)){
                while(await rows.ReadAsync(ct)){
                    username=rows.GetString(2);
                    samples.Add((rows.GetString(0),rows.GetString(1)));
                }
            }

            if(samples.Count>0 && !string.IsNullOrWhiteSpace(username) &&
                vault.IsConfigured)
            {
                try
                {
                    var password=await vault.GetForIntegrationAsync("user",user,
                        "znuny.password",ct);
                    if(!string.IsNullOrEmpty(password)){
                        var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
                        if(ZnunyEndpoints.ValidateServer(server) is null &&
                            !new Uri(server.ApiUrl).Host.Equals("SERVER",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            using var timeout=CancellationTokenSource
                                .CreateLinkedTokenSource(ct);
                            timeout.CancelAfter(TimeSpan.FromSeconds(35));
                            var counter=new ZnunyReader.Counter(1+samples.Count);
                            var session=await reader.SessionAsync(server,username,
                                password,counter,timeout.Token);
                            foreach(var item in samples){
                                if(timeout.IsCancellationRequested)break;
                                try{
                                    var ticket=await reader.GetAsync(server,session,
                                        item.TicketId,counter,timeout.Token);
                                    if(ticket.Id==item.TicketId &&
                                       ticket.Queue.Equals(item.Queue,
                                           StringComparison.Ordinal))
                                        result.Add(item.Queue);
                                }
                                catch(ZnunyReader.RemoteFailure){
                                    // Lack of rights, unsupported TicketGet or
                                    // transient errors never grant a queue.
                                }
                            }
                        }
                    }
                }
                catch(ZnunyReader.RemoteFailure){
                    // Personal API login failed. Do not reveal any candidate.
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested){
                    // Timeout is not an authorization success.
                }
                catch(InvalidOperationException){
                    // Missing/broken configuration is not an authorization success.
                }
            }
            _queues[user]=new(DateTimeOffset.UtcNow.Add(Lifetime),result);
            return result;
        }
        finally{gate.Release();}
    }

    /// <summary>Direct routes: either personally assigned/planned OR a
    /// still-unassigned ticket in a queue confirmed by the personal agent.
    /// A client-provided ticket ID never establishes either privilege.</summary>
    public async Task<bool> CanReadTicketAsync(Guid user,string ticketId,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(ticketId)||ticketId.Length>80||
            !ticketId.All(char.IsAsciiDigit))return false;
        if((await PersonalTicketsAsync(user,ct)).Contains(ticketId))return true;
        var queues=await NewQueuesAsync(user,ct);
        if(queues.Count==0)return false;
        await using var db=await OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1 FROM znuny_cached_tickets t
                JOIN znuny_approved_agent_bindings b ON b.user_id=@user
                JOIN znuny_user_settings p ON p.user_id=b.user_id
                WHERE t.ticket_id=@ticket
                  AND t.queue=ANY(@queues)
                  AND lower(t.state_type)<>'closed'
                  AND p.candidate_user_id IS NOT NULL
                  AND (t.owner_agent_id IS NULL OR
                       t.owner_agent_id IN (0,p.candidate_user_id))
                  AND (t.responsible_agent_id IS NULL OR
                       t.responsible_agent_id IN (0,p.candidate_user_id))
            )
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.AddWithValue("ticket",ticketId);
        cmd.Parameters.Add("queues",NpgsqlTypes.NpgsqlDbType.Array |
            NpgsqlTypes.NpgsqlDbType.Varchar).Value=queues.ToArray();
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }
}
