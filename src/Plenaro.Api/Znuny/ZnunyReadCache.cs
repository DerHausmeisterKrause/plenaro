using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.RateLimiting;
using Npgsql;
using NpgsqlTypes;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

namespace Plenaro.Api.Znuny;

// Never call this service before the endpoint's fresh user/ticket authorization.
// Personal-account responses are not shared or persisted.
public sealed class ZnunyReadCache(IConfiguration config,ZnunyReader reader,
    ILogger<ZnunyReadCache> logger):IDisposable
{
    private readonly SemaphoreSlim[] stripes=Enumerable.Range(0,128)
        .Select(_=>new SemaphoreSlim(1,1)).ToArray();
    private readonly SemaphoreSlim remote=new(6,6);
    private readonly PartitionedRateLimiter<Guid> misses=PartitionedRateLimiter.Create<Guid,Guid>(
        user=>RateLimitPartition.GetFixedWindowLimiter(user,_=>new FixedWindowRateLimiterOptions{
            PermitLimit=120,Window=TimeSpan.FromMinutes(5),QueueLimit=0,AutoReplenishment=true
        }));
    public sealed record Snapshot(ZnunyReader.Detail Detail,DateTimeOffset FetchedAt,bool Stale);
    public sealed record Credentials(string Username,string Password,bool Central);
    private sealed record TicketRow(ZnunyReader.Detail? Detail,DateTimeOffset Fetched,
        DateTimeOffset? Changed,DateTimeOffset? Current,bool Scoped,bool Enabled);

    public static string Key(ServerSettings server,string username)=>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{server,username})));
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        try{await db.OpenAsync(ct);return db;}catch{await db.DisposeAsync();throw;}
    }

    private async Task<T> RemoteAsync<T>(Guid user,Func<Task<T>> fetch,CancellationToken ct)
    {
        using var lease=misses.AttemptAcquire(user);
        if(!lease.IsAcquired)throw new ZnunyReader.RemoteFailure("cache-miss-limit");
        if(!await remote.WaitAsync(TimeSpan.FromSeconds(10),ct))
            throw new ZnunyReader.RemoteFailure("cache-fetch-busy");
        try{return await fetch();}finally{remote.Release();}
    }

    private SemaphoreSlim Stripe(string key)=>stripes[(int)((uint)StringComparer.Ordinal.GetHashCode(key)% (uint)stripes.Length)];
    private static async Task<T> LockedAsync<T>(SemaphoreSlim gate,Func<Task<T>> action,CancellationToken ct)
    {
        if(!await gate.WaitAsync(TimeSpan.FromSeconds(20),ct))
            throw new ZnunyReader.RemoteFailure("cache-fetch-busy");
        try{return await action();}finally{gate.Release();}
    }

    private static async Task<TicketRow> ReadTicketAsync(NpgsqlConnection db,ServerSettings server,
        string username,string ticket,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT t.changed_at,d.payload::text,d.fetched_at,d.changed_at,
                c.source_api_url=@source AND c.service_username=@username,c.enabled
            FROM znuny_cached_tickets t CROSS JOIN znuny_sync_control c
            LEFT JOIN znuny_ticket_detail_cache d ON d.ticket_id=t.ticket_id AND d.config_key=@key
            WHERE t.ticket_id=@ticket
            """,db);
        cmd.Parameters.AddWithValue("source",server.ApiUrl);
        cmd.Parameters.AddWithValue("username",username);
        cmd.Parameters.AddWithValue("key",Key(server,username));
        cmd.Parameters.AddWithValue("ticket",ticket);
        await using var row=await cmd.ExecuteReaderAsync(ct);
        if(!await row.ReadAsync(ct))return new(null,DateTimeOffset.MinValue,null,null,false,false);
        DateTimeOffset? Date(int i)=>row.IsDBNull(i)?null:new DateTimeOffset(row.GetDateTime(i),TimeSpan.Zero);
        var scoped=!row.IsDBNull(4)&&row.GetBoolean(4);
        return new(scoped&&!row.IsDBNull(1)?JsonSerializer.Deserialize<ZnunyReader.Detail>(row.GetString(1)):null,
            Date(2)??DateTimeOffset.MinValue,Date(3),Date(0),scoped,row.GetBoolean(5));
    }

    private static async Task RequestAsync(NpgsqlConnection db,string ticket,string key,CancellationToken ct)
    {
        // Schedule only an aged, recently viewed snapshot, once until processed.
        await using var cmd=new NpgsqlCommand("""
            UPDATE znuny_ticket_detail_cache SET requested_at=now()
            WHERE ticket_id=@ticket AND config_key=@key AND payload IS NOT NULL
              AND fetched_at<now()-interval '1 minute'
              AND (requested_at IS NULL OR requested_at<=fetched_at)
            """,db);
        cmd.Parameters.AddWithValue("ticket",ticket);cmd.Parameters.AddWithValue("key",key);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static async Task SaveTicketAsync(NpgsqlConnection db,ServerSettings server,
        string username,string ticket,DateTimeOffset? changed,DateTimeOffset started,
        ZnunyReader.Detail detail,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            INSERT INTO znuny_ticket_detail_cache(ticket_id,config_key,changed_at,fetched_at,payload)
            SELECT t.ticket_id,@key,@changed,@started,@payload
            FROM znuny_cached_tickets t CROSS JOIN znuny_sync_control c
            WHERE t.ticket_id=@ticket AND c.source_api_url=@source AND c.service_username=@username
            ON CONFLICT(ticket_id) DO UPDATE SET config_key=EXCLUDED.config_key,
                changed_at=EXCLUDED.changed_at,fetched_at=EXCLUDED.fetched_at,payload=EXCLUDED.payload,
                requested_at=NULL,retry_after=NULL
            WHERE znuny_ticket_detail_cache.fetched_at<=EXCLUDED.fetched_at
            """,db);
        cmd.Parameters.AddWithValue("ticket",ticket);cmd.Parameters.AddWithValue("key",Key(server,username));
        cmd.Parameters.AddWithValue("source",server.ApiUrl);cmd.Parameters.AddWithValue("username",username);
        cmd.Parameters.Add("changed",NpgsqlDbType.TimestampTz).Value=(object?)changed?.UtcDateTime??DBNull.Value;
        cmd.Parameters.Add("started",NpgsqlDbType.TimestampTz).Value=started.UtcDateTime;
        cmd.Parameters.Add("payload",NpgsqlDbType.Jsonb).Value=JsonSerializer.Serialize(detail);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Snapshot> TicketAsync(ServerSettings server,Credentials cred,Guid user,
        string ticket,bool refresh,CancellationToken ct)
    {
        async Task<Snapshot> FetchAsync()=>await RemoteAsync(user,async()=>{
            var started=DateTimeOffset.UtcNow;
            var calls=new ZnunyReader.Counter(4);
            var session=await reader.SessionAsync(server,cred.Username,cred.Password,calls,ct);
            return new Snapshot(await reader.GetArticlesAsync(server,session,ticket,calls,ct),started,false);
        },ct);
        if(!cred.Central)return await FetchAsync();
        var key=Key(server,cred.Username);
        await using var db=await OpenAsync(ct);
        var row=await ReadTicketAsync(db,server,cred.Username,ticket,ct);
        if(!row.Scoped)return await FetchAsync();
        if(!refresh&&row.Detail is not null&&row.Changed==row.Current&&
            (row.Enabled||DateTimeOffset.UtcNow-row.Fetched<TimeSpan.FromMinutes(1))){
            await RequestAsync(db,ticket,key,ct);
            return new(row.Detail,row.Fetched,DateTimeOffset.UtcNow-row.Fetched>TimeSpan.FromMinutes(2));
        }
        return await LockedAsync(Stripe("ticket:"+ticket),async()=>{
            row=await ReadTicketAsync(db,server,cred.Username,ticket,ct);
            if(!refresh&&row.Detail is not null&&row.Changed==row.Current&&
            (row.Enabled||DateTimeOffset.UtcNow-row.Fetched<TimeSpan.FromMinutes(1))){
                await RequestAsync(db,ticket,key,ct);
                return new Snapshot(row.Detail,row.Fetched,DateTimeOffset.UtcNow-row.Fetched>TimeSpan.FromMinutes(2));
            }
            try{
                var result=await FetchAsync();
                await SaveTicketAsync(db,server,cred.Username,ticket,row.Current,result.FetchedAt,result.Detail,ct);
                return result;
            }catch(ZnunyReader.RemoteFailure e) when(!refresh&&row.Detail is not null&&
                e.Code is "timeout" or "network-error" or "server-error" or "rate-limited" or "cache-miss-limit" or "cache-fetch-busy"){
                await RequestAsync(db,ticket,key,ct);
                return new Snapshot(row.Detail,row.Fetched,true);
            }
        },ct);
    }

    public static async Task<T?> ReadSharedAsync<T>(NpgsqlConnection db,ServerSettings server,
        string username,string kind,CancellationToken ct) where T:class
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT d.payload::text FROM znuny_shared_read_cache d CROSS JOIN znuny_sync_control c
            WHERE d.kind=@kind AND d.config_key=@key AND d.fetched_at>now()-interval '5 minutes'
              AND c.source_api_url=@source AND c.service_username=@username
            """,db);
        cmd.Parameters.AddWithValue("kind",kind);cmd.Parameters.AddWithValue("key",Key(server,username));
        cmd.Parameters.AddWithValue("source",server.ApiUrl);cmd.Parameters.AddWithValue("username",username);
        return await cmd.ExecuteScalarAsync(ct) is string json?JsonSerializer.Deserialize<T>(json):null;
    }

    public static async Task SaveSharedAsync<T>(NpgsqlConnection db,ServerSettings server,
        string username,string kind,T value,DateTimeOffset started,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            INSERT INTO znuny_shared_read_cache(kind,config_key,fetched_at,payload)
            SELECT @kind,@key,@started,@payload FROM znuny_sync_control c
            WHERE c.source_api_url=@source AND c.service_username=@username
            ON CONFLICT(kind) DO UPDATE SET config_key=EXCLUDED.config_key,
                fetched_at=EXCLUDED.fetched_at,payload=EXCLUDED.payload
            WHERE znuny_shared_read_cache.fetched_at<=EXCLUDED.fetched_at
            """,db);
        cmd.Parameters.AddWithValue("kind",kind);cmd.Parameters.AddWithValue("key",Key(server,username));
        cmd.Parameters.AddWithValue("source",server.ApiUrl);cmd.Parameters.AddWithValue("username",username);
        cmd.Parameters.Add("started",NpgsqlDbType.TimestampTz).Value=started.UtcDateTime;
        cmd.Parameters.Add("payload",NpgsqlDbType.Jsonb).Value=JsonSerializer.Serialize(value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<T> SharedAsync<T>(ServerSettings server,Credentials cred,Guid user,string kind,
        Func<string,ZnunyReader.Counter,Task<T>> load,CancellationToken ct,bool refresh=false) where T:class
    {
        Task<T> Fetch()=>RemoteAsync(user,async()=>{
            var calls=new ZnunyReader.Counter(4);
            var session=await reader.SessionAsync(server,cred.Username,cred.Password,calls,ct);
            return await load(session,calls);
        },ct);
        if(!cred.Central)return await Fetch();
        await using var db=await OpenAsync(ct);
        var found=await ReadSharedAsync<T>(db,server,cred.Username,kind,ct);
        if(!refresh&&found is not null)return found;
        return await LockedAsync(Stripe("shared:"+kind),async()=>{
            var existing=await ReadSharedAsync<T>(db,server,cred.Username,kind,ct);
            if(!refresh&&existing is not null)return existing;
            var started=DateTimeOffset.UtcNow;
            var result=await Fetch();
            await SaveSharedAsync(db,server,cred.Username,kind,result,started,ct);
            return result;
        },ct);
    }

    public Task<ZnunyReader.Agent[]> AgentsAsync(ServerSettings server,Credentials cred,Guid user,CancellationToken ct,bool refresh=false)=>
        SharedAsync(server,cred,user,"agents",async(session,calls)=>
            (await reader.GetAgentsAsync(server,session,calls,ct)).ToArray(),ct,refresh);

    public async Task<ZnunyReader.BookingFields> BookingAsync(ServerSettings server,Credentials cred,
        Guid user,string ticket,CancellationToken ct)
    {
        Task<ZnunyReader.BookingFields> Original()=>RemoteAsync(user,async()=>{
            var calls=new ZnunyReader.Counter(6);
            var session=await reader.SessionAsync(server,cred.Username,cred.Password,calls,ct);
            return await reader.GetBookingFieldsAsync(server,session,ticket,calls,ct);
        },ct);
        if(!cred.Central)return await Original();
        Snapshot snapshot;
        try{snapshot=await TicketAsync(server,cred,user,ticket,false,ct);}
        catch(ZnunyReader.RemoteFailure e) when(e.Code is not ("cache-miss-limit" or "cache-fetch-busy")){
            // Very large/unsupported article responses must not disable time booking.
            return await Original();
        }
        var options=await SharedAsync(server,cred,user,"options",
            (session,calls)=>reader.GetBookingOptionsAsync(server,session,calls,ct),ct);
        return new(snapshot.Detail.CostCenter,snapshot.Detail.Order,options.CostCenterOptions,options.OrderOptions);
    }

    // Best effort after confirmed writes: never convert a confirmed upstream
    // write into a failed response or automatically repeat it.
    public async Task InvalidateAsync(string ticket)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try{
            await using var db=await OpenAsync(timeout.Token);
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO znuny_ticket_detail_cache(ticket_id,config_key,fetched_at)
                SELECT ticket_id,'',clock_timestamp() FROM znuny_cached_tickets WHERE ticket_id=@ticket
                ON CONFLICT(ticket_id) DO UPDATE SET payload=NULL,fetched_at=clock_timestamp(),
                    requested_at=NULL,retry_after=NULL
                """,db);
            cmd.Parameters.AddWithValue("ticket",ticket);
            await cmd.ExecuteNonQueryAsync(timeout.Token);
        }catch(Exception e){logger.LogWarning("Znuny cache invalidation failed ({Type})",e.GetType().Name);}
    }

    public void Dispose(){misses.Dispose();remote.Dispose();foreach(var gate in stripes)gate.Dispose();}
}
