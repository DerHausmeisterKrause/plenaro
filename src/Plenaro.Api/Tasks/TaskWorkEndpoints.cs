using System.Globalization;
using System.Net.Mail;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Task-local timer and planning segments. Never models attendance, breaks or
/// employee working time. Every read/write is scoped to the local Identity owner.
/// </summary>
public static class TaskWorkEndpoints
{
    public sealed record AdjustmentInput(int DeltaMinutes, string? Note);
    public sealed record SegmentInput(DateTimeOffset StartsAt, DateTimeOffset EndsAt,
        string? Note, string[]? Participants);
    public sealed record SegmentRescheduleInput(DateTimeOffset StartsAt, DateTimeOffset EndsAt,
        DateTimeOffset ExpectedStartsAt, DateTimeOffset ExpectedEndsAt);
    public sealed record SegmentView(Guid Id, Guid TaskId, string TaskTitle,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Note, string[] Participants);
    public sealed record TimeSummary(long BookedSeconds, long RunningSeconds,
        DateTimeOffset? RunningSince, long AdjustedMinutes, int SessionCount);

    public static void MapPlenaroTaskWork(this WebApplication app)
    {
        var tasks = app.MapGroup("/api/v1/tasks/{taskId:guid}").RequireAuthorization();
        var segments = app.MapGroup("/api/v1/segments").RequireAuthorization();

        tasks.MapGet("/time", async (Guid taskId, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            if (!await TaskExistsAsync(db, taskId, owner.Value, ct)) return Results.NotFound();
            return Results.Ok(await ReadTimeAsync(db, taskId, owner.Value, ct));
        });

        tasks.MapPost("/time/start", async (Guid taskId, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO time_entries(id,owner_user_id,task_id,started_at,entry_type)
                SELECT @id,@owner,@task,clock_timestamp(),'work'
                FROM tasks WHERE id=@task AND owner_user_id=@owner
                  AND status IN ('planned','active')
                RETURNING id
                """, db);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("task", taskId);
            try
            {
                var result = await cmd.ExecuteScalarAsync(ct);
                if (result is null)
                    return await TaskExistsAsync(db, taskId, owner.Value, ct)
                        ? Results.Conflict(new { error = "Abgeschlossene Aufgaben können keinen Timer starten." })
                        : Results.NotFound();
                return Results.Ok(await ReadTimeAsync(db, taskId, owner.Value, ct));
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return Results.Conflict(new { error = "Ein Timer läuft bereits. Bitte zuerst stoppen." });
            }
        });

        tasks.MapPost("/time/stop", async (Guid taskId, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            if (!await TaskExistsAsync(db, taskId, owner.Value, ct)) return Results.NotFound();
            await using var cmd = new NpgsqlCommand("""
                UPDATE time_entries SET ended_at=GREATEST(clock_timestamp(),started_at+interval '1 second')
                WHERE owner_user_id=@owner AND task_id=@task AND ended_at IS NULL AND entry_type='work'
                RETURNING id
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("task", taskId);
            if (await cmd.ExecuteScalarAsync(ct) is null)
                return Results.Conflict(new { error = "Für diese Aufgabe läuft kein Timer." });
            return Results.Ok(await ReadTimeAsync(db, taskId, owner.Value, ct));
        });

        tasks.MapPost("/time/adjust", async (Guid taskId, AdjustmentInput input, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (input.DeltaMinutes == 0 || input.DeltaMinutes is < -1440 or > 1440 ||
                input.Note?.Length > 500)
                return Results.BadRequest(new { error = "Korrektur muss zwischen -1440 und +1440 Minuten liegen." });
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO task_time_adjustments(id,owner_user_id,task_id,delta_minutes,note)
                SELECT @id,@owner,@task,@minutes,@note FROM tasks
                WHERE id=@task AND owner_user_id=@owner
                RETURNING id
                """, db);
            cmd.Parameters.AddWithValue("id", Guid.NewGuid());
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("task", taskId);
            cmd.Parameters.AddWithValue("minutes", input.DeltaMinutes);
            cmd.Parameters.AddWithValue("note", input.Note?.Trim() ?? "");
            if (await cmd.ExecuteScalarAsync(ct) is null) return Results.NotFound();
            return Results.Ok(await ReadTimeAsync(db, taskId, owner.Value, ct));
        });

        tasks.MapGet("/segments", async (Guid taskId, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            if (!await TaskExistsAsync(db, taskId, owner.Value, ct)) return Results.NotFound();
            await using var cmd = new NpgsqlCommand("""
                SELECT s.id,s.task_id,t.title,s.starts_at,s.ends_at,s.note,s.participants
                FROM task_segments s JOIN tasks t ON t.id=s.task_id AND t.owner_user_id=s.owner_user_id
                WHERE s.owner_user_id=@owner AND s.task_id=@task
                ORDER BY s.starts_at LIMIT 200
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("task", taskId);
            return Results.Ok(await ReadSegmentsAsync(cmd, ct));
        });

        segments.MapGet("/", async (DateTimeOffset start, DateTimeOffset end, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            if (start.Year < 2000 || end.Year > 2100 || end <= start ||
                end - start > TimeSpan.FromDays(45))
                return Results.BadRequest(new { error = "Zeitraum maximal 45 Tage." });
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT s.id,s.task_id,t.title,s.starts_at,s.ends_at,s.note,s.participants
                FROM task_segments s JOIN tasks t ON t.id=s.task_id AND t.owner_user_id=s.owner_user_id
                WHERE s.owner_user_id=@owner AND s.ends_at>@start AND s.starts_at<@end
                ORDER BY s.starts_at LIMIT 200
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("start", NpgsqlDbType.TimestampTz).Value = start.UtcDateTime;
            cmd.Parameters.AddWithValue("end", NpgsqlDbType.TimestampTz).Value = end.UtcDateTime;
            return Results.Ok(await ReadSegmentsAsync(cmd, ct));
        });

        segments.MapGet("/availability", async (DateTimeOffset start, DateTimeOffset end,
            Guid? excludeId, HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (ValidatePeriod(start, end) is { } error)
                return Results.BadRequest(new { error });
            await using var db = await OpenAsync(config, ct);
            if (excludeId.HasValue && !await SegmentExistsAsync(db, excludeId.Value, owner.Value, null, ct))
                return Results.NotFound();
            return Results.Ok(await SegmentAvailability.ReadAsync(
                db, owner.Value, start, end, excludeId, null, ct));
        });

        tasks.MapPost("/segments", async (Guid taskId, SegmentInput input, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            var error = ValidateSegment(input);
            if (error is not null) return Results.BadRequest(new { error });
            await using var db = await OpenAsync(config, ct);
            await using var tx = await db.BeginTransactionAsync(ct);
            await SegmentAvailability.LockOwnerAsync(db, tx, owner.Value, ct);
            if (!await TaskExistsAsync(db, taskId, owner.Value, ct, tx))
                return Results.NotFound();
            var check = await SegmentAvailability.ReadAsync(db, owner.Value,
                input.StartsAt, input.EndsAt, null, tx, ct);
            if (!check.Available) return SlotConflict(check);
            var id = Guid.NewGuid();
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO task_segments(id,task_id,owner_user_id,starts_at,ends_at,note,participants)
                SELECT @id,@task,@owner,@start,@end,@note,@participants
                FROM tasks WHERE id=@task AND owner_user_id=@owner
                RETURNING id
                """, db, tx);
            BindSegment(cmd, input, owner.Value, taskId, id);
            if (await cmd.ExecuteScalarAsync(ct) is null) return Results.NotFound();
            await tx.CommitAsync(ct);
            return Results.Created($"/api/v1/tasks/{taskId}/segments", new { id });
        });

        tasks.MapPut("/segments/{segmentId:guid}", async (Guid taskId, Guid segmentId,
            SegmentInput input, HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            var error = ValidateSegment(input);
            if (error is not null) return Results.BadRequest(new { error });
            await using var db = await OpenAsync(config, ct);
            await using var tx = await db.BeginTransactionAsync(ct);
            await SegmentAvailability.LockOwnerAsync(db, tx, owner.Value, ct);
            if (!await SegmentExistsAsync(db, segmentId, owner.Value, tx, ct, taskId))
                return Results.NotFound();
            var check = await SegmentAvailability.ReadAsync(db, owner.Value,
                input.StartsAt, input.EndsAt, segmentId, tx, ct);
            if (!check.Available) return SlotConflict(check);
            await using var cmd = new NpgsqlCommand("""
                UPDATE task_segments SET starts_at=@start,ends_at=@end,
                    note=@note,participants=@participants
                WHERE id=@id AND owner_user_id=@owner AND task_id=@task
                RETURNING id
                """, db, tx);
            BindSegment(cmd, input, owner.Value, taskId, segmentId);
            if (await cmd.ExecuteScalarAsync(ct) is null) return Results.NotFound();
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });


        // The update checks both optimistic version and local availability under
        // the same owner-specific transaction lock as creation and full edits.
        tasks.MapPatch("/segments/{segmentId:guid}/schedule", async (Guid taskId, Guid segmentId,
            SegmentRescheduleInput input, HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (ValidatePeriod(input.StartsAt, input.EndsAt) is { } error)
                return Results.BadRequest(new { error });
            if (ValidatePeriod(input.ExpectedStartsAt, input.ExpectedEndsAt) is not null)
                return Results.BadRequest(new { error = "Ursprünglicher Zeitraum ist ungültig." });
            await using var db = await OpenAsync(config, ct);
            await using var tx = await db.BeginTransactionAsync(ct);
            await SegmentAvailability.LockOwnerAsync(db, tx, owner.Value, ct);
            if (!await SegmentExistsAsync(db, segmentId, owner.Value, tx, ct, taskId))
                return Results.NotFound();
            await using (var expected = new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM task_segments
                    WHERE id=@id AND owner_user_id=@owner AND task_id=@task
                    AND starts_at=@old_start AND ends_at=@old_end)
                """, db, tx))
            {
                expected.Parameters.AddWithValue("id", segmentId);
                expected.Parameters.AddWithValue("owner", owner.Value);
                expected.Parameters.AddWithValue("task", taskId);
                expected.Parameters.Add("old_start", NpgsqlDbType.TimestampTz).Value = input.ExpectedStartsAt.UtcDateTime;
                expected.Parameters.Add("old_end", NpgsqlDbType.TimestampTz).Value = input.ExpectedEndsAt.UtcDateTime;
                if (!(bool)(await expected.ExecuteScalarAsync(ct) ?? false))
                    return Results.Conflict(new { error = "Das Zeitsegment wurde zwischenzeitlich verändert. Bitte Kalender aktualisieren." });
            }
            var check = await SegmentAvailability.ReadAsync(db, owner.Value,
                input.StartsAt, input.EndsAt, segmentId, tx, ct);
            if (!check.Available) return SlotConflict(check);
            await using var cmd = new NpgsqlCommand("""
                UPDATE task_segments SET starts_at=@start, ends_at=@end
                WHERE id=@id AND task_id=@task AND owner_user_id=@owner
                  AND starts_at=@expected_start AND ends_at=@expected_end
                RETURNING id
                """, db, tx);
            cmd.Parameters.AddWithValue("id", segmentId);
            cmd.Parameters.AddWithValue("task", taskId);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = input.StartsAt.UtcDateTime;
            cmd.Parameters.Add("end", NpgsqlDbType.TimestampTz).Value = input.EndsAt.UtcDateTime;
            cmd.Parameters.Add("expected_start", NpgsqlDbType.TimestampTz).Value = input.ExpectedStartsAt.UtcDateTime;
            cmd.Parameters.Add("expected_end", NpgsqlDbType.TimestampTz).Value = input.ExpectedEndsAt.UtcDateTime;
            if (await cmd.ExecuteScalarAsync(ct) is null)
                return Results.Conflict(new { error = "Segmentänderung konnte nicht übernommen werden." });
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        tasks.MapDelete("/segments/{segmentId:guid}", async (Guid taskId, Guid segmentId,
            HttpContext http, UserManager<ApplicationUser> users, IConfiguration config,
            IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                DELETE FROM task_segments WHERE id=@id AND owner_user_id=@owner AND task_id=@task
                """, db);
            cmd.Parameters.AddWithValue("id", segmentId);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("task", taskId);
            return await cmd.ExecuteNonQueryAsync(ct) == 0 ? Results.NotFound() : Results.NoContent();
        });
    }

    private static async Task<TimeSummary> ReadTimeAsync(NpgsqlConnection db, Guid taskId,
        Guid owner, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT
                COALESCE(SUM(EXTRACT(EPOCH FROM (ended_at-started_at)))
                    FILTER (WHERE ended_at IS NOT NULL),0)::bigint,
                MAX(started_at) FILTER (WHERE ended_at IS NULL),
                COUNT(*) FILTER (WHERE ended_at IS NOT NULL)::integer
            FROM time_entries WHERE owner_user_id=@owner AND task_id=@task AND entry_type='work'
            """, db);
        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.AddWithValue("task", taskId);
        long sessionSeconds;
        DateTimeOffset? running;
        int count;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            sessionSeconds = reader.GetInt64(0);
            running = reader.IsDBNull(1) ? null : new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero);
            count = reader.GetInt32(2);
        }
        await using var correction = new NpgsqlCommand("""
            SELECT COALESCE(SUM(delta_minutes),0)::bigint
            FROM task_time_adjustments WHERE owner_user_id=@owner AND task_id=@task
            """, db);
        correction.Parameters.AddWithValue("owner", owner);
        correction.Parameters.AddWithValue("task", taskId);
        var adjustedMinutes = (long)(await correction.ExecuteScalarAsync(ct) ?? 0L);
        var activeSeconds = running.HasValue
            ? Math.Max(0, (long)(DateTimeOffset.UtcNow - running.Value).TotalSeconds) : 0L;
        return new TimeSummary(Math.Max(0, sessionSeconds + adjustedMinutes * 60L),
            activeSeconds, running, adjustedMinutes, count);
    }

    private static async Task<List<SegmentView>> ReadSegmentsAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var result = new List<SegmentView>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new SegmentView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero),
                reader.GetString(5), reader.GetFieldValue<string[]>(6)));
        return result;
    }

    private static string? ValidatePeriod(DateTimeOffset start, DateTimeOffset end)
    {
        if (start.Year < 2000 || end.Year > 2100 || end <= start ||
            end - start > TimeSpan.FromHours(24))
            return "Start und Ende müssen innerhalb von maximal 24 Stunden liegen.";
        return null;
    }

    private static string? ValidateSegment(SegmentInput input)
    {
        if (ValidatePeriod(input.StartsAt, input.EndsAt) is { } periodError)
            return periodError;
        if (input.Note?.Length > 1000) return "Kurzbeschreibung darf maximal 1000 Zeichen haben.";
        var addresses = input.Participants ?? [];
        if (addresses.Length > 20 || addresses.Any(x => x.Length is < 3 or > 254))
            return "Maximal 20 gültige Teilnehmeradressen sind erlaubt.";
        foreach (var address in addresses)
        {
            try
            {
                var parsed = new MailAddress(address);
                if (!string.Equals(parsed.Address, address, StringComparison.OrdinalIgnoreCase))
                    return "Ungültige Teilnehmeradresse.";
            }
            catch (FormatException) { return "Ungültige Teilnehmeradresse."; }
        }
        return null;
    }

    private static void BindSegment(NpgsqlCommand cmd, SegmentInput input, Guid owner,
        Guid taskId, Guid id)
    {
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.AddWithValue("task", taskId);
        cmd.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = input.StartsAt.UtcDateTime;
        cmd.Parameters.Add("end", NpgsqlDbType.TimestampTz).Value = input.EndsAt.UtcDateTime;
        cmd.Parameters.AddWithValue("note", input.Note?.Trim() ?? "");
        cmd.Parameters.Add("participants", NpgsqlDbType.Array | NpgsqlDbType.Text)
            .Value = input.Participants ?? [];
    }

    private static IResult SlotConflict(SegmentAvailability.Result check) =>
        Results.Conflict(new {
            error = "Dieser Zeitraum überschneidet sich mit einem bestehenden eigenen Zeitsegment.",
            conflicts = check.Conflicts
        });

    private static async Task<bool> SegmentExistsAsync(NpgsqlConnection db, Guid id, Guid owner,
        NpgsqlTransaction? transaction, CancellationToken ct, Guid? taskId = null)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM task_segments
            WHERE id=@id AND owner_user_id=@owner AND (@task IS NULL OR task_id=@task))
            """, db, transaction);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.Add("task", NpgsqlDbType.Uuid).Value =
            taskId.HasValue ? taskId.Value : DBNull.Value;
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<bool> TaskExistsAsync(NpgsqlConnection db, Guid task, Guid owner,
        CancellationToken ct, NpgsqlTransaction? transaction = null)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM tasks WHERE id=@task AND owner_user_id=@owner)", db, transaction);
        cmd.Parameters.AddWithValue("task", task);
        cmd.Parameters.AddWithValue("owner", owner);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<Guid?> OwnerAsync(HttpContext http, UserManager<ApplicationUser> users)
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

    private static async Task<bool> ValidCsrfAsync(HttpContext http, IAntiforgery anti)
    {
        try { await anti.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() =>
        Results.BadRequest(new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
