using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

public static class ZnunySyncEndpoints
{
    public sealed record ShowAllInput(bool ShowAll);
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

        // Re-run the historical import without deleting cached tickets or any
        // user-specific planning. The same advisory lock as RunAsync prevents
        // resetting the cursor during an active remote fetch.
        admin.MapPost("/sync/bootstrap/restart", async (ResetRequest value,
            HttpContext http, IAntiforgery anti, IConfiguration config,
            ZnunyCentralSync sync, CancellationToken ct) =>
        {
            if(!await Csrf(http,anti)) return BadCsrf();
            if(value.Confirmation != "ZNUNY ERSTIMPORT NEU STARTEN")
                return Results.BadRequest(new {error="Bestätigung zum Neustart fehlt."});

            await using (var db=await Open(config,ct))
            {
                await using var tx=await db.BeginTransactionAsync(ct);
                await using (var lockCmd=new NpgsqlCommand(
                    "SELECT pg_try_advisory_xact_lock(@key)",db,tx))
                {
                    lockCmd.Parameters.AddWithValue("key",713220261031L);
                    if(await lockCmd.ExecuteScalarAsync(ct) is not true)
                        return Results.Conflict(new {
                            error="Es läuft bereits eine Znuny-Synchronisierung. Bitte danach erneut versuchen."
                        });
                }
                // Capture a fixed historical range at the moment of the click.
                // Keep delta_watermark and its last-run timestamp untouched so
                // that changes made during this backfill can be caught up later.
                await using var restart=new NpgsqlCommand("""
                    UPDATE znuny_sync_control SET
                        bootstrap_cursor=now()-initial_lookback_days * INTERVAL '1 day',
                        bootstrap_end=now(),
                        bootstrap_finished_at=NULL,
                        last_error=NULL,
                        updated_at=now()
                    WHERE id=true
                    """,db,tx);
                if(await restart.ExecuteNonQueryAsync(ct)!=1)
                    return Results.Problem("Znuny-Synchronisierungsstatus fehlt.",statusCode:503);
                await tx.CommitAsync(ct);
            }
            // Begin one bounded cycle immediately; subsequent daily windows
            // are processed by the existing 45-second background scheduler.
            return Results.Ok(await sync.RunAsync(true,ct));
        }).RequireRateLimiting("znuny-bootstrap-restart");

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
            ZnunyTicketVisibility visibility,
            string? view,string? search,string? queue,string? state,
            string? sort,string? direction,int? limit,int? page,CancellationToken ct) =>
        {
            var account=await users.GetUserAsync(http.User);
            if(account is null || account.IsDisabled)return Results.Unauthorized();
            if(view is not (null or "mine" or "new" or "completed"))
                return Results.BadRequest(new{error="Ungültige Ansicht."});
            if(queue is {Length:>200} || state is {Length:>100})
                return Results.BadRequest(new{error="Filter zu lang."});
            if(sort is not (null or "changed" or "created" or "title" or "number") ||
                direction is not (null or "asc" or "desc"))
                return Results.BadRequest(new{error="Ungültige Sortierung."});
            if(search is {Length:>100})
                return Results.BadRequest(new{error="Suchbegriff zu lang."});
            var pageSize=Math.Clamp(limit??100,1,100);
            if(page is < 0 or > 1000)return Results.BadRequest(new {error="Ungültige Seite."});
            var skip=(page??0)*pageSize;
            IReadOnlySet<string> allowed;
            try {allowed=await visibility.VisibleAsync(account.Id,ct);}
            catch(ZnunyTicketVisibility.UnavailableException){
                return Results.Problem("Deine persönlichen Znuny-Queue-Rechte "+
                    "konnten nicht bestätigt werden. Es werden keine "+
                    "ungeprüften Tickets angezeigt.",statusCode:503);
            }
            if(allowed.Count==0)return Results.Ok(Array.Empty<object>());
            await using var db=await Open(config,ct);
            var sortColumn=sort switch {
                "created"=>"t.created_at",
                "title"=>"t.title",
                "number"=>"t.ticket_number",
                _=>"t.changed_at"
            };
            var sortDirection=direction=="asc"?"ASC":"DESC";
            await using var cmd=new NpgsqlCommand($"""
                SELECT t.ticket_id,t.ticket_number,t.title,t.queue,t.state,
                    t.state_type,t.owner_agent_id,t.responsible_agent_id,
                    t.created_at,t.changed_at
                FROM znuny_cached_tickets t
                INNER JOIN znuny_approved_agent_bindings b ON b.user_id=@user
                LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
                LEFT JOIN znuny_ticket_search_documents d ON d.ticket_id=t.ticket_id
                LEFT JOIN znuny_ticket_workspaces w ON w.ticket_id=t.ticket_id
                    AND w.user_id=@user
                WHERE (
                    (@view='new' AND p.candidate_user_id IS NOT NULL AND
                      (t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,p.candidate_user_id)) AND
                      (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,p.candidate_user_id)))
                    OR (@view<>'new' AND
                      (t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id))
                )
                AND ((@view='completed' AND lower(t.state_type)='closed')
                  OR (@view<>'completed' AND lower(t.state_type)<>'closed'))
                AND t.ticket_id=ANY(@authorizedIds)
                AND (@queue='' OR t.queue=@queue)
                AND (@state='' OR t.state=@state)
                -- WPF-style candidate inclusion/exclusion. Split comma,
                -- semicolon and whitespace-delimited phrases; token matching
                -- is case-insensitive and ignores regex/wildcard metacharacters.
                AND (@view<>'new' OR COALESCE(p.show_all_candidate_pool,false) OR
                  (
                    EXISTS(
                      SELECT 1 FROM regexp_split_to_table(
                        COALESCE(p.candidate_keywords,''),'[[:cntrl:],;]+') AS phrase
                      WHERE length(trim(phrase))>0 AND
                        strpos(lower(t.title||' '||COALESCE(d.content,'')),
                          lower(trim(phrase)))>0
                    )
                    AND NOT EXISTS(
                      SELECT 1 FROM regexp_split_to_table(
                        COALESCE(p.candidate_exclude_keywords,''),'[[:cntrl:],;]+') AS phrase
                      WHERE length(trim(phrase))>0 AND
                        strpos(lower(t.title||' '||COALESCE(d.content,'')),
                          lower(trim(phrase)))>0
                    )
                  ))
                -- Full-text terms are applied *after* ticket authorization;
                -- also search article body and user's own notes.
                AND (@query='' OR NOT EXISTS(
                  SELECT 1 FROM regexp_split_to_table(@query,'[[:space:]]+') AS term
                  WHERE length(term)>0 AND strpos(lower(
                    concat_ws(' ',t.ticket_number,t.title,t.queue,t.state,
                      COALESCE(d.content,''),COALESCE(w.description,''),COALESCE(w.ticket_url,''))),
                    lower(term))=0
                ))
                ORDER BY {sortColumn} {sortDirection} NULLS LAST,t.ticket_id DESC
                LIMIT @max OFFSET @skip
                """,db);
            cmd.Parameters.AddWithValue("user",account.Id);
            cmd.Parameters.Add("authorizedIds",NpgsqlTypes.NpgsqlDbType.Array |
                NpgsqlTypes.NpgsqlDbType.Varchar).Value=allowed.ToArray();
            cmd.Parameters.AddWithValue("view",view??"mine");
            // This search is applied only *after* agent authorization in SQL.
            cmd.Parameters.AddWithValue("query",search?.Trim()??"");
            cmd.Parameters.AddWithValue("queue",queue?.Trim()??"");
            cmd.Parameters.AddWithValue("state",state?.Trim()??"");
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

        reader.MapPut("/tickets/candidates/show-all",async(ShowAllInput input,
            HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return Results.BadRequest(new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO znuny_user_settings(user_id,show_all_candidate_pool)
                SELECT @user,@show FROM znuny_approved_agent_bindings WHERE user_id=@user
                ON CONFLICT(user_id) DO UPDATE SET
                    show_all_candidate_pool=EXCLUDED.show_all_candidate_pool,
                    updated_at=now()
                RETURNING user_id
                """,db);
            cmd.Parameters.AddWithValue("user",user.Id);
            cmd.Parameters.AddWithValue("show",input.ShowAll);
            return await cmd.ExecuteScalarAsync(ct) is null?Results.Forbid():Results.NoContent();
        });

        ZnunyTicketPlanningEndpoints.Map(reader);
        ZnunyTicketDetailEndpoints.Map(reader);
        ZnunyTicketRemoteEndpoints.Map(reader);

        reader.MapGet("/tickets/status",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,
            CancellationToken ct) =>
        {
            var account=await users.GetUserAsync(http.User);
            if(account is null || account.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT a.agent_id,(SELECT enabled FROM znuny_sync_control),
                    p.candidate_user_id,COALESCE(p.show_all_candidate_pool,false),
                    COALESCE(p.candidate_keywords,'')
                FROM znuny_approved_agent_bindings a
                LEFT JOIN znuny_user_settings p ON p.user_id=a.user_id
                WHERE a.user_id=@user
                """,db);
            cmd.Parameters.AddWithValue("user",account.Id);
            await using var r=await cmd.ExecuteReaderAsync(ct);
            if(!await r.ReadAsync(ct))
                return Results.Ok(new{approved=false,agentId=(int?)null,syncEnabled=false,
                    candidateUserId=(int?)null,showAllCandidatePoolTickets=false,
                    hasCandidateKeywords=false});
            return Results.Ok(new{approved=true,agentId=(int?)r.GetInt32(0),
                syncEnabled=r.GetBoolean(1),
                candidateUserId=r.IsDBNull(2)?(int?)null:r.GetInt32(2),
                showAllCandidatePoolTickets=r.GetBoolean(3),
                hasCandidateKeywords=!string.IsNullOrWhiteSpace(r.GetString(4))});
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
