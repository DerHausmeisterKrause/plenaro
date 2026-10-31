using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

public static class ZnunySyncEndpoints
{
    public sealed record SyncConfiguration(bool Enabled,string ServiceUsername,
        int InitialLookbackDays=365,int DeltaIntervalMinutes=15,
        int OpenIntervalMinutes=360,int MaxRequestsPerCycle=200,
        int CandidateDefaultAgentId=1);
    public sealed record ServicePassword(string Password);
    public sealed record ResetRequest(string Confirmation);

    public static void MapPlenaroZnunyCentral(this WebApplication app)
    {
        var admin=app.MapGroup("/api/v1/admin/znuny").RequireAuthorization("AdminOnly");
        var reader=app.MapGroup("/api/v1/znuny").RequireAuthorization();

        admin.MapGet("/sync", async (ZnunyCentralSync sync,CancellationToken ct) =>
            Results.Ok(await sync.StatusAsync(ct)));

        admin.MapPut("/sync",async (SyncConfiguration input,HttpContext http,
            IAntiforgery anti,IConfiguration config,CancellationToken ct) =>
        {
            if(!await Csrf(http,anti))return BadCsrf();
            if(input.ServiceUsername is null ||
                input.ServiceUsername.Length>256 ||
                input.ServiceUsername.Any(char.IsControl) ||
                input.InitialLookbackDays is < 7 or > 3650 ||
                input.DeltaIntervalMinutes is < 3 or > 1440 ||
                input.OpenIntervalMinutes is < 15 or > 10080 ||
                input.MaxRequestsPerCycle is < 20 or > 1000 ||
                input.CandidateDefaultAgentId < 1)
                return Results.BadRequest(new {error="Ungültige Sync-Einstellungen."});
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                UPDATE znuny_sync_control SET enabled=@enabled,
                    service_username=@username,initial_lookback_days=@lookback,
                    delta_interval_minutes=@delta,open_interval_minutes=@open,
                    max_requests_per_cycle=@budget,
                    candidate_default_agent_id=@defaultAgent,updated_at=now()
                WHERE id=true
                """,db);
            cmd.Parameters.AddWithValue("enabled",input.Enabled);
            cmd.Parameters.AddWithValue("username",input.ServiceUsername.Trim());
            cmd.Parameters.AddWithValue("lookback",input.InitialLookbackDays);
            cmd.Parameters.AddWithValue("delta",input.DeltaIntervalMinutes);
            cmd.Parameters.AddWithValue("open",input.OpenIntervalMinutes);
            cmd.Parameters.AddWithValue("budget",input.MaxRequestsPerCycle);
            cmd.Parameters.AddWithValue("defaultAgent",input.CandidateDefaultAgentId);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        admin.MapPut("/sync/password",async (ServicePassword value,HttpContext http,
            IAntiforgery anti,SecretVault vault,CancellationToken ct) =>
        {
            if(!await Csrf(http,anti))return BadCsrf();
            if(!vault.IsConfigured)
                return Results.Problem("Secret-Vault fehlt.",statusCode:503);
            if(value.Password is not { Length: >= 1 and <= 4096 })
                return Results.BadRequest(new {error="Ungültiges Service-Passwort."});
            await vault.SetAsync("global",null,ZnunyCentralSync.ServiceSecret,value.Password,ct);
            return Results.NoContent();
        });
        admin.MapDelete("/sync/password",async (HttpContext http,IAntiforgery anti,
            SecretVault vault,CancellationToken ct) =>
        {
            if(!await Csrf(http,anti))return BadCsrf();
            if(!vault.IsConfigured)
                return Results.Problem("Secret-Vault fehlt.",statusCode:503);
            await vault.DeleteAsync("global",null,ZnunyCentralSync.ServiceSecret,ct);
            return Results.NoContent();
        });

        // User click: synchronous 1 bounded cycle, background service uses same
        // singleton and the same PostgreSQL advisory lock across processes.
        admin.MapPost("/sync/run", async (HttpContext http,IAntiforgery anti,
            ZnunyCentralSync sync,CancellationToken ct) =>
        {
            if(!await Csrf(http,anti))return BadCsrf();
            return Results.Ok(await sync.RunAsync(true,ct));
        }).RequireRateLimiting("znuny-test");

        // Destructive reset explicitly requires disabling sync, then a phrase.
        // This prevents accidental carry-over between different Znuny servers.
        admin.MapPost("/sync/reset",async (ResetRequest value,HttpContext http,
            IAntiforgery anti,IConfiguration config,CancellationToken ct) =>
        {
            if(!await Csrf(http,anti))return BadCsrf();
            if(value.Confirmation!="ZNUNY CACHE ZURUECKSETZEN")
                return Results.BadRequest(new {error="Bestätigung fehlt."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT enabled FROM znuny_sync_control WHERE id=true FOR UPDATE
                """,db,tx);
            if(await cmd.ExecuteScalarAsync(ct) is true)
                return Results.Conflict(new {error="Zentrale Synchronisation zuerst deaktivieren."});
            // PostgreSQL lock protects this operation against a background run.
            await using var lockCmd=new NpgsqlCommand(
                "SELECT pg_try_advisory_xact_lock(@key)",db,tx);
            lockCmd.Parameters.AddWithValue("key",713220261031L);
            if(await lockCmd.ExecuteScalarAsync(ct) is not true)
                return Results.Conflict(new {error="Synchronisation läuft gerade."});
            await using var reset=new NpgsqlCommand("""
                DELETE FROM znuny_cached_tickets;
                UPDATE znuny_sync_control SET source_api_url=NULL,
                    bootstrap_cursor=NULL,bootstrap_end=NULL,
                    bootstrap_finished_at=NULL,delta_watermark=NULL,
                    last_delta_at=NULL,last_open_at=NULL,open_cursor=NULL,
                    last_error=NULL,last_attempt_at=NULL,updated_at=now()
                WHERE id=true
                """,db,tx);
            await reset.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        reader.MapGet("/tickets",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,
            string? view,string? search,int? limit,int? page,CancellationToken ct) =>
        {
            var account=await users.GetUserAsync(http.User);
            if(account is null || account.IsDisabled)return Results.Unauthorized();
            if(view is not (null or "mine" or "new" or "completed"))
                return Results.BadRequest(new{error="Ungültige Ansicht."});
            if(search is {Length:>100})
                return Results.BadRequest(new{error="Suchbegriff zu lang."});
            var pageSize=Math.Clamp(limit??100,1,100);
            if(page is < 0 or > 1000)return Results.BadRequest(new {error="Ungültige Seite."});
            var skip=(page??0)*pageSize;
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT t.ticket_id,t.ticket_number,t.title,t.queue,t.state,
                    t.state_type,t.owner_agent_id,t.responsible_agent_id,
                    t.created_at,t.changed_at
                FROM znuny_cached_tickets t
                INNER JOIN znuny_approved_agent_bindings b ON b.user_id=@user
                CROSS JOIN znuny_sync_control c
                WHERE (
                    (@view='new' AND
                      (t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,c.candidate_default_agent_id)) AND
                      (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,c.candidate_default_agent_id)))
                    OR (@view<>'new' AND
                      (t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id))
                )
                AND (
                    (@view='completed' AND lower(t.state_type)='closed')
                    OR (@view<>'completed' AND lower(t.state_type)<>'closed')
                )
                AND (@query='' OR t.ticket_number ILIKE '%'||@query||'%'
                    OR t.title ILIKE '%'||@query||'%')
                ORDER BY t.changed_at DESC NULLS LAST,t.ticket_id DESC
                LIMIT @max OFFSET @skip
                """,db);
            cmd.Parameters.AddWithValue("user",account.Id);
            cmd.Parameters.AddWithValue("view",view??"mine");
            // This search is applied only *after* agent authorization in SQL.
            cmd.Parameters.AddWithValue("query",search?.Trim()??"");
            cmd.Parameters.AddWithValue("max",pageSize);
            cmd.Parameters.AddWithValue("skip",skip);
            var tickets=new List<object>();
            await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))
            {
                DateTimeOffset? Date(int col)=>r.IsDBNull(col)?null:
                    new DateTimeOffset(r.GetDateTime(col),TimeSpan.Zero);
                tickets.Add(new {
                    ticketId=r.GetString(0),ticketNumber=r.GetString(1),
                    title=r.GetString(2),queue=r.GetString(3),
                    state=r.GetString(4),stateType=r.GetString(5),
                    ownerId=r.IsDBNull(6)?(int?)null:r.GetInt32(6),
                    responsibleId=r.IsDBNull(7)?(int?)null:r.GetInt32(7),
                    createdAt=Date(8),changedAt=Date(9)
                });
            }
            return Results.Ok(tickets);
        });

        ZnunyTicketPlanningEndpoints.Map(reader);

        reader.MapGet("/tickets/status",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,
            CancellationToken ct) =>
        {
            var account=await users.GetUserAsync(http.User);
            if(account is null || account.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT a.agent_id,(SELECT enabled FROM znuny_sync_control)
                FROM znuny_approved_agent_bindings a WHERE a.user_id=@user
                """,db);
            cmd.Parameters.AddWithValue("user",account.Id);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))
                return Results.Ok(new{approved=false,agentId=(int?)null,syncEnabled=false});
            return Results.Ok(new{approved=true,agentId=(int?)r.GetInt32(0),
                syncEnabled=r.GetBoolean(1)});
        });
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration cfg,CancellationToken ct)
    {
        var db=new NpgsqlConnection(cfg.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);return db;
    }
    private static async Task<bool> Csrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static IResult BadCsrf()=>Results.BadRequest(
        new {error="Ungültiger Sicherheitstoken."});
}
