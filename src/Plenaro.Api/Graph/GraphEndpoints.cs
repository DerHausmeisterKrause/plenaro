using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
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
