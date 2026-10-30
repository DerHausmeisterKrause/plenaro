using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Local personal reminders derived from owned task segments. The API has no
/// Microsoft dependency and never creates attendance or break records.
/// Browser polling delivers reminders only while a session is open.
/// </summary>
public static class TaskReminderEndpoints
{
    public sealed record ReminderSettings(bool Enabled, int LeadMinutes);
    public sealed record ReminderReceipt(Guid SegmentId, DateTimeOffset StartsAt, string Kind);
    public sealed record ReminderItem(Guid SegmentId, Guid TaskId, string TaskTitle,
        DateTimeOffset StartsAt, DateTimeOffset DueAt, string Kind);

    public static void MapPlenaroReminders(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/notifications").RequireAuthorization();

        group.MapGet("/settings", async (HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            return Results.Ok(await GetSettingsAsync(db, owner.Value, ct));
        });

        group.MapPut("/settings", async (ReminderSettings input, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await HasCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (input.LeadMinutes is < 0 or > 1440)
                return Results.BadRequest(new { error = "Vorlauf muss zwischen 0 und 1440 Minuten liegen." });

            await using var db = await OpenAsync(config, ct);
            await using var command = new NpgsqlCommand("""
                INSERT INTO user_reminder_settings(owner_user_id,enabled,lead_minutes)
                VALUES (@owner,@enabled,@lead)
                ON CONFLICT (owner_user_id) DO UPDATE
                    SET enabled=EXCLUDED.enabled,
                        lead_minutes=EXCLUDED.lead_minutes,
                        updated_at=now()
                """, db);
            command.Parameters.AddWithValue("owner", owner.Value);
            command.Parameters.AddWithValue("enabled", input.Enabled);
            command.Parameters.AddWithValue("lead", input.LeadMinutes);
            await command.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        // The inbox shows due, unread events. 'lead' exists only before start;
        // 'start' remains visible for 24 hours, even after reloading the page.
        // Receipts prevent repeated reminders after dismissing them.
        group.MapGet("/", async (HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            var settings = await GetSettingsAsync(db, owner.Value, ct);
            if (!settings.Enabled) return Results.Ok(Array.Empty<ReminderItem>());

            var now = DateTimeOffset.UtcNow;
            await using var cmd = new NpgsqlCommand("""
                SELECT s.id, s.task_id, t.title, s.starts_at, rem.due_at, rem.kind
                FROM task_segments s
                JOIN tasks t ON t.id=s.task_id AND t.owner_user_id=s.owner_user_id
                CROSS JOIN LATERAL (VALUES
                    ('lead', s.starts_at - (@lead * interval '1 minute')),
                    ('start', s.starts_at)
                ) AS rem(kind, due_at)
                LEFT JOIN task_reminder_receipts receipt
                  ON receipt.owner_user_id=s.owner_user_id
                 AND receipt.segment_id=s.id
                 AND receipt.starts_at=s.starts_at
                 AND receipt.kind=rem.kind
                WHERE s.owner_user_id=@owner
                  AND t.status IN ('planned','active')
                  AND s.starts_at >= @now - interval '24 hours'
                  AND s.starts_at <= @now + (@lead * interval '1 minute')
                  AND rem.due_at <= @now
                  AND ( (rem.kind='lead' AND @lead>0 AND s.starts_at>@now)
                     OR (rem.kind='start' AND s.starts_at<=@now) )
                  AND receipt.segment_id IS NULL
                ORDER BY rem.due_at DESC, s.id DESC
                LIMIT 50
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("lead", settings.LeadMinutes);
            cmd.Parameters.Add("now", NpgsqlDbType.TimestampTz).Value = now.UtcDateTime;

            var notices = new List<ReminderItem>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                notices.Add(new ReminderItem(reader.GetGuid(0), reader.GetGuid(1),
                    reader.GetString(2),
                    new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                    new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
                    reader.GetString(5)));
            }
            return Results.Ok(notices);
        });

        group.MapPost("/read", async (ReminderReceipt input, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await HasCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (input.SegmentId == Guid.Empty || input.Kind is not ("lead" or "start") ||
                input.StartsAt.Year is < 2000 or > 2100)
                return Results.BadRequest(new { error = "Ungültige Erinnerung." });

            await using var db = await OpenAsync(config, ct);
            // Only the real owner can acknowledge a reminder at its current start time.
            // Idempotence: an already read reminder can be safely submitted again.
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO task_reminder_receipts(owner_user_id,segment_id,starts_at,kind)
                SELECT @owner,s.id,s.starts_at,@kind
                FROM task_segments s
                WHERE s.owner_user_id=@owner AND s.id=@segment AND s.starts_at=@start
                ON CONFLICT DO NOTHING
                RETURNING segment_id
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("segment", input.SegmentId);
            cmd.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = input.StartsAt.UtcDateTime;
            cmd.Parameters.AddWithValue("kind", input.Kind);
            if (await cmd.ExecuteScalarAsync(ct) is not null) return Results.NoContent();

            await using var exists = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM task_segments
                    WHERE owner_user_id=@owner AND id=@segment AND starts_at=@start)
                """, db);
            exists.Parameters.AddWithValue("owner", owner.Value);
            exists.Parameters.AddWithValue("segment", input.SegmentId);
            exists.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = input.StartsAt.UtcDateTime;
            return (bool)(await exists.ExecuteScalarAsync(ct) ?? false)
                ? Results.NoContent() : Results.NotFound();
        });
    }

    private static async Task<ReminderSettings> GetSettingsAsync(
        NpgsqlConnection db, Guid owner, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT enabled, lead_minutes FROM user_reminder_settings
            WHERE owner_user_id=@owner
            """, db);
        cmd.Parameters.AddWithValue("owner", owner);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new ReminderSettings(reader.GetBoolean(0), reader.GetInt16(1))
            : new ReminderSettings(true, 2);
    }

    private static async Task<Guid?> OwnerAsync(HttpContext http,
        UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(http.User);
        return user is null || user.IsDisabled ? null : user.Id;
    }

    private static async Task<NpgsqlConnection> OpenAsync(IConfiguration config, CancellationToken ct)
    {
        var db = new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }

    private static async Task<bool> HasCsrfAsync(HttpContext http, IAntiforgery anti)
    {
        try { await anti.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() => Results.BadRequest(
        new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
