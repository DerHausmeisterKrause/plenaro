using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Identity;

/// <summary>Only local Admin users may configure external identity providers.</summary>
public static class EntraAdminEndpoints
{
    public sealed record EntraInput(string TenantId, string ClientId, string ClientSecret);

    public static void MapPlenaroEntraAdmin(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/admin/entra").RequireAuthorization("AdminOnly");

        api.MapGet("/", (EntraConfiguration config, SecretVault vault) =>
            Results.Ok(new
            {
                enabled = config.Enabled,
                vaultReady = vault.IsConfigured,
                httpsRequired = config.RequireHttps,
                tenantId = config.Enabled ? config.TenantId : "",
                clientId = config.Enabled ? config.ClientId : "",
                secretConfigured = config.Enabled
            }));

        api.MapPut("/", async (HttpContext http, EntraInput input, EntraConfiguration config,
            SecretVault vault, IAntiforgery antiforgery,
            IOptionsMonitorCache<OpenIdConnectOptions> oidc,
            IOptionsMonitorCache<OAuthOptions> graph, CancellationToken ct) =>
        {
            if (!await CheckCsrf(http, antiforgery)) return Results.BadRequest(new { error = "Sicherheitstoken ungültig." });
            if (!vault.IsConfigured) return Results.Problem("Der Secret-Vault muss zuerst eingerichtet werden.", statusCode: 503);
            if (!config.RequireHttps) return Results.BadRequest(new { error = "Entra erfordert HTTPS." });
            if (!Guid.TryParse(input.TenantId, out var tenant) || tenant == Guid.Empty ||
                !Guid.TryParse(input.ClientId, out var client) || client == Guid.Empty)
                return Results.BadRequest(new { error = "Tenant- und Client-ID müssen gültige GUIDs sein." });
            var secret = string.IsNullOrEmpty(input.ClientSecret) && config.Enabled
                ? config.ClientSecret : input.ClientSecret;
            if (string.IsNullOrWhiteSpace(secret) || secret.Length is < 16 or > 4096)
                return Results.BadRequest(new { error = "Das Client-Secret muss 16 bis 4096 Zeichen enthalten." });
            await config.SaveAsync(vault, true, tenant.ToString("D"), client.ToString("D"), secret, ct);
            oidc.TryRemove(EntraConfiguration.Scheme);
            graph.TryRemove(Plenaro.Api.Graph.GraphCalendarService.Scheme);
            return Results.NoContent();
        }).RequireRateLimiting("login");

        api.MapDelete("/", async (HttpContext http, EntraConfiguration config,
            SecretVault vault, IAntiforgery antiforgery,
            IOptionsMonitorCache<OpenIdConnectOptions> oidc,
            IOptionsMonitorCache<OAuthOptions> graph, CancellationToken ct) =>
        {
            if (!await CheckCsrf(http, antiforgery)) return Results.BadRequest(new { error = "Sicherheitstoken ungültig." });
            if (!vault.IsConfigured) return Results.Problem("Der Secret-Vault ist nicht verfügbar.", statusCode: 503);
            await config.DisableAsync(vault, ct);
            oidc.TryRemove(EntraConfiguration.Scheme);
            graph.TryRemove(Plenaro.Api.Graph.GraphCalendarService.Scheme);
            return Results.NoContent();
        }).RequireRateLimiting("login");
    }

    private static async Task<bool> CheckCsrf(HttpContext http, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
