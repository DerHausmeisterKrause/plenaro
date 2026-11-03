using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
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

        group.MapGet("/", async (UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            var accounts = await users.Users.AsNoTracking()
                .OrderBy(u => u.DisplayName).ThenBy(u => u.Email).Take(500).ToListAsync(ct);
            var approvedAgents = new Dictionary<Guid, int>();
            await using (var connection = new NpgsqlConnection(config.GetConnectionString("Plenaro")))
            {
                await connection.OpenAsync(ct);
                await using var command = new NpgsqlCommand(
                    "SELECT user_id,agent_id FROM znuny_approved_agent_bindings",connection);
                await using var rows = await command.ExecuteReaderAsync(ct);
                while (await rows.ReadAsync(ct))
                    approvedAgents.Add(rows.GetGuid(0),rows.GetInt32(1));
            }
            var candidates = new Dictionary<Guid, int?>();
            await using (var connection = new NpgsqlConnection(config.GetConnectionString("Plenaro")))
            {
                await connection.OpenAsync(ct);
                await using var command = new NpgsqlCommand(
                    "SELECT user_id,candidate_user_id FROM znuny_user_settings",connection);
                await using var rows = await command.ExecuteReaderAsync(ct);
                while (await rows.ReadAsync(ct))
                    candidates[rows.GetGuid(0)] = rows.IsDBNull(1) ? null : rows.GetInt32(1);
            }
            var result = new List<object>();
            foreach (var user in accounts)
                result.Add(new
                {
                    id = user.Id,
                    email = user.Email ?? user.UserName,
                    loginType = (await users.GetLoginsAsync(user)).Any(
                        login => login.LoginProvider == EntraConfiguration.Provider) ? "Entra" : "Local",
                    hasLocalPassword = await users.HasPasswordAsync(user),
                    displayName = user.DisplayName,
                    role = (await users.GetRolesAsync(user)).Contains("Admin") ? "Admin" : "User",
                    disabled = user.IsDisabled,
                    agentId = approvedAgents.TryGetValue(user.Id,out var agentId)
                        ? (int?)agentId : null,
                    candidateUserId = candidates.GetValueOrDefault(user.Id)
                });
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateUserRequest request, HttpContext http,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            PlenaroIdentityDbContext db, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!ValidName(request.DisplayName) || !ValidEmail(request.Email) ||
                !ValidRole(request.Role) || !ValidAgentId(request.AgentId) ||
                !ValidAgentId(request.CandidateUserId) ||
                request.Password is not { Length: >= 12 and <= 1024 })
                return Results.BadRequest(new { error = "Name, E-Mail, Rolle oder Passwort ungültig." });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(db,ct);
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
            try
            {
                await SaveAgentIdAsync(db,user.Id,request.AgentId,ct);
                if (request.CandidateUserId is not null)
                    await SaveCandidateIdAsync(db,user.Id,request.CandidateUserId,ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new { error = "Diese Znuny-Agenten-ID ist bereits einem anderen Plenaro-Benutzer zugewiesen." });
            }
            await tx.CommitAsync(ct);
            return Results.Created($"/api/v1/admin/users/{user.Id}", new { id = user.Id });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateUserRequest request,
            ClaimsPrincipal actor, HttpContext http, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, PlenaroIdentityDbContext db, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!ValidName(request.DisplayName) || !ValidRole(request.Role) ||
                !TryParseAgentId(request.AgentId, out var agentId, out var agentIdProvided) ||
                !TryParseAgentId(request.CandidateUserId, out var candidateUserId, out var candidateProvided))
                return Results.BadRequest(new { error = "Name, Rolle oder Znuny-Agenten-ID ungültig." });

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

            // Changes to agent access and Identity account update are atomic.
            try
            {
                if (agentIdProvided)
                    await SaveAgentIdAsync(db,user.Id,agentId,ct);
                if (candidateProvided)
                    await SaveCandidateIdAsync(db,user.Id,candidateUserId,ct);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new { error = "Diese Znuny-Agenten-ID ist bereits einem anderen Plenaro-Benutzer zugewiesen." });
            }

            // Revoke all previously issued cookies when account privileges change.
            var stamp = await users.UpdateSecurityStampAsync(user);
            if (!stamp.Succeeded) return IdentityError(stamp);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        // Destructive account deletion is restricted to authenticated administrators.
        // Business tasks are never silently cascaded away: the existing RESTRICT
        // foreign keys remain authoritative and must be resolved explicitly.
        group.MapDelete("/{id:guid}", async (Guid id, [FromBody] DeleteUserRequest request,
            ClaimsPrincipal actor, HttpContext http, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, PlenaroIdentityDbContext db,
            CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (users.GetUserId(actor) == id.ToString("D"))
                return Results.Conflict(new { error = "Du kannst dein eigenes Administratorkonto nicht löschen." });

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(db, ct);
            var user = await users.FindByIdAsync(id.ToString("D"));
            if (user is null) return Results.NotFound();

            // Require the visible, exact account identifier as a second confirmation.
            var expected = user.Email ?? user.UserName ?? "";
            if (string.IsNullOrWhiteSpace(expected) ||
                !string.Equals(request.Confirmation, expected, StringComparison.Ordinal))
                return Results.BadRequest(new { error = "Zur Bestätigung muss die E-Mail-Adresse des Benutzers exakt eingegeben werden." });

            if (await users.IsInRoleAsync(user, "Admin") && !user.IsDisabled)
            {
                var admins = await users.GetUsersInRoleAsync("Admin");
                if (admins.Count(a => !a.IsDisabled) <= 1)
                    return Results.Conflict(new { error = "Der letzte aktive Administrator kann nicht gelöscht werden." });
            }

            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            var transaction = (NpgsqlTransaction)tx.GetDbTransaction();

            // The database was deliberately designed to RESTRICT owned business
            // tasks. Explicitly check to provide a useful 409 instead of an HTTP 500.
            await using (var dependencies = new NpgsqlCommand("""
                SELECT
                    (SELECT count(*) FROM tasks WHERE owner_user_id=@user) +
                    (SELECT count(*) FROM task_segments WHERE owner_user_id=@user) +
                    (SELECT count(*) FROM time_entries WHERE owner_user_id=@user)
                """, connection, transaction))
            {
                dependencies.Parameters.AddWithValue("user", user.Id);
                var count = (long)(await dependencies.ExecuteScalarAsync(ct) ?? 0L);
                if (count > 0)
                    return Results.Conflict(new { error =
                        "Dieser Benutzer besitzt noch lokale Aufgaben oder Zeitbuchungen. " +
                        "Bitte die geschäftlichen Daten zuerst übertragen oder entfernen. " +
                        "Alternativ das Benutzerkonto sperren." });
            }

            // Only the local identity and its user-scoped data are deleted.
            // Do not ban the Entra provider key: the same person may sign in
            // again and receive a fresh unprivileged Plenaro account.

            try
            {
                // PostgreSQL cascades personal rows (Graph/OTRS encrypted secrets,
                // login bindings, preferences, ticket plans, timer records).
                var result = await users.DeleteAsync(user);
                if (!result.Succeeded) return IdentityError(result);
                await tx.CommitAsync(ct);
                return Results.NoContent();
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.ForeignKeyViolation })
            {
                return Results.Conflict(new { error =
                    "Benutzer hat noch abhängige Geschäftsdaten. " +
                    "Bitte diese zuerst übertragen oder entfernen." });
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return Results.Conflict(new { error =
                    "Benutzer hat noch abhängige Geschäftsdaten. " +
                    "Bitte diese zuerst übertragen oder entfernen." });
            }
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
    private static bool ValidAgentId(int? agentId) => agentId is null or > 0;

    private static bool TryParseAgentId(JsonElement raw,out int? agentId,out bool provided)
    {
        agentId = null;
        provided = raw.ValueKind != JsonValueKind.Undefined;
        if (!provided || raw.ValueKind == JsonValueKind.Null) return true;
        if (raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt32(out var id) || id<=0)
            return false;
        agentId = id;
        return true;
    }

    private static async Task SaveAgentIdAsync(PlenaroIdentityDbContext db,Guid userId,
        int? agentId,CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
        await using var command = agentId is { } assigned
            ? new NpgsqlCommand("""
                INSERT INTO znuny_approved_agent_bindings(user_id,agent_id)
                VALUES(@user,@agent)
                ON CONFLICT(user_id) DO UPDATE SET
                    agent_id=EXCLUDED.agent_id,approved_at=now()
                """,connection,transaction)
            : new NpgsqlCommand(
                "DELETE FROM znuny_approved_agent_bindings WHERE user_id=@user",
                connection,transaction);
        command.Parameters.AddWithValue("user",userId);
        if (agentId is { } value) command.Parameters.AddWithValue("agent",value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task SaveCandidateIdAsync(PlenaroIdentityDbContext db,
        Guid userId, int? candidateUserId, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var transaction = (NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction();
        await using var command = new NpgsqlCommand("""
            INSERT INTO znuny_user_settings(user_id,candidate_user_id)
            VALUES(@user,@candidate)
            ON CONFLICT(user_id) DO UPDATE
            SET candidate_user_id=EXCLUDED.candidate_user_id,updated_at=now()
            """,connection,transaction);
        command.Parameters.AddWithValue("user",userId);
        command.Parameters.Add("candidate",NpgsqlTypes.NpgsqlDbType.Integer).Value =
            candidateUserId is int assigned ? assigned : DBNull.Value;
        await command.ExecuteNonQueryAsync(ct);
    }

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

    public sealed record CreateUserRequest(string Email, string DisplayName, string Password,
        string Role, int? AgentId = null, int? CandidateUserId = null);
    // Absence of agentId on legacy clients means "leave unchanged"; explicit
    // null means "revoke". Never silently revoke on unrelated profile updates.
    public sealed record UpdateUserRequest(string DisplayName, string Role, bool Disabled,
        JsonElement AgentId = default, JsonElement CandidateUserId = default);
    public sealed record ResetPasswordRequest(string Password);
    public sealed record DeleteUserRequest(string Confirmation);
}
