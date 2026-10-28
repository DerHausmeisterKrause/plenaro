using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Plenaro.Api.Identity;

/// <summary>Only administrators can manage local users; every mutation requires CSRF.</summary>
public static class AdminUsersEndpoints
{
    private const long ManagementLock = 496576540203L;

    public static void MapAdminUsers(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/admin/users")
            .RequireAuthorization("AdminOnly");

        group.MapGet("/", async (UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var accounts = await users.Users.AsNoTracking()
                .OrderBy(u => u.DisplayName).ThenBy(u => u.Email).Take(500).ToListAsync(ct);
            var result = new List<object>();
            foreach (var user in accounts)
                result.Add(new
                {
                    id = user.Id,
                    email = user.Email,
                    displayName = user.DisplayName,
                    role = (await users.GetRolesAsync(user)).Contains("Admin") ? "Admin" : "User",
                    disabled = user.IsDisabled
                });
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateUserRequest request, HttpContext http,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            PlenaroIdentityDbContext db, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!ValidName(request.DisplayName) || !ValidEmail(request.Email) ||
                !ValidRole(request.Role) || request.Password is not { Length: >= 12 and <= 1024 })
                return Results.BadRequest(new { error = "Name, E-Mail, Rolle oder Passwort ungültig." });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var email = request.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = request.DisplayName.Trim(),
                EmailConfirmed = true, // Created by the administrator, no email service configured yet.
                LockoutEnabled = true
            };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return IdentityError(result);
            result = await users.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded) return IdentityError(result);
            await tx.CommitAsync(ct);
            return Results.Created($"/api/v1/admin/users/{user.Id}", new { id = user.Id });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request,
            ClaimsPrincipal actor, HttpContext http, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, PlenaroIdentityDbContext db, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!ValidName(request.DisplayName) || !ValidRole(request.Role))
                return Results.BadRequest(new { error = "Name oder Rolle ungültig." });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(db, ct);
            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Results.NotFound();
            var isAdmin = await users.IsInRoleAsync(user, "Admin");
            var requestedAdmin = request.Role == "Admin";
            var actorId = users.GetUserId(actor);

            if (actorId == user.Id.ToString() && (request.Disabled || !requestedAdmin))
                return Results.Conflict(new { error = "Du kannst dein eigenes Administratorkonto nicht sperren oder herabstufen." });

            if (isAdmin && (request.Disabled || !requestedAdmin) && !user.IsDisabled)
            {
                // This check and mutation share the advisory lock to prevent concurrent last-admin removal.
                var admins = await users.GetUsersInRoleAsync("Admin");
                if (admins.Count(a => !a.IsDisabled) <= 1)
                    return Results.Conflict(new { error = "Der letzte aktive Administrator muss erhalten bleiben." });
            }

            user.DisplayName = request.DisplayName.Trim();
            user.IsDisabled = request.Disabled;
            var update = await users.UpdateAsync(user);
            if (!update.Succeeded) return IdentityError(update);

            if (isAdmin != requestedAdmin)
            {
                var remove = await users.RemoveFromRoleAsync(user, isAdmin ? "Admin" : "User");
                if (!remove.Succeeded) return IdentityError(remove);
                var add = await users.AddToRoleAsync(user, request.Role);
                if (!add.Succeeded) return IdentityError(add);
            }

            // Revoke all previously issued cookies if a role, name or disabled status changes.
            var stamp = await users.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded) return IdentityError(stamp);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request,
            HttpContext http, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (request.Password is not { Length: >= 12 and <= 1024 })
                return Results.BadRequest(new { error = "Passwort ungültig." });

            var user = await users.FindByIdAsync(id.ToString());
            if (user is null) return Results.NotFound();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var result = await users.ResetPasswordAsync(user, token, request.Password);
            return result.Succeeded ? Results.NoContent() : IdentityError(result);
        });
    }

    private static bool ValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 128;
    private static bool ValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 256 && email.Contains('@');
    private static bool ValidRole(string? role) => role is "Admin" or "User";

    private static async Task LockAsync(PlenaroIdentityDbContext db, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection,
            (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction());
        cmd.Parameters.AddWithValue("key", ManagementLock);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ValidCsrf(HttpContext ctx, IAntiforgery tokens)
    {
        try { await tokens.ValidateRequestAsync(ctx); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() => Results.BadRequest(new { error = "Sicherheitstoken fehlt oder ist ungültig." });
    private static IResult IdentityError(IdentityResult result) =>
        Results.BadRequest(new { error = string.Join(" ", result.Errors.Select(e => e.Description)) });

    public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);
    public sealed record UpdateUserRequest(string DisplayName, string Role, bool Disabled);
    public sealed record ResetPasswordRequest(string Password);
}
