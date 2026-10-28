using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Plenaro.Api.Identity;

public static class AuthEndpoints
{
    public static void MapPlenaroAuth(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        app.MapGet("/api/v1/setup/status", async (
            PlenaroIdentityDbContext db, CancellationToken ct) =>
        {
            var required = !await db.Users.AnyAsync(ct);
            return Results.Ok(new { required });
        });

        app.MapPost("/api/v1/setup/complete", async (
            SetupRequest request, HttpContext context, IAntiforgery antiforgery,
            PlenaroIdentityDbContext db, UserManager<ApplicationUser> users,
            RoleManager<IdentityRole<Guid>> roles, CancellationToken ct) =>
        {
            if (!await CsrfIsValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });

            return await InitialSetup.CompleteAsync(request, db, users, roles, ct);
        }).RequireRateLimiting("login");

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var token = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = token.RequestToken });
        });

        group.MapPost("/login", async (
            LoginRequest request,
            HttpContext context,
            IAntiforgery antiforgery,
            SignInManager<ApplicationUser> signIn,
            UserManager<ApplicationUser> users) =>
        {
            if (!await CsrfIsValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });

            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) || request.Email.Length > 256)
                return Results.Unauthorized();

            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null)
            {
                // Keep the response identical to an incorrect password; no account enumeration.
                return Results.Unauthorized();
            }

            var result = await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        }).RequireRateLimiting("login");

        group.MapGet("/me", async (
            System.Security.Claims.ClaimsPrincipal principal,
            UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            return Results.Ok(new
            {
                id = user.Id,
                email = user.Email,
                name = user.DisplayName,
                roles,
            });
        }).RequireAuthorization();

        group.MapPost("/logout", async (
            HttpContext context, IAntiforgery antiforgery, SignInManager<ApplicationUser> signIn) =>
        {
            if (!await CsrfIsValid(context, antiforgery))
                return Results.BadRequest(new { error = "Ungültiger Sicherheitstoken." });
            await signIn.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        // Initial RBAC canary. Real user-management APIs are a separate, audited step.
        app.MapGet("/api/v1/admin/ping", () => Results.Ok(new { admin = true }))
            .RequireAuthorization("AdminOnly");
    }

    private static async Task<bool> CsrfIsValid(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private sealed record LoginRequest(string Email, string Password);
}
