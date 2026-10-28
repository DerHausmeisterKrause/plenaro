using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Plenaro.Api.Identity;

/// <summary>
/// One-time local bootstrap via `docker compose exec web dotnet Plenaro.Api.dll --bootstrap-admin`.
/// Deliberately NO unauthenticated network endpoint for creating the initial administrator.
/// </summary>
public static class AdminBootstrap
{
    private const long BootstrapLockKey = 496576540202L;

    public static async Task<int> RunAsync(IServiceProvider services, string connectionString, TextReader input, TextWriter output)
    {
        await using var gate = new NpgsqlConnection(connectionString);
        await gate.OpenAsync();
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", gate))
        {
            acquire.Parameters.AddWithValue("key", BootstrapLockKey);
            await acquire.ExecuteNonQueryAsync();
        }

        try
        {
            using var scope = services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var db = scope.ServiceProvider.GetRequiredService<PlenaroIdentityDbContext>();

            if (await users.Users.AnyAsync())
            {
                await output.WriteLineAsync("Die Erstinstallation ist bereits abgeschlossen. Kein weiterer Bootstrap möglich.");
                return 1;
            }

            await output.WriteLineAsync("Plenaro: einmalige lokale Administrator-Ersteinrichtung.");
            await output.WriteAsync("Admin-E-Mail: ");
            await output.FlushAsync();
            var email = (await input.ReadLineAsync())?.Trim() ?? string.Empty;

            await output.WriteAsync("Anzeigename: ");
            await output.FlushAsync();
            var displayName = (await input.ReadLineAsync())?.Trim() ?? string.Empty;

            await output.WriteAsync("Passwort (mindestens 12 Zeichen, Groß-/Kleinbuchstaben, Ziffer und Sonderzeichen): ");
            await output.FlushAsync();
            var password = ReadSecret(input, output);

            await output.WriteAsync("Passwort wiederholen: ");
            await output.FlushAsync();
            var confirm = ReadSecret(input, output);

            if (string.IsNullOrWhiteSpace(email) || displayName.Length is < 1 or > 128 ||
                password != confirm)
            {
                await output.WriteLineAsync("Ungültige Eingaben oder Passwörter stimmen nicht überein.");
                return 1;
            }

            // Both roles and the admin account must be committed as one unit, or not at all.
            await using var transaction = await db.Database.BeginTransactionAsync();
            foreach (var roleName in new[] { "Admin", "User" })
            {
                if (await roles.RoleExistsAsync(roleName)) continue;
                var result = await roles.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                {
                    await output.WriteLineAsync("Rolle konnte nicht erstellt werden.");
                    return 1;
                }
            }

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true, // Bootstrap occurs on the server console, not via email.
                DisplayName = displayName,
                LockoutEnabled = true,
            };

            var created = await users.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                await output.WriteLineAsync("Admin konnte nicht angelegt werden:");
                foreach (var error in created.Errors)
                    await output.WriteLineAsync($"- {error.Description}");
                return 1;
            }

            var assigned = await users.AddToRoleAsync(admin, "Admin");
            if (!assigned.Succeeded)
            {
                await output.WriteLineAsync("Adminrolle konnte nicht zugewiesen werden.");
                return 1;
            }

            await transaction.CommitAsync();
            await output.WriteLineAsync("Admin wurde erstellt. Du kannst dich jetzt unter /login anmelden.");
            return 0;
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", gate);
            release.Parameters.AddWithValue("key", BootstrapLockKey);
            await release.ExecuteNonQueryAsync();
        }
    }

    private static string ReadSecret(TextReader input, TextWriter output)
    {
        if (Console.IsInputRedirected || !ReferenceEquals(input, Console.In))
            return input.ReadLine() ?? string.Empty;

        var chars = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                output.WriteLine();
                return chars.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Length > 0) chars.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar) && chars.Length < 1024)
                chars.Append(key.KeyChar);
        }
    }
}
