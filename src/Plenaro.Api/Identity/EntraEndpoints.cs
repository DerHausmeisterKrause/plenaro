using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Plenaro.Api.Identity;

/// <summary>
/// Every validated single-tenant Entra identity receives a separate Plenaro User
/// account on first login. Never merge by email or grant Znuny rights automatically.
/// Existing explicit Entra links remain valid for backwards compatibility.
/// </summary>
public static class EntraEndpoints
{
    private const long ProvisionLock = 496576540203L;

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
            if (!external.Succeeded || external.Principal is null ||
                !TryExternalKey(external.Principal, config, out var key))
            {
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                return Results.Redirect("/login?entra=failed");
            }

            // Provisioning already happened in the validated OIDC callback.
            // Never resolve by E-Mail, UPN, display name or client-supplied ID.
            var user = await users.FindByLoginAsync(EntraConfiguration.Provider, key);
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            if (user is null) return Results.Redirect("/login?entra=failed");
            if (user.IsDisabled || await users.IsLockedOutAsync(user))
                return Results.Redirect("/login?entra=denied");

            // Roles and ticket authorizations always come from local Identity/admin.
            await signIn.SignInAsync(user, isPersistent: false);
            return Results.Redirect("/workspace");
        }).RequireRateLimiting("login");
    }

    /// <summary>Call only after OIDC has validated issuer, audience and signature.</summary>
    internal static async Task<ApplicationUser?> EnsureProvisionedAsync(
        ClaimsPrincipal principal, EntraConfiguration config,
        UserManager<ApplicationUser> users, PlenaroIdentityDbContext db,
        CancellationToken ct)
    {
        if (!TryExternalKey(principal, config, out var key)) return null;
        // Serialize first-sign-in with admin mutations and concurrent SSO requests.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var guard = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)",
            connection, (NpgsqlTransaction)tx.GetDbTransaction()))
        {
            guard.Parameters.AddWithValue("key", ProvisionLock);
            await guard.ExecuteNonQueryAsync(ct);
        }

        // A deleted Plenaro user is eligible for normal first-login provisioning
        // again. Account deletion revokes the old local ID and all its permissions,
        // but it is not an Entra sign-in ban. The new user starts with role User
        // and without Znuny agent/candidate permissions.

        var existing = await users.FindByLoginAsync(EntraConfiguration.Provider, key);
        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return existing;
        }

        var tenantId = Guid.Parse(principal.FindFirst("tid")!.Value);
        var objectId = Guid.Parse(principal.FindFirst("oid")!.Value);
        var preferred = principal.FindFirst("preferred_username")?.Value ??
            principal.FindFirst(ClaimTypes.Email)?.Value ??
            principal.FindFirst("email")?.Value;
        var name = principal.FindFirst("name")?.Value?.Trim();
        var email = preferred?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 ||
            !email.Contains('@') || email.Any(char.IsControl))
            email = null;
        // Never merge by email. Identity requires unique nonempty email values,
        // so collision/missing-email cases receive an unrouteable synthetic address.
        if (email is null || await users.FindByEmailAsync(email) is not null)
            email = $"entra-{objectId:N}@{tenantId:N}.invalid";

        var user = new ApplicationUser
        {
            UserName = $"entra-{tenantId:N}-{objectId:N}",
            Email = email,
            EmailConfirmed = false,
            DisplayName = !string.IsNullOrWhiteSpace(name)
                ? name[..Math.Min(name.Length, 128)]
                : (preferred is { Length: > 0 }
                    ? preferred[..Math.Min(preferred.Length, 128)]
                    : "Microsoft-Benutzer"),
            LockoutEnabled = true,
        };
        var created = await users.CreateAsync(user); // No local password.
        if (!created.Succeeded) throw new InvalidOperationException("Entra account creation failed.");
        var role = await users.AddToRoleAsync(user, "User");
        if (!role.Succeeded) throw new InvalidOperationException("Entra role assignment failed.");
        var login = await users.AddLoginAsync(user,
            new UserLoginInfo(EntraConfiguration.Provider, key, "Microsoft Entra ID"));
        if (!login.Succeeded) throw new InvalidOperationException("Entra identity binding failed.");
        await tx.CommitAsync(ct);
        return user;
    }

    private static bool TryExternalKey(ClaimsPrincipal principal,
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
}
