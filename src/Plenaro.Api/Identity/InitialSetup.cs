using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Plenaro.Api.Identity;

public static class InitialSetup
{
    private const long LockKey = 496576540202L;

    public static async Task<int> IssueAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await LockAsync(connection, transaction);
        await using (var check = new NpgsqlCommand(
            """SELECT EXISTS(SELECT 1 FROM "AspNetUsers")""", connection, transaction))
        {
            if (await check.ExecuteScalarAsync() is true)
            {
                Console.WriteLine("Installation bereits abgeschlossen.");
                return 1;
            }
        }
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        await using (var cmd = new NpgsqlCommand("""
            INSERT INTO plenaro_setup_tokens(id, token_hash, expires_at)
            VALUES (1, @digest, now() + interval '15 minutes')
            ON CONFLICT (id) DO UPDATE SET
            token_hash=excluded.token_hash, expires_at=excluded.expires_at
            """, connection, transaction))
        {
            cmd.Parameters.AddWithValue("digest", digest);
            await cmd.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        Console.WriteLine("Einrichtungscode (15 Minuten gültig):");
        Console.WriteLine($"SETUP_TOKEN={code}");
        return 0;
    }

    public static async Task<IResult> CompleteAsync(
        SetupRequest request,
        PlenaroIdentityDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole<Guid>> roles,
        CancellationToken ct)
    {
        if (request.Code is not { Length: 64 } ||
            request.Code.Any(c => !Uri.IsHexDigit(c)) ||
            string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
            string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 128 ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            return Results.BadRequest(new { error = "Bitte die Eingaben prüfen." });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var nativeTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        await LockAsync(connection, nativeTransaction, ct);

        if (await db.Users.AnyAsync(ct))
            return Results.Conflict(new { error = "Installation bereits abgeschlossen." });

        byte[]? stored;
        await using (var cmd = new NpgsqlCommand("""
            SELECT token_hash FROM plenaro_setup_tokens
            WHERE id=1 AND expires_at>now()
            """, connection, nativeTransaction))
            stored = await cmd.ExecuteScalarAsync(ct) as byte[];

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(request.Code.ToUpperInvariant()));
        if (stored is not { Length: 32 } || !CryptographicOperations.FixedTimeEquals(stored, digest))
            return Results.BadRequest(new { error = "Einrichtungscode ungültig oder abgelaufen." });

        foreach (var name in new[] { "Admin", "User" })
        {
            if (await roles.RoleExistsAsync(name)) continue;
            if (!(await roles.CreateAsync(new IdentityRole<Guid>(name))).Succeeded)
                return Results.Problem("Rollen konnten nicht erstellt werden.");
        }

        var email = request.Email.Trim();
        var admin = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(), LockoutEnabled = true,
        };
        var created = await users.CreateAsync(admin, request.Password);
        if (!created.Succeeded)
            return Results.BadRequest(new { error = string.Join(" ", created.Errors.Select(e => e.Description)) });
        if (!(await users.AddToRoleAsync(admin, "Admin")).Succeeded)
            return Results.Problem("Adminrolle konnte nicht vergeben werden.");

        await db.Database.ExecuteSqlRawAsync("DELETE FROM plenaro_setup_tokens WHERE id=1", ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task LockAsync(
        NpgsqlConnection conn, NpgsqlTransaction transaction, CancellationToken ct = default)
    {
        await using var cmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", conn, transaction);
        cmd.Parameters.AddWithValue("key", LockKey);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record SetupRequest(string Code, string Email, string DisplayName, string Password);
