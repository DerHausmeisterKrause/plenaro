using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Secrets;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

namespace Plenaro.Api.Znuny;

/// <summary>
/// One scheduler per deployment; database advisory lock prevents duplicate work
/// across replicas. A *single* shared service credential imports the ticket
/// metadata into a central cache. No per-user remote polling.
/// </summary>
public sealed class ZnunyCentralSync(
    IConfiguration config, SecretVault vault, ZnunyReader reader,
    ILogger<ZnunyCentralSync> logger) : BackgroundService
{
    public const string ServiceSecret = "znuny.sync.password";
    private const long LockKey = 713220261031L;
    private readonly SemaphoreSlim _localGate = new(1,1);

    public sealed record Status(bool Enabled,string ServiceUsername,bool PasswordConfigured,
        string? SourceApiUrl,int InitialLookbackDays,int DeltaIntervalMinutes,
        int OpenIntervalMinutes,int MaxRequestsPerCycle,DateTimeOffset? BootstrapCursor,
        DateTimeOffset? BootstrapEnd,DateTimeOffset? BootstrapFinishedAt,
        DateTimeOffset? DeltaWatermark,DateTimeOffset? LastDeltaAt,
        DateTimeOffset? LastOpenAt,DateTimeOffset? OpenCursor,
        DateTimeOffset? LastAttemptAt,string? LastError,long CachedTickets);
    public sealed record RunResult(bool Started,string Status,int Requests,int Tickets);
    private sealed record ConfigRow(
        bool Enabled,string Username,string? SourceUrl,
        int Lookback,int DeltaMinutes,int OpenMinutes,int Budget,
        DateTimeOffset? Cursor,DateTimeOffset? BootstrapEnd,
        DateTimeOffset? Finished,DateTimeOffset? Watermark,
        DateTimeOffset? LastDelta,DateTimeOffset? LastOpen,
        DateTimeOffset? OpenCursor);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var periodic=new PeriodicTimer(TimeSpan.FromSeconds(45));
        try
        {
            while(await periodic.WaitForNextTickAsync(ct))
            {
                try { await RunAsync(false,ct); }
                catch (OperationCanceledException) when(ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning("Znuny central sync cycle stopped: {Reason}",ex.GetType().Name);
                }
            }
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested) { }
    }

    public async Task<RunResult> RunAsync(bool force,CancellationToken ct)
    {
        if(!await _localGate.WaitAsync(0,ct))
            return new(false,"already-running",0,0);
        try
        {
            await using var db=await OpenAsync(ct);
            await using var lockCommand=new NpgsqlCommand(
                "SELECT pg_try_advisory_lock(@key)",db);
            lockCommand.Parameters.AddWithValue("key",LockKey);
            if(await lockCommand.ExecuteScalarAsync(ct) is not true)
                return new(false,"already-running",0,0);
            try {return await RunLockedAsync(db,force,ct);}
            finally
            {
                await using var unlock=new NpgsqlCommand(
                    "SELECT pg_advisory_unlock(@key)",db);
                unlock.Parameters.AddWithValue("key",LockKey);
                await unlock.ExecuteScalarAsync(CancellationToken.None);
            }
        }
        finally{_localGate.Release();}
    }

    private async Task<RunResult> RunLockedAsync(NpgsqlConnection db,bool force,CancellationToken ct)
    {
        var state=await LoadAsync(db,ct);
        if(!state.Enabled) return new(false,"disabled",0,0);
        var tickNow=DateTimeOffset.UtcNow;
        if(!force && state.Finished is not null && state.OpenCursor is null &&
            state.LastDelta is { } delta &&
            tickNow-delta<TimeSpan.FromMinutes(state.DeltaMinutes) &&
            state.LastOpen is { } open &&
            tickNow-open<TimeSpan.FromMinutes(state.OpenMinutes))
            return new(false,"not-due",0,0);
        if(!vault.IsConfigured || string.IsNullOrWhiteSpace(state.Username))
        {
            await SetErrorAsync(db,"service-credentials-missing",ct);
            return new(false,"service-credentials-missing",0,0);
        }
        var password=await vault.GetForIntegrationAsync("global",null,ServiceSecret,ct);
        if(string.IsNullOrWhiteSpace(password))
        {
            await SetErrorAsync(db,"service-credentials-missing",ct);
            return new(false,"service-credentials-missing",0,0);
        }
        var server=await ReadServerAsync(db,ct);
        if(ValidateServer(server) is not null ||
            Uri.TryCreate(server.ApiUrl,UriKind.Absolute,out var parsed) &&
            parsed.Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
        {
            await SetErrorAsync(db,"server-not-configured",ct);
            return new(false,"server-not-configured",0,0);
        }
        if(state.SourceUrl is not null &&
            !string.Equals(state.SourceUrl,server.ApiUrl,StringComparison.Ordinal))
        {
            await SetErrorAsync(db,"source-changed-reset-required",ct);
            return new(false,"source-changed-reset-required",0,0);
        }

        var now=DateTimeOffset.UtcNow;
        await ExecuteAsync(db,"UPDATE znuny_sync_control SET last_attempt_at=now(),last_error=NULL WHERE id=true",ct);
        var counter=new ZnunyReader.Counter(state.Budget);
        var imported=0;
        try
        {
            var session=await reader.SessionAsync(server,state.Username,password,counter,ct);
            if(state.SourceUrl is null)
            {
                await using var origin=new NpgsqlCommand("""
                    UPDATE znuny_sync_control SET source_api_url=@url WHERE id=true
                    """,db);
                origin.Parameters.AddWithValue("url",server.ApiUrl);
                await origin.ExecuteNonQueryAsync(ct);
            }
            if(state.Finished is null)
            {
                var start=state.Cursor ?? now.AddDays(-state.Lookback);
                var end=state.BootstrapEnd ?? now;
                if(state.Cursor is null)
                {
                    await using var startCmd=new NpgsqlCommand("""
                        UPDATE znuny_sync_control SET bootstrap_cursor=@cursor,
                            bootstrap_end=@end WHERE id=true
                        """,db);
                    startCmd.Parameters.Add("cursor",NpgsqlDbType.TimestampTz).Value=start.UtcDateTime;
                    startCmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=end.UtcDateTime;
                    await startCmd.ExecuteNonQueryAsync(ct);
                }

                // At most 8 one-day windows per cycle to avoid monopolising
                // Znuny. Cursor is updated only after all TicketGets succeed.
                var iterations=0;
                while(start<end && iterations++<8 && counter.Calls<state.Budget-15)
                {
                    var next=start.AddDays(1)<end ? start.AddDays(1):end;
                    var ids=await reader.SearchWindowAsync(server,session,"created",
                        start.AddMinutes(-2),next,counter,ct);
                    imported+=await LoadTicketsAsync(db,server,session,ids,counter,ct);
                    await SaveCursorAsync(db,"bootstrap_cursor",next,ct);
                    start=next;
                }
                if(start>=end)
                {
                    await using var finish=new NpgsqlCommand("""
                        UPDATE znuny_sync_control SET bootstrap_finished_at=now(),
                            delta_watermark=@end,last_delta_at=now(),
                            open_cursor=TIMESTAMPTZ '2000-01-01 00:00:00+00'
                        WHERE id=true
                        """,db);
                    finish.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=end.UtcDateTime;
                    await finish.ExecuteNonQueryAsync(ct);
                }
                return new(true,start>=end?"bootstrap-complete":"bootstrap-running",
                    counter.Calls,imported);
            }

            if(force || state.LastDelta is null ||
                now-state.LastDelta>=TimeSpan.FromMinutes(state.DeltaMinutes))
            {
                // Rewind 24h to cover Znuny server timezone / late indexed
                // changes, then advance the watermark only after a full cycle.
                var from=(state.Watermark ?? now.AddDays(-1)).AddDays(-1);
                var ids=await reader.SearchWindowAsync(server,session,"changed",from,now,
                    counter,ct);
                imported+=await LoadTicketsAsync(db,server,session,ids,counter,ct);
                await using var advance=new NpgsqlCommand("""
                    UPDATE znuny_sync_control SET delta_watermark=@now,
                        last_delta_at=now() WHERE id=true
                    """,db);
                advance.Parameters.Add("now",NpgsqlDbType.TimestampTz).Value=now.UtcDateTime;
                await advance.ExecuteNonQueryAsync(ct);
            }

            // Open tickets older than bootstrap range: a resumable 90-day
            // sweep from 2000 onwards, with a separate admin interval.
            if(state.OpenCursor is not null || force || state.LastOpen is null ||
                now-state.LastOpen>=TimeSpan.FromMinutes(state.OpenMinutes))
            {
                var start=state.OpenCursor ?? new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero);
                var cutoff=now.AddDays(-state.Lookback);
                if(state.OpenCursor is null)
                {
                    // Usually there are fewer than SearchLimit older open tickets.
                    // Fast path: ONE global request instead of scanning 26 years
                    // in 90-day windows on every open-ticket interval.
                    var fast=await reader.SearchAsync(server,session,"open",null,cutoff,counter,ct);
                    if(fast.Count<server.SearchLimit)
                    {
                        imported+=await LoadTicketsAsync(db,server,session,fast,counter,ct);
                        await ExecuteAsync(db, """
                            UPDATE znuny_sync_control SET last_open_at=now(),
                                open_cursor=NULL WHERE id=true
                            """,ct);
                        return new(true,"ok",counter.Calls,imported);
                    }
                    // Saturated global query: fall back to resumable windows.
                    await SaveCursorAsync(db,"open_cursor",start,ct);
                }
                var i=0;
                while(start<cutoff && i++<8 && counter.Calls<state.Budget-15)
                {
                    var next=start.AddDays(90)<cutoff ? start.AddDays(90):cutoff;
                    var ids=await reader.SearchWindowAsync(server,session,"open",
                        start.AddMinutes(-2),next,counter,ct);
                    imported+=await LoadTicketsAsync(db,server,session,ids,counter,ct);
                    await SaveCursorAsync(db,"open_cursor",next,ct);
                    start=next;
                }
                if(start>=cutoff)
                {
                    await ExecuteAsync(db, """
                        UPDATE znuny_sync_control SET last_open_at=now(),
                            open_cursor=NULL WHERE id=true
                        """,ct);
                }
            }
            return new(true,"ok",counter.Calls,imported);
        }
        catch(ZnunyReader.RemoteFailure ex)
        {
            await SetErrorAsync(db,ex.Code,ct);
            return new(true,ex.Code,counter.Calls,imported);
        }
        catch(Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            await SetErrorAsync(db,"database-or-config-error",ct);
            logger.LogWarning("Central Znuny sync failed: {Kind}",ex.GetType().Name);
            return new(true,"database-or-config-error",counter.Calls,imported);
        }
    }

    private async Task<int> LoadTicketsAsync(NpgsqlConnection db,ServerSettings server,
        string session,IReadOnlyList<string> ids,ZnunyReader.Counter counter,CancellationToken ct)
    {
        var count=0;
        foreach(var id in ids)
        {
            var item=await reader.GetAsync(server,session,id,counter,ct);
            // Missing ownership IDs are NOT inferred from untrusted names.
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO znuny_cached_tickets(ticket_id,ticket_number,title,queue,
                    state,state_type,owner_agent_id,responsible_agent_id,created_at,changed_at)
                VALUES(@id,@number,@title,@queue,@state,@type,@owner,@responsible,
                    @created,@changed)
                ON CONFLICT(ticket_id) DO UPDATE SET ticket_number=EXCLUDED.ticket_number,
                    title=EXCLUDED.title,queue=EXCLUDED.queue,state=EXCLUDED.state,
                    state_type=EXCLUDED.state_type,
                    owner_agent_id=EXCLUDED.owner_agent_id,
                    responsible_agent_id=EXCLUDED.responsible_agent_id,
                    created_at=EXCLUDED.created_at,changed_at=EXCLUDED.changed_at,
                    synced_at=now()
                """,db);
            cmd.Parameters.AddWithValue("id",item.Id);
            cmd.Parameters.AddWithValue("number",item.Number);
            cmd.Parameters.AddWithValue("title",item.Title);
            cmd.Parameters.AddWithValue("queue",item.Queue);
            cmd.Parameters.AddWithValue("state",item.State);
            cmd.Parameters.AddWithValue("type",item.StateType);
            cmd.Parameters.Add("owner",NpgsqlDbType.Integer).Value=
                item.OwnerId is int owner ? owner : DBNull.Value;
            cmd.Parameters.Add("responsible",NpgsqlDbType.Integer).Value=
                item.ResponsibleId is int responsible ? responsible : DBNull.Value;
            cmd.Parameters.Add("created",NpgsqlDbType.TimestampTz).Value=
                item.Created is { } created ? created.UtcDateTime : DBNull.Value;
            cmd.Parameters.Add("changed",NpgsqlDbType.TimestampTz).Value=
                item.Changed is { } changed ? changed.UtcDateTime : DBNull.Value;
            await cmd.ExecuteNonQueryAsync(ct);
            count++;
        }
        return count;
    }

    public async Task<Status> StatusAsync(CancellationToken ct)
    {
        await using var db=await OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT c.enabled,c.service_username,c.source_api_url,
                c.initial_lookback_days,c.delta_interval_minutes,c.open_interval_minutes,
                c.max_requests_per_cycle,c.bootstrap_cursor,c.bootstrap_end,
                c.bootstrap_finished_at,c.delta_watermark,c.last_delta_at,
                c.last_open_at,c.open_cursor,c.last_attempt_at,c.last_error,
                (SELECT count(*) FROM znuny_cached_tickets)
            FROM znuny_sync_control c WHERE id=true
            """,db);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new InvalidOperationException("Missing sync row");
        var enabled=r.GetBoolean(0);
        var username=r.GetString(1);
        var source=r.IsDBNull(2)?null:r.GetString(2);
        var lookback=r.GetInt32(3);
        var delta=r.GetInt32(4);
        var open=r.GetInt32(5);
        var budget=r.GetInt32(6);
        DateTimeOffset? Date(int i)=>r.IsDBNull(i)?null:new DateTimeOffset(r.GetDateTime(i),TimeSpan.Zero);
        var cursor=Date(7);var end=Date(8);var done=Date(9);var mark=Date(10);
        var last=Date(11);var opened=Date(12);var openCursor=Date(13);
        var attempt=Date(14);
        var error=r.IsDBNull(15)?null:r.GetString(15);
        var total=r.GetInt64(16);
        // Can't query vault while database reader is open? Vault opens its own connection.
        var hasPassword=vault.IsConfigured &&
            (await vault.ListNamesAsync("global",null,ct)).Contains(ServiceSecret);
        return new(enabled,username,hasPassword,source,lookback,delta,open,budget,
            cursor,end,done,mark,last,opened,openCursor,attempt,error,total);
    }

    private async Task<ConfigRow> LoadAsync(NpgsqlConnection db,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT enabled,service_username,source_api_url,initial_lookback_days,
                delta_interval_minutes,open_interval_minutes,max_requests_per_cycle,
                bootstrap_cursor,bootstrap_end,bootstrap_finished_at,delta_watermark,
                last_delta_at,last_open_at,open_cursor
            FROM znuny_sync_control WHERE id=true
            """,db);
        await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new InvalidOperationException("Missing sync row");
        DateTimeOffset? Date(int i)=>r.IsDBNull(i)?null:new DateTimeOffset(r.GetDateTime(i),TimeSpan.Zero);
        return new(r.GetBoolean(0),r.GetString(1),
            r.IsDBNull(2)?null:r.GetString(2),
            r.GetInt32(3),r.GetInt32(4),r.GetInt32(5),r.GetInt32(6),
            Date(7),Date(8),Date(9),Date(10),Date(11),Date(12),Date(13));
    }

    private static async Task<ServerSettings> ReadServerAsync(NpgsqlConnection db,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT setting_value::text FROM system_settings WHERE setting_key='tickets.znuny'
            """,db);
        var value=await cmd.ExecuteScalarAsync(ct) as string;
        return value is null?new ServerSettings():
            System.Text.Json.JsonSerializer.Deserialize<ServerSettings>(value,
                new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web)) ?? new ServerSettings();
    }

    private static async Task SaveCursorAsync(NpgsqlConnection db,string column,
        DateTimeOffset value,CancellationToken ct)
    {
        // Name originates exclusively from this fixed internal allowlist.
        if(column is not ("bootstrap_cursor" or "open_cursor"))
            throw new ArgumentException("Invalid cursor");
        await using var cmd=new NpgsqlCommand(
            $"UPDATE znuny_sync_control SET {column}=@value WHERE id=true",db);
        cmd.Parameters.Add("value",NpgsqlDbType.TimestampTz).Value=value.UtcDateTime;
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private static async Task SetErrorAsync(NpgsqlConnection db,string message,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            UPDATE znuny_sync_control SET last_error=@error WHERE id=true
            """,db);
        cmd.Parameters.AddWithValue("error",message.Length<=300?message:message[..300]);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private static async Task ExecuteAsync(NpgsqlConnection db,string sql,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand(sql,db);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);return db;
    }
}
