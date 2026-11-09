using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Classic ticket detail's *local* writable features: notes, URL, stopwatch
/// and corrections. NEVER implies a remote Znuny booking or ticket mutation.
/// </summary>
public static class ZnunyTicketDetailEndpoints
{
    public sealed record WorkspaceInput(string Description,string TicketUrl);
    public sealed record AdjustmentInput(int DeltaMinutes,string? Note);
    public sealed record SetTimeInput(long TargetSeconds);
    public sealed record TimeSummary(long BookedSeconds,long RunningSeconds,
        DateTimeOffset? RunningSince,long AdjustedMinutes,int SessionCount,
        long TransferredSeconds,long UnbookedSeconds);

    private const string AccessFilter = """
        FROM znuny_cached_tickets t
        JOIN znuny_approved_agent_bindings b ON b.user_id=@user
        LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
        WHERE t.ticket_id=@ticket
          AND (t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id
          OR (p.candidate_user_id IS NOT NULL AND (t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,p.candidate_user_id))
          AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,p.candidate_user_id)))
          OR EXISTS(SELECT 1 FROM znuny_ticket_segments s
              WHERE s.ticket_id=t.ticket_id AND (
                s.user_id=@user OR
                EXISTS(SELECT 1 FROM "AspNetUsers" account,
                    unnest(s.participants) AS participant(email)
                    WHERE account."Id"=@user
                      AND account."NormalizedEmail" IS NOT NULL
                      AND account."NormalizedEmail"=upper(trim(participant.email))))))
        """;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/tickets/{ticketId}/workspace",async(string ticketId,
            HttpContext http,UserManager<ApplicationUser> users,IConfiguration config,
            CancellationToken ct)=>{
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            if(!await CanAccess(db,user.Value,ticketId,false,ct))return Results.NotFound();
            await using var cmd=new NpgsqlCommand("""
                SELECT description,ticket_url FROM znuny_ticket_workspaces
                WHERE user_id=@user AND ticket_id=@ticket
                """,db);
            Bind(cmd,user.Value,ticketId);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct)
                ? Results.Ok(new {description=reader.GetString(0),ticketUrl=reader.GetString(1)})
                : Results.Ok(new {description="",ticketUrl=""});
        });

        group.MapPut("/tickets/{ticketId}/workspace",async(string ticketId,WorkspaceInput input,
            HttpContext http,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            if(input.Description is null || input.Description.Length>10000 ||
                input.TicketUrl is null || input.TicketUrl.Length>2048 ||
                (input.TicketUrl.Length>0 && !SafeUrl(input.TicketUrl)))
                return Results.BadRequest(new {error="Beschreibung oder Ticket-URL ungültig."});
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand($"""
                INSERT INTO znuny_ticket_workspaces(user_id,ticket_id,description,ticket_url)
                SELECT @user,t.ticket_id,@description,@url
                {AccessFilter}
                ON CONFLICT(user_id,ticket_id) DO UPDATE
                SET description=EXCLUDED.description,ticket_url=EXCLUDED.ticket_url,updated_at=now()
                RETURNING ticket_id
                """,db);
            Bind(cmd,user.Value,ticketId);
            cmd.Parameters.AddWithValue("description",input.Description);
            cmd.Parameters.AddWithValue("url",input.TicketUrl.Trim());
            return await cmd.ExecuteScalarAsync(ct) is null ? Results.NotFound():Results.NoContent();
        });

        group.MapGet("/tickets/{ticketId}/time",async(string ticketId,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            if(!await CanAccess(db,user.Value,ticketId,false,ct))return Results.NotFound();
            return Results.Ok(await ReadTime(db,user.Value,ticketId,ct));
        });

        group.MapGet("/tickets/{ticketId}/time/bookings",async(string ticketId,
            HttpContext http,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            if(!await CanAccess(db,user.Value,ticketId,false,ct))return Results.NotFound();
            await using var cmd=new NpgsqlCommand("""
                SELECT id,source_seconds,booked_minutes,cost_center,order_value,
                    short_description,note,status,article_id,created_at,rejection_code
                FROM znuny_ticket_time_bookings
                WHERE user_id=@user AND ticket_id=@ticket
                ORDER BY created_at DESC LIMIT 100
                """,db);
            Bind(cmd,user.Value,ticketId);
            var rows=new List<object>();
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))rows.Add(new{
                id=reader.GetGuid(0),sourceSeconds=reader.GetInt64(1),
                bookedMinutes=reader.GetInt32(2),costCenter=reader.GetString(3),
                order=reader.GetString(4),shortDescription=reader.GetString(5),
                note=reader.GetString(6),status=reader.GetString(7),
                articleId=reader.IsDBNull(8)?null:reader.GetString(8),
                createdAt=new DateTimeOffset(reader.GetDateTime(9),TimeSpan.Zero),
                rejectionCode=reader.IsDBNull(10)?null:reader.GetString(10)
            });
            return Results.Ok(rows);
        });

        group.MapPost("/tickets/{ticketId}/time/start",async(string ticketId,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand($"""
                INSERT INTO znuny_ticket_time_entries(id,user_id,ticket_id,started_at)
                SELECT @id,@user,t.ticket_id,clock_timestamp()
                {AccessFilter} AND lower(t.state_type)<>'closed'
                RETURNING id
                """,db);
            Bind(cmd,user.Value,ticketId);
            cmd.Parameters.AddWithValue("id",Guid.NewGuid());
            try{
                if(await cmd.ExecuteScalarAsync(ct) is null)return Results.NotFound();
                return Results.Ok(await ReadTime(db,user.Value,ticketId,ct));
            }
            catch(PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation){
                return Results.Conflict(new {error="Es läuft bereits ein Ticket-Timer. Bitte zuerst stoppen."});
            }
        });

        group.MapPost("/tickets/{ticketId}/time/stop",async(string ticketId,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            if(!await CanAccess(db,user.Value,ticketId,false,ct))return Results.NotFound();
            await using var cmd=new NpgsqlCommand("""
                UPDATE znuny_ticket_time_entries
                SET ended_at=GREATEST(clock_timestamp(),started_at+interval '1 second')
                WHERE user_id=@user AND ticket_id=@ticket AND ended_at IS NULL
                RETURNING id
                """,db);
            Bind(cmd,user.Value,ticketId);
            if(await cmd.ExecuteScalarAsync(ct) is null)
                return Results.Conflict(new {error="Für dieses Ticket läuft kein Timer."});
            return Results.Ok(await ReadTime(db,user.Value,ticketId,ct));
        });

        // Desktop parity: clicking the visible clock edits an absolute
        // HH:MM:SS value. Only the difference is stored, at exact-second
        // resolution; a running clock continues after saving.
        group.MapPut("/tickets/{ticketId}/time",async(string ticketId,
            SetTimeInput input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            if(input.TargetSeconds is <0 or >864000)
                return Results.BadRequest(new{error="Timerzeit muss zwischen 0 und 240 Stunden liegen."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await using(var guard=new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@user::text || ':' || @ticket, 2))",db,tx)){
                Bind(guard,user.Value,ticketId);
                await guard.ExecuteNonQueryAsync(ct);
            }
            if(!await CanAccess(db,user.Value,ticketId,false,ct))return Results.NotFound();
            var before=await ReadTime(db,user.Value,ticketId,ct);
            var active=before.RunningSince is null?0L:
                Math.Max(0L,(long)(DateTimeOffset.UtcNow-before.RunningSince.Value).TotalSeconds);
            // Like WPF: edit only the still-unbooked duration, not the
            // aggregate of earlier successfully transferred bookings.
            var current=Math.Max(0,before.BookedSeconds+active-before.TransferredSeconds);
            var difference=input.TargetSeconds-current;
            if(Math.Abs(difference)>86400)
                return Results.BadRequest(new{error="Pro Änderung sind höchstens 24 Stunden erlaubt."});
            if(difference!=0){
                await using var cmd=new NpgsqlCommand("""
                    INSERT INTO znuny_ticket_time_corrections(id,user_id,ticket_id,delta_seconds)
                    VALUES(@id,@user,@ticket,@seconds)
                    """,db,tx);
                Bind(cmd,user.Value,ticketId);
                cmd.Parameters.AddWithValue("id",Guid.NewGuid());
                cmd.Parameters.AddWithValue("seconds",difference);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Results.Ok(await ReadTime(db,user.Value,ticketId,ct));
        });

        group.MapPost("/tickets/{ticketId}/time/adjust",async(string ticketId,
            AdjustmentInput input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var user=await UserId(http,users);
            if(user is null)return Results.Unauthorized();
            if(input.DeltaMinutes==0 || input.DeltaMinutes is < -1440 or > 1440 ||
                input.Note?.Length>500)
                return Results.BadRequest(new {error="Ungültige Zeitkorrektur."});
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand($"""
                INSERT INTO znuny_ticket_time_adjustments(id,user_id,ticket_id,delta_minutes,note)
                SELECT @id,@user,t.ticket_id,@minutes,@note
                {AccessFilter}
                RETURNING id
                """,db);
            Bind(cmd,user.Value,ticketId);
            cmd.Parameters.AddWithValue("id",Guid.NewGuid());
            cmd.Parameters.AddWithValue("minutes",input.DeltaMinutes);
            cmd.Parameters.AddWithValue("note",input.Note?.Trim()??"");
            if(await cmd.ExecuteScalarAsync(ct) is null)return Results.NotFound();
            return Results.Ok(await ReadTime(db,user.Value,ticketId,ct));
        });
    }

    private static bool SafeUrl(string value){
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri))return false;
        return uri.Scheme is "https" or "http" && uri.Host.Length>0 &&
            uri.UserInfo.Length==0;
    }

    internal static async Task<TimeSummary> ReadTime(NpgsqlConnection db,Guid user,string ticket,
        CancellationToken ct){
        await using var cmd=new NpgsqlCommand("""
            SELECT
              COALESCE((SELECT sum(GREATEST(1,EXTRACT(EPOCH FROM ended_at-started_at))::bigint)
                FROM znuny_ticket_time_entries WHERE user_id=@user AND ticket_id=@ticket
                  AND ended_at IS NOT NULL),0),
              COALESCE((SELECT sum(delta_minutes) FROM znuny_ticket_time_adjustments
                WHERE user_id=@user AND ticket_id=@ticket),0),
              COALESCE((SELECT sum(delta_seconds) FROM znuny_ticket_time_corrections
                WHERE user_id=@user AND ticket_id=@ticket),0),
              (SELECT started_at FROM znuny_ticket_time_entries
                WHERE user_id=@user AND ticket_id=@ticket AND ended_at IS NULL LIMIT 1),
              (SELECT count(*) FROM znuny_ticket_time_entries
                WHERE user_id=@user AND ticket_id=@ticket AND ended_at IS NOT NULL),
              COALESCE((SELECT sum(source_seconds) FROM znuny_ticket_time_bookings
                WHERE user_id=@user AND ticket_id=@ticket AND status='confirmed'),0)
            """,db);
        Bind(cmd,user,ticket);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        var adjustment=reader.GetInt64(1);
        var seconds=Math.Max(0,reader.GetInt64(0)+adjustment*60+reader.GetInt64(2));
        var transferred=reader.GetInt64(5);
        var runningSince=reader.IsDBNull(3)?(DateTimeOffset?)null:
            new DateTimeOffset(reader.GetDateTime(3),TimeSpan.Zero);
        var running=runningSince is null?0L:Math.Max(0L,
            (long)(DateTimeOffset.UtcNow-runningSince.Value).TotalSeconds);
        return new TimeSummary(seconds,0,runningSince,adjustment,
            checked((int)reader.GetInt64(4)),transferred,
            Math.Max(0,seconds+running-transferred));
    }

    private static async Task<bool> CanAccess(NpgsqlConnection db,Guid user,string ticket,
        bool onlyOpen,CancellationToken ct){
        await using var cmd=new NpgsqlCommand($"""
            SELECT EXISTS(SELECT 1 {AccessFilter}
            {(onlyOpen?"AND lower(t.state_type)<>'closed'":"")})
            """,db);
        Bind(cmd,user,ticket);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }
    private static void Bind(NpgsqlCommand cmd,Guid user,string ticket){
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.AddWithValue("ticket",ticket);
    }
    private static async Task<Guid?> UserId(HttpContext http,UserManager<ApplicationUser> users){
        var account=await users.GetUserAsync(http.User);
        return account is null||account.IsDisabled?null:account.Id;
    }
    private static async Task<bool> Csrf(HttpContext http,IAntiforgery anti){
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static IResult BadCsrf()=>Results.BadRequest(new {error="Ungültiger Sicherheitstoken."});
    private static async Task<NpgsqlConnection> Open(IConfiguration config,CancellationToken ct){
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }
}
