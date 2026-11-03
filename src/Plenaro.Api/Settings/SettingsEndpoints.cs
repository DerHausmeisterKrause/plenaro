using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Settings;

/// <summary>
/// Only allowlisted, non-secret settings are accepted. User IDs always come from
/// the authenticated identity, never from the request body or URL.
/// </summary>
public static class SettingsEndpoints
{
    private const string GlobalKey = "workspace.general";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record WorkspaceSettings(
        string InstanceName = "Plenaro",
        string SupportEmail = "",
        string DefaultWeekStart = "monday");

    public sealed record PersonalSettings(
        string Theme = "light",
        string WeekStart = "inherit");

    public static void MapPlenaroSettings(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/settings").RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal,
            UserManager<ApplicationUser> users, IConfiguration configuration,
            CancellationToken ct) =>
        {
            var account = await users.GetUserAsync(principal);
            if (account is null || account.IsDisabled) return Results.Unauthorized();

            await using var db = new NpgsqlConnection(RequiredConnectionString(configuration));
            await db.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT preferences::text FROM user_preferences WHERE user_id = @uid
                """, db);
            cmd.Parameters.AddWithValue("uid", account.Id);
            var json = await cmd.ExecuteScalarAsync(ct) as string;
            return Results.Ok(json is null
                ? new PersonalSettings()
                : JsonSerializer.Deserialize<PersonalSettings>(json, JsonOptions) ?? new PersonalSettings());
        });

        group.MapPut("/me", async (PersonalSettings settings,
            HttpContext http, IAntiforgery antiforgery, ClaimsPrincipal principal,
            UserManager<ApplicationUser> users, IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            var account = await users.GetUserAsync(principal);
            if (account is null || account.IsDisabled) return Results.Unauthorized();
            if (settings.Theme is not ("light" or "dark") ||
                settings.WeekStart is not ("inherit" or "monday" or "sunday"))
                return Results.BadRequest(new { error = "Ungültige persönliche Einstellung." });

            await using var db = new NpgsqlConnection(RequiredConnectionString(configuration));
            await db.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO user_preferences (user_id, preferences, updated_at)
                VALUES (@uid, @json, now())
                ON CONFLICT (user_id) DO UPDATE
                SET preferences = EXCLUDED.preferences, updated_at = now()
                """, db);
            cmd.Parameters.AddWithValue("uid", account.Id);
            cmd.Parameters.AddWithValue("json", NpgsqlDbType.Jsonb,
                JsonSerializer.Serialize(settings, JsonOptions));
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        // Every authenticated user can read non-secret organizational defaults.
        group.MapGet("/global", async (IConfiguration configuration, CancellationToken ct) =>
        {
            await using var db = new NpgsqlConnection(RequiredConnectionString(configuration));
            await db.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT setting_value::text FROM system_settings WHERE setting_key = @key
                """, db);
            cmd.Parameters.AddWithValue("key", GlobalKey);
            var json = await cmd.ExecuteScalarAsync(ct) as string;
            return Results.Ok(json is null
                ? new WorkspaceSettings()
                : JsonSerializer.Deserialize<WorkspaceSettings>(json, JsonOptions) ?? new WorkspaceSettings());
        });

        group.MapPut("/global", async (WorkspaceSettings settings,
            HttpContext http, IAntiforgery antiforgery, IConfiguration configuration,
            CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (string.IsNullOrWhiteSpace(settings.InstanceName) ||
                settings.InstanceName.Trim().Length is < 2 or > 80 ||
                settings.DefaultWeekStart is not ("monday" or "sunday") ||
                settings.SupportEmail is null || settings.SupportEmail.Length > 254 ||
                (settings.SupportEmail.Length > 0 &&
                 (!MailAddress.TryCreate(settings.SupportEmail, out var mail) ||
                  !string.Equals(mail.Address, settings.SupportEmail, StringComparison.OrdinalIgnoreCase))))
                return Results.BadRequest(new { error = "Ungültiger Instanzname, Kontakt oder Wochenbeginn." });

            var normalized = settings with { InstanceName = settings.InstanceName.Trim() };
            await using var db = new NpgsqlConnection(RequiredConnectionString(configuration));
            await db.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO system_settings (setting_key, setting_value, updated_at)
                VALUES (@key, @json, now())
                ON CONFLICT (setting_key) DO UPDATE
                SET setting_value = EXCLUDED.setting_value, updated_at = now()
                """, db);
            cmd.Parameters.AddWithValue("key", GlobalKey);
            cmd.Parameters.AddWithValue("json", NpgsqlDbType.Jsonb,
                JsonSerializer.Serialize(normalized, JsonOptions));
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }

    private static string RequiredConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Plenaro")
        ?? throw new InvalidOperationException("Plenaro database connection is missing.");

    private static async Task<bool> ValidCsrf(HttpContext context, IAntiforgery tokens)
    {
        try
        {
            await tokens.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult CsrfError() => Results.BadRequest(
        new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
