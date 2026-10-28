using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Plenaro.Api.Secrets;
using Plenaro.Api.Graph;

namespace Plenaro.Api.Identity;

/// <summary>
/// SSO never creates a local user or maps email automatically. An Entra object ID
/// only receives an existing local account after that account explicitly approves
/// the linking in an authenticated CSRF-protected request.
/// </summary>
public static class EntraEndpoints
{
    public static void MapPlenaroEntra(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1/auth/entra");

        api.MapGet("/status", async (HttpContext context, EntraConfiguration config,
            UserManager<ApplicationUser> users) =>
        {
            if (!config.Enabled) return Results.Ok(new { enabled = false, linked = false });
            var user = await users.GetUserAsync(context.User);
            var linked = user is not null && (await users.GetLoginsAsync(user))
                .Any(login => login.LoginProvider == EntraConfiguration.Provider);
            return Results.Ok(new { enabled = true, linked });
        });

        api.MapGet("/start", (HttpContext context, EntraConfiguration config) =>
        {
            if (!config.Enabled) return Results.NotFound();
            if (context.User.Identity?.IsAuthenticated == true) return Results.Redirect("/");
            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = "/api/v1/auth/entra/complete"
            }, authenticationSchemes: [EntraConfiguration.Scheme]);
        }).RequireRateLimiting("login");

        api.MapGet("/complete", async (HttpContext context, EntraConfiguration config,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
        {
            if (!config.Enabled) return Results.NotFound();
            var external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!external.Succeeded || external.Principal is null)
                return Results.Redirect("/login?entra=failed");

            if (!TryExternalKey(external.Principal, config, out var key))
            {
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                return Results.Redirect("/login?entra=failed");
            }

            // Link challenges never log the visitor into another local account.
            if (external.Properties?.Items.ContainsKey("linkUserId") == true)
                return Results.Redirect("/settings?entra=pending");

            var user = await users.FindByLoginAsync(EntraConfiguration.Provider, key);
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            if (user is null) return Results.Redirect("/login?entra=not-linked");
            if (user.IsDisabled || await users.IsLockedOutAsync(user))
                return Results.Redirect("/login?entra=denied");

            // Roles/permissions always come from Plenaro Identity, never Entra claims.
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/");
        }).RequireRateLimiting("login");

        api.MapGet("/link/start", async (HttpContext context, EntraConfiguration config,
            UserManager<ApplicationUser> users) =>
        {
            if (!config.Enabled) return Results.NotFound();
            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();

            var properties = new AuthenticationProperties
            {
                RedirectUri = "/api/v1/auth/entra/complete"
            };
            properties.Items["linkUserId"] = user.Id.ToString("D");
            return Results.Challenge(properties, authenticationSchemes: [EntraConfiguration.Scheme]);
        }).RequireAuthorization().RequireRateLimiting("login");

        api.MapGet("/link/pending", async (HttpContext context, EntraConfiguration config,
            UserManager<ApplicationUser> users) =>
        {
            if (!config.Enabled) return Results.NotFound();
            var local = await users.GetUserAsync(context.User);
            if (local is null || local.IsDisabled) return Results.Unauthorized();
            var external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!external.Succeeded || external.Principal is null ||
                external.Properties?.Items.TryGetValue("linkUserId", out var initiatingUserId) != true ||
                initiatingUserId != local.Id.ToString("D") ||
                !TryExternalKey(external.Principal, config, out _))
                return Results.BadRequest(new { error = "Verknüpfung abgelaufen. Bitte erneut starten." });

            var account = external.Principal.FindFirst("preferred_username")?.Value ??
                external.Principal.FindFirst("name")?.Value ?? "Microsoft-Konto";
            return Results.Ok(new { account, tenantId = config.TenantId });
        }).RequireAuthorization();

        api.MapPost("/link/confirm", async (HttpContext context, IAntiforgery antiforgery,
            EntraConfiguration config, UserManager<ApplicationUser> users) =>
        {
            if (!config.Enabled) return Results.NotFound();
            if (!await CsrfIsValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });

            var local = await users.GetUserAsync(context.User);
            if (local is null || local.IsDisabled) return Results.Unauthorized();
            var external = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
            if (!external.Succeeded || external.Principal is null ||
                external.Properties?.Items.TryGetValue("linkUserId", out var initiatingUserId) != true ||
                initiatingUserId != local.Id.ToString("D") ||
                !TryExternalKey(external.Principal, config, out var key))
                return Results.BadRequest(new { error = "Verknüpfung abgelaufen. Bitte erneut starten." });

            // Also block linking a second Entra identity to the same local user.
            var existing = await users.GetLoginsAsync(local);
            if (existing.Any(x => x.LoginProvider == EntraConfiguration.Provider &&
                                  x.ProviderKey != key))
                return Results.Conflict(new { error = "Dieses Konto ist bereits mit einer anderen Microsoft-Identität verbunden." });

            var owner = await users.FindByLoginAsync(EntraConfiguration.Provider, key);
            if (owner is not null && owner.Id != local.Id)
                return Results.Conflict(new { error = "Diese Microsoft-Identität ist bereits mit einem anderen Konto verknüpft." });

            if (owner is null)
            {
                var result = await users.AddLoginAsync(local,
                    new UserLoginInfo(EntraConfiguration.Provider, key, "Microsoft Entra ID"));
                if (!result.Succeeded)
                    return Results.Conflict(new { error = "Microsoft-Identität konnte nicht verknüpft werden." });
            }

            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("login");

        api.MapPost("/unlink", async (HttpContext context, IAntiforgery antiforgery,
            EntraConfiguration config, UserManager<ApplicationUser> users,
            SignInManager<ApplicationUser> signIn, SecretVault vault) =>
        {
            if (!config.Enabled) return Results.NotFound();
            if (!await CsrfIsValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });

            var user = await users.GetUserAsync(context.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            // Never lock a user out of Plenaro by removing their only login method.
            if (!await users.HasPasswordAsync(user))
                return Results.Conflict(new { error = "Ohne lokales Passwort kann Microsoft nicht getrennt werden." });
            var link = (await users.GetLoginsAsync(user))
                .FirstOrDefault(l => l.LoginProvider == EntraConfiguration.Provider);
            if (link is null) return Results.NoContent();

            var result = await users.RemoveLoginAsync(user, link.LoginProvider, link.ProviderKey);
            if (!result.Succeeded) return Results.Conflict(new { error = "Verknüpfung konnte nicht entfernt werden." });
            // An unlinked Microsoft account must not retain delegated calendar access.
            if (vault.IsConfigured)
                await vault.DeleteAsync("user", user.Id,
                    GraphCalendarService.RefreshSecretName, context.RequestAborted);
            await users.UpdateSecurityStampAsync(user);
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static bool TryExternalKey(System.Security.Claims.ClaimsPrincipal principal,
        EntraConfiguration config, out string providerKey)
    {
        providerKey = "";
        var tenant = principal.FindFirst("tid")?.Value;
        var objectId = principal.FindFirst("oid")?.Value;
        if (!Guid.TryParse(tenant, out var tenantId) ||
            !Guid.TryParse(objectId, out var userId) ||
            tenantId.ToString("D") != config.TenantId ||
            userId == Guid.Empty)
            return false;
        providerKey = $"{tenantId:D}:{userId:D}";
        return true;
    }

    private static async Task<bool> CsrfIsValid(HttpContext context, IAntiforgery tokens)
    {
        try { await tokens.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}
