using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Graph;

public static class GraphEndpoints
{
    public static void MapPlenaroGraph(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/graph").RequireAuthorization();

        group.MapGet("/status", async (HttpContext context, EntraConfiguration entra,
            SecretVault vault, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (!entra.Enabled)
                return Results.Ok(new { enabled = false, linked = false, vaultReady = false, connected = false });
            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var linked = (await users.GetLoginsAsync(user))
                .Any(x => x.LoginProvider == EntraConfiguration.Provider &&
                    x.ProviderKey.StartsWith(entra.TenantId + ":", StringComparison.Ordinal));
            var connected = linked && vault.IsConfigured &&
                (await vault.ListNamesAsync("user", user.Id, ct)).Contains(GraphCalendarService.RefreshSecretName);
            return Results.Ok(new { enabled = true, linked, vaultReady = vault.IsConfigured, connected });
        });

        // Consent is explicitly initiated by a signed-in user with a linked Entra account.
        group.MapGet("/connect/start", async (HttpContext context, EntraConfiguration entra,
            SecretVault vault, UserManager<ApplicationUser> users) =>
        {
            if (!entra.Enabled) return Results.NotFound();
            if (!vault.IsConfigured)
                return Results.Problem("Secret-Vault ist nicht aktiviert.", statusCode: 503);
            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var linked = (await users.GetLoginsAsync(user))
                .Any(l => l.LoginProvider == EntraConfiguration.Provider &&
                    l.ProviderKey.StartsWith(entra.TenantId + ":", StringComparison.Ordinal));
            if (!linked)
                return Results.Conflict(new { error = "Microsoft-Konto zuerst unter Einstellungen verknüpfen." });

            var properties = new AuthenticationProperties { RedirectUri = "/week?graph=connected" };
            properties.Items["graphLocalUser"] = user.Id.ToString("D");
            return Results.Challenge(properties,
                authenticationSchemes: [GraphCalendarService.Scheme]);
        });

        group.MapPost("/disconnect", async (HttpContext context,
            IAntiforgery antiforgery, EntraConfiguration entra, SecretVault vault,
            UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (!entra.Enabled) return Results.NotFound();
            if (!await CsrfValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });
            if (!vault.IsConfigured)
                return Results.Problem("Secret-Vault ist nicht aktiviert.", statusCode: 503);
            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            await vault.DeleteAsync("user", user.Id, GraphCalendarService.RefreshSecretName, ct);
            return Results.NoContent();
        });

        // Explicitly queue an existing segment after upgrading or reconnecting.
        group.MapPost("/segments/{kind}/{id:guid}/sync",async(string kind,Guid id,
            HttpContext context,IAntiforgery antiforgery,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            if(!await CsrfValid(context,antiforgery))return Results.BadRequest(new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(context.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(kind is not ("task" or "ticket"))return Results.NotFound();
            await using var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
            await db.OpenAsync(ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            var sql=kind=="task"
                ?"SELECT id FROM task_segments WHERE id=@id AND owner_user_id=@user FOR UPDATE"
                :"""
                SELECT s.id FROM znuny_ticket_segments s
                JOIN znuny_approved_agent_bindings b ON b.user_id=s.user_id
                JOIN znuny_cached_tickets t ON t.ticket_id=s.ticket_id
                WHERE s.id=@id AND s.user_id=@user AND lower(t.state_type)<>'closed'
                FOR UPDATE OF s
                """;
            await using var check=new NpgsqlCommand(sql,db,tx);
            check.Parameters.AddWithValue("id",id);check.Parameters.AddWithValue("user",user.Id);
            if(await check.ExecuteScalarAsync(ct) is null)return Results.NotFound();
            await SegmentCalendarSync.QueueAsync(db,tx,kind,id,user.Id,ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        // Also expose deletion failures whose local segment no longer exists.
        group.MapGet("/segments/status",async(HttpContext context,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            var user=await users.GetUserAsync(context.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
            await db.OpenAsync(ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT status,count(*)::int FROM segment_calendar_sync
                WHERE user_id=@user AND status IN ('pending','syncing','error')
                GROUP BY status
                """,db);
            cmd.Parameters.AddWithValue("user",user.Id);
            int pending=0,errors=0;
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)){
                if(reader.GetString(0)=="error")errors+=reader.GetInt32(1);
                else pending+=reader.GetInt32(1);
            }
            context.Response.Headers.CacheControl="no-store";
            return Results.Ok(new{pending,errors});
        });

        group.MapGet("/calendar", async (string? start, string? end, HttpContext context,
            UserManager<ApplicationUser> users, EntraConfiguration entra,
            GraphCalendarService calendar, CancellationToken ct) =>
        {
            if (!entra.Enabled) return Results.NotFound();
            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            // Keep requests bounded; user-supplied URLs, mailboxes or raw Graph paths are forbidden.
            if (!DateTimeOffset.TryParse(start, out var from) ||
                !DateTimeOffset.TryParse(end, out var to) ||
                from >= to || to - from > TimeSpan.FromDays(14))
                return Results.BadRequest(new { error = "Zeitraum muss zwischen 1 Minute und 14 Tagen liegen." });
            try
            {
                var events = await calendar.ReadAsync(user.Id, from, to, ct);
                context.Response.Headers.CacheControl = "no-store";
                return Results.Ok(events);
            }
            catch (GraphConnectionException e)
            {
                return e.Reason == "consent-required"
                    ? Results.Conflict(new { error = "Outlook-Berechtigung fehlt oder ist abgelaufen.", code = "consent-required" })
                    : Results.Problem("Microsoft Graph momentan nicht erreichbar.", statusCode: 502);
            }
        });
    }

    private static async Task<bool> CsrfValid(HttpContext context, IAntiforgery tokens)
    {
        try { await tokens.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
