using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// The shared synchronizer uses a technical service account and therefore
/// cannot authorize a browser user. A personal Znuny TicketSearch session
/// supplies the positive ticket allowlist; unverified IDs never leave the API.
///
/// Cache lifetime is short, never falls back to the global sync account,
/// and every direct ticket route verifies TicketGet again with the agent's
/// personal session. Inaccessible or unsupported remote searches fail closed.
/// </summary>
public sealed class ZnunyTicketVisibility(
    IConfiguration config,SecretVault vault,ZnunyReader reader)
{
    private sealed record Snapshot(
        DateTimeOffset ValidUntil,
        IReadOnlySet<string> Ids,
        string Session,
        ZnunyEndpoints.ServerSettings Server);
    private readonly ConcurrentDictionary<Guid,Snapshot> _snapshots=new();
    private readonly ConcurrentDictionary<Guid,SemaphoreSlim> _locks=new();
    private static readonly TimeSpan Lifetime=TimeSpan.FromSeconds(60);

    public sealed class UnavailableException() : Exception(
        "Persönliche Znuny-Queue-Berechtigungen konnten nicht bestätigt werden.");

    public void Invalidate(Guid user)=>_snapshots.TryRemove(user,out _);
    public void InvalidateAll()=>_snapshots.Clear();

    // Exact intersection, not a client-side hide or owner-only heuristic.
    public static string[] Intersect(IEnumerable<string> candidates,
        IReadOnlySet<string> permitted)=>
        candidates.Where(permitted.Contains).Distinct(StringComparer.Ordinal).ToArray();

    public async Task<IReadOnlySet<string>> VisibleAsync(Guid user,CancellationToken ct)=>
        (await SnapshotAsync(user,ct)).Ids;

    public async Task<bool> CanReadTicketAsync(Guid user,string ticketId,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(ticketId)||ticketId.Length>80||
            !ticketId.All(char.IsAsciiDigit))return false;
        var snapshot=await SnapshotAsync(user,ct);
        if(!snapshot.Ids.Contains(ticketId))return false;
        // The list has a 60s refresh cadence, but details, notes and writes
        // MUST recheck the live Znuny permission even if a queue was moved
        // or the user's role has just been revoked.
        try
        {
            var ticket=await reader.GetAsync(snapshot.Server,snapshot.Session,
                ticketId,new ZnunyReader.Counter(2),ct);
            return ticket.Id==ticketId;
        }
        catch(ZnunyReader.RemoteFailure e) when(
            e.Code is "authentication" or "remote-rejected" or "missing-ticket" or
                "route-not-found" or "ticket-id-mismatch" or "server-error")
        {
            // Any failed live check denies access; we must not use the
            // central service account as an authorization fallback.
            Invalidate(user);
            return false;
        }
        catch(ZnunyReader.RemoteFailure)
        {
            Invalidate(user);
            throw new UnavailableException();
        }
    }

    private async Task<Snapshot> SnapshotAsync(Guid user,CancellationToken ct)
    {
        if(_snapshots.TryGetValue(user,out var existing)&&
            existing.ValidUntil>DateTimeOffset.UtcNow)return existing;
        var gate=_locks.GetOrAdd(user,_=>new SemaphoreSlim(1,1));
        await gate.WaitAsync(ct);
        try
        {
            if(_snapshots.TryGetValue(user,out existing)&&
                existing.ValidUntil>DateTimeOffset.UtcNow)return existing;
            // Remove expired positive privileges BEFORE asking the remote
            // system; a timeout must never resurrect the prior snapshot.
            Invalidate(user);
            await using var db=new NpgsqlConnection(
                config.GetConnectionString("Plenaro"));
            await db.OpenAsync(ct);

            string username;
            DateTimeOffset? minCreated=null,maxCreated=null;
            await using(var cmd=new NpgsqlCommand("""
                SELECT COALESCE(p.username,''),
                    min(COALESCE(t.created_at,t.changed_at)),
                    max(COALESCE(t.created_at,t.changed_at)),
                    count(t.ticket_id),
                    count(*) FILTER(WHERE t.ticket_id IS NOT NULL
                        AND t.created_at IS NULL)
                FROM znuny_approved_agent_bindings b
                LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
                LEFT JOIN znuny_cached_tickets t ON
                    t.owner_agent_id=b.agent_id OR
                    t.responsible_agent_id=b.agent_id OR
                    (p.candidate_user_id IS NOT NULL AND
                        (t.owner_agent_id IS NULL OR
                         t.owner_agent_id IN(0,p.candidate_user_id)) AND
                        (t.responsible_agent_id IS NULL OR
                         t.responsible_agent_id IN(0,p.candidate_user_id)))
                WHERE b.user_id=@user
                GROUP BY p.username
                """,db)){
                cmd.Parameters.AddWithValue("user",user);
                await using var rows=await cmd.ExecuteReaderAsync(ct);
                if(!await rows.ReadAsync(ct))
                    return new Snapshot(DateTimeOffset.UtcNow.Add(Lifetime),
                        new HashSet<string>(StringComparer.Ordinal),"",
                        new ZnunyEndpoints.ServerSettings());
                username=rows.GetString(0);
                var count=rows.GetInt64(3);
                if(count==0)
                    return new Snapshot(DateTimeOffset.UtcNow.Add(Lifetime),
                        new HashSet<string>(StringComparer.Ordinal),"",
                        new ZnunyEndpoints.ServerSettings());
                if(!rows.IsDBNull(1))
                    minCreated=new DateTimeOffset(rows.GetDateTime(1),TimeSpan.Zero);
                if(!rows.IsDBNull(2))
                    maxCreated=new DateTimeOffset(rows.GetDateTime(2),TimeSpan.Zero);
                if(rows.GetInt64(4)>0)minCreated=new DateTimeOffset(
                    2000,1,1,0,0,0,TimeSpan.Zero);
            }
            if(string.IsNullOrWhiteSpace(username) || minCreated is null ||
                maxCreated is null || !vault.IsConfigured)
                throw new UnavailableException();
            var password=await vault.GetForIntegrationAsync("user",user,
                "znuny.password",ct);
            if(string.IsNullOrEmpty(password))throw new UnavailableException();
            var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
            if(ZnunyEndpoints.ValidateServer(server) is not null||
                new Uri(server.ApiUrl).Host.Equals("SERVER",
                    StringComparison.OrdinalIgnoreCase))
                throw new UnavailableException();

            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            var token=timeout.Token;
            // Every returned ID is authorized by Znuny under the *personal*
            // agent session, not the technical synchronization credentials.
            var calls=new ZnunyReader.Counter(180);
            var session=await reader.SessionAsync(server,username,password,calls,token);
            var after=minCreated.Value.AddDays(-1);
            var until=maxCreated.Value.AddDays(1);
            if(until<=after)until=after.AddDays(2);
            var authorized=await reader.SearchWindowAsync(server,session,
                "created",after,until,calls,token);
            var ids=new HashSet<string>(authorized,StringComparer.Ordinal);
            var snapshot=new Snapshot(DateTimeOffset.UtcNow.Add(Lifetime),
                ids,session,server);
            _snapshots[user]=snapshot;
            return snapshot;
        }
        catch(ZnunyReader.RemoteFailure)
        {
            throw new UnavailableException();
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {
            throw new UnavailableException();
        }
        catch(InvalidOperationException)
        {
            throw new UnavailableException();
        }
        finally {gate.Release();}
    }
}
