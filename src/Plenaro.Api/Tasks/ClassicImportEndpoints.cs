using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Browser accepts only the allowlisted, locally converted Classic export JSON.
/// No SQLite files or desktop secrets are uploaded. Preview and commit validate
/// the same DTO; every commit is fully transactional and owner-isolated.
/// </summary>
public static partial class ClassicImportEndpoints
{
    public sealed record ImportTask(Guid LegacyId, string Title, string? Description,
        string Status, int? Priority, string[]? Tags, DateOnly? PlannedFor, DateTimeOffset CreatedAt);
    public sealed record ImportSegment(string LegacyId, Guid TaskLegacyId,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Note, string[]? Participants);
    public sealed record ImportTime(string LegacyId, Guid TaskLegacyId,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Note);
    public sealed record ImportMarker(DateOnly Day, string DayType, bool IsBr, bool IsHo);
    public sealed record ImportSettings(int? ReminderLeadMinutes);
    public sealed record ImportDocument(int FormatVersion, string SourceId, int SchemaVersion,
        ImportTask[]? Tasks, ImportSegment[]? Segments, ImportTime[]? TimeEntries,
        ImportMarker[]? DayMarkers, ImportSettings? Settings, string[]? Warnings,
        ImportMetadata[]? TaskMetadata = null, ImportBooking[]? TicketBookings = null,
        ImportMonthly[]? MonthlyAdjustments = null, ImportLegacySettings? LegacySettings = null);

    private sealed record ImportKey(string Kind, string Key, Guid TargetId);
    private sealed record ImportSnapshot(string Kind, string Key, Guid TargetId, string Hash);
    private sealed record StoredBatch(Guid Id, string SourceId, DateTimeOffset CreatedAt,
        int RowCount, int? ImportedLeadMinutes);

    public static void MapPlenaroClassicImport(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/classic-import").RequireAuthorization();

        group.MapPost("/preview", async (ImportDocument doc, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (Validate(doc) is { } problem) return Results.BadRequest(new { error = problem });
            await using var db = await OpenAsync(config, ct);
            var existing = await RegisteredAsync(db, owner.Value, doc.SourceId, null, ct);
            var keys = Keys(doc, owner.Value).Concat(ArchiveKeys(doc, owner.Value)).ToArray();
            var newMarkers = 0;
            foreach (var marker in doc.DayMarkers ?? [])
            {
                if (existing.Contains(("marker", marker.Day.ToString("yyyy-MM-dd")))) continue;
                if (!await HasMarkerAsync(db, owner.Value, marker.Day, null, ct)) newMarkers++;
            }
            return Results.Ok(new {
                taskCount = doc.Tasks?.Length ?? 0,
                segmentCount = doc.Segments?.Length ?? 0,
                timeCount = doc.TimeEntries?.Length ?? 0,
                markerCount = doc.DayMarkers?.Length ?? 0,
                metadataCount = doc.TaskMetadata?.Length ?? 0,
                bookingCount = doc.TicketBookings?.Length ?? 0,
                monthlyCount = doc.MonthlyAdjustments?.Length ?? 0,
                newTasks = keys.Count(k => k.Kind == "task" && !existing.Contains((k.Kind,k.Key))),
                newSegments = keys.Count(k => k.Kind == "segment" && !existing.Contains((k.Kind,k.Key))),
                newTimeEntries = keys.Count(k => k.Kind == "time" && !existing.Contains((k.Kind,k.Key))),
                newMarkers,
                newMetadata = keys.Count(k => k.Kind == "metadata" && !existing.Contains((k.Kind,k.Key))),
                newBookings = keys.Count(k => k.Kind == "booking" && !existing.Contains((k.Kind,k.Key))),
                newMonthly = keys.Count(k => k.Kind == "monthly" && !existing.Contains((k.Kind,k.Key))),
                legacySettings = doc.LegacySettings,
                skippedExistingOrDuplicateMarkers = (doc.DayMarkers?.Length ?? 0) - newMarkers,
                reminderLeadMinutes = doc.Settings?.ReminderLeadMinutes,
                warnings = doc.Warnings ?? [],
                note = "Vorschau ist unverbindlich. Beim Import werden Konflikte erneut geprüft; vorhandene Daten bleiben erhalten."
            });
        });

        group.MapPost("/commit", async (ImportDocument doc, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (Validate(doc) is { } problem) return Results.BadRequest(new { error = problem });
            await using var db = await OpenAsync(config, ct);
            await using var tx = await db.BeginTransactionAsync(ct);
            await SegmentAvailability.LockOwnerAsync(db, tx, owner.Value, ct);
            var registered = await RegisteredAsync(db, owner.Value, doc.SourceId, tx, ct);
            var batchId = Guid.NewGuid();
            int? previousLead = null;
            var hadSettings = false;
            int? leadToApply = doc.Settings?.ReminderLeadMinutes;
            if (doc.Settings?.ReminderLeadMinutes is not null)
            {
                await using var previous = new NpgsqlCommand("""
                    SELECT lead_minutes FROM user_reminder_settings WHERE owner_user_id=@owner
                    """, db, tx);
                previous.Parameters.AddWithValue("owner", owner.Value);
                var prior = await previous.ExecuteScalarAsync(ct);
                hadSettings = prior is not null;
                previousLead = prior is null ? null : Convert.ToInt32(prior);
                // Re-importing the same Classic settings must not change an
                // existing timestamp and break undo of an earlier batch.
                if (hadSettings && previousLead == leadToApply)
                    leadToApply = null;
            }
            await using (var batch = new NpgsqlCommand("""
                INSERT INTO classic_import_batches(id,owner_user_id,source_id,previous_lead,
                    changed_lead,settings_had_row,legacy_settings)
                VALUES(@id,@owner,@source,@oldlead,@newlead,@had,@legacy)
                """, db, tx))
            {
                batch.Parameters.AddWithValue("id", batchId);
                batch.Parameters.AddWithValue("owner", owner.Value);
                batch.Parameters.AddWithValue("source", doc.SourceId);
                batch.Parameters.Add("oldlead", NpgsqlDbType.Smallint).Value =
                    previousLead is null ? DBNull.Value : (object)(short)previousLead.Value;
                batch.Parameters.Add("newlead", NpgsqlDbType.Smallint).Value =
                    leadToApply is not int lead ? DBNull.Value : (object)(short)lead;
                batch.Parameters.AddWithValue("had", hadSettings);
                batch.Parameters.Add("legacy", NpgsqlDbType.Jsonb).Value =
                    JsonSerializer.Serialize(doc.LegacySettings ?? new ImportLegacySettings(),
                        new JsonSerializerOptions(ArchiveJson) {
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        });
                await batch.ExecuteNonQueryAsync(ct);
            }

            var inserted = 0;
            try
            {
                // Tasks are imported first; all child references use deterministic,
                // per-owner keys, never a user-provided PostgreSQL owner ID.
                foreach (var task in doc.Tasks ?? [])
                {
                    var key = task.LegacyId.ToString("D");
                    if (registered.Contains(("task",key))) continue;
                    var id = TargetId(owner.Value, doc.SourceId, "task", key);
                    await using var cmd = new NpgsqlCommand("""
                        INSERT INTO tasks(id,owner_user_id,title,description,status,priority,
                            tags,planned_for,created_at,updated_at,completed_at)
                        VALUES(@id,@owner,@title,@description,@status,@priority,
                            @tags,@planned,@created,now(),
                            CASE WHEN @status='completed' THEN @created ELSE NULL END)
                        """, db, tx);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.Parameters.AddWithValue("owner", owner.Value);
                    cmd.Parameters.AddWithValue("title", task.Title.Trim());
                    cmd.Parameters.AddWithValue("description", task.Description ?? "");
                    cmd.Parameters.AddWithValue("status", task.Status);
                    cmd.Parameters.Add("priority", NpgsqlDbType.Smallint).Value =
                        task.Priority is int p ? (object)(short)p : DBNull.Value;
                    cmd.Parameters.Add("tags", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = task.Tags ?? [];
                    cmd.Parameters.Add("planned", NpgsqlDbType.Date).Value =
                        task.PlannedFor is DateOnly d ? d : DBNull.Value;
                    cmd.Parameters.Add("created", NpgsqlDbType.TimestampTz).Value = task.CreatedAt.UtcDateTime;
                    await cmd.ExecuteNonQueryAsync(ct);
                    await TrackAsync(db, tx, owner.Value, doc.SourceId, "task", key, id, batchId, null, ct);
                    inserted++;
                }

                foreach (var segment in doc.Segments ?? [])
                {
                    if (registered.Contains(("segment",segment.LegacyId))) continue;
                    var parent = TargetId(owner.Value, doc.SourceId, "task", segment.TaskLegacyId.ToString("D"));
                    var collision = await SegmentAvailability.ReadAsync(db, owner.Value,
                        segment.StartsAt, segment.EndsAt, null, tx, ct);
                    if (!collision.Available)
                        throw new InvalidOperationException(
                            "Ein altes Zeitsegment überschneidet sich mit einer bereits vorhandenen Planung. Import abgebrochen; bitte zuerst den Konflikt auflösen.");
                    var id = TargetId(owner.Value, doc.SourceId, "segment", segment.LegacyId);
                    await using var cmd = new NpgsqlCommand("""
                        INSERT INTO task_segments(id,task_id,owner_user_id,starts_at,ends_at,
                            note,participants)
                        VALUES(@id,@task,@owner,@start,@end,@note,@participants)
                        """, db, tx);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.Parameters.AddWithValue("task", parent);
                    cmd.Parameters.AddWithValue("owner", owner.Value);
                    cmd.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = segment.StartsAt.UtcDateTime;
                    cmd.Parameters.Add("end", NpgsqlDbType.TimestampTz).Value = segment.EndsAt.UtcDateTime;
                    cmd.Parameters.AddWithValue("note", segment.Note ?? "");
                    cmd.Parameters.Add("participants", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = segment.Participants ?? [];
                    await cmd.ExecuteNonQueryAsync(ct);
                    await TrackAsync(db, tx, owner.Value, doc.SourceId, "segment", segment.LegacyId, id, batchId, null, ct);
                    inserted++;
                }
                foreach (var time in doc.TimeEntries ?? [])
                {
                    if (registered.Contains(("time",time.LegacyId))) continue;
                    var parent = TargetId(owner.Value, doc.SourceId, "task", time.TaskLegacyId.ToString("D"));
                    var id = TargetId(owner.Value, doc.SourceId, "time", time.LegacyId);
                    await using var cmd = new NpgsqlCommand("""
                        INSERT INTO time_entries(id,owner_user_id,task_id,started_at,ended_at,
                            entry_type,note)
                        VALUES(@id,@owner,@task,@start,@end,'work',@note)
                        """, db, tx);
                    cmd.Parameters.AddWithValue("id", id);
                    cmd.Parameters.AddWithValue("owner", owner.Value);
                    cmd.Parameters.AddWithValue("task", parent);
                    cmd.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = time.StartsAt.UtcDateTime;
                    cmd.Parameters.Add("end", NpgsqlDbType.TimestampTz).Value = time.EndsAt.UtcDateTime;
                    cmd.Parameters.AddWithValue("note", time.Note ?? "");
                    await cmd.ExecuteNonQueryAsync(ct);
                    await TrackAsync(db, tx, owner.Value, doc.SourceId, "time", time.LegacyId, id, batchId, null, ct);
                    inserted++;
                }
                foreach (var marker in doc.DayMarkers ?? [])
                {
                    var key = marker.Day.ToString("yyyy-MM-dd");
                    if (registered.Contains(("marker",key)) ||
                        await HasMarkerAsync(db, owner.Value, marker.Day, tx, ct)) continue;
                    await using var cmd = new NpgsqlCommand("""
                        INSERT INTO day_markers(user_id,day,day_type,is_br,is_ho)
                        VALUES(@owner,@day,@type,@br,@ho)
                        """, db, tx);
                    cmd.Parameters.AddWithValue("owner", owner.Value);
                    cmd.Parameters.Add("day", NpgsqlDbType.Date).Value = marker.Day;
                    cmd.Parameters.AddWithValue("type", marker.DayType);
                    cmd.Parameters.AddWithValue("br", marker.IsBr);
                    cmd.Parameters.AddWithValue("ho", marker.IsHo);
                    await cmd.ExecuteNonQueryAsync(ct);
                    await TrackAsync(db, tx, owner.Value, doc.SourceId, "marker", key,
                        TargetId(owner.Value,doc.SourceId,"marker",key), batchId, marker.Day, ct);
                    inserted++;
                }

                inserted += await ImportArchiveAsync(doc, owner.Value, batchId,
                    registered, db, tx, ct);

                if (leadToApply is int newLead)
                {
                    await using var settings = new NpgsqlCommand("""
                        INSERT INTO user_reminder_settings(owner_user_id,enabled,lead_minutes)
                        VALUES(@owner,true,@lead)
                        ON CONFLICT(owner_user_id) DO UPDATE
                        SET lead_minutes=EXCLUDED.lead_minutes,updated_at=now()
                        """, db, tx);
                    settings.Parameters.AddWithValue("owner", owner.Value);
                    settings.Parameters.AddWithValue("lead", newLead);
                    await settings.ExecuteNonQueryAsync(ct);
                    await using var timestamp = new NpgsqlCommand("""
                        UPDATE classic_import_batches
                        SET settings_imported_at=(
                            SELECT updated_at FROM user_reminder_settings WHERE owner_user_id=@owner)
                        WHERE id=@batch
                        """, db, tx);
                    timestamp.Parameters.AddWithValue("owner",owner.Value);
                    timestamp.Parameters.AddWithValue("batch",batchId);
                    await timestamp.ExecuteNonQueryAsync(ct);
                }
                await tx.CommitAsync(ct);
                return Results.Ok(new { batchId, inserted, skipped = 
                    (doc.Tasks?.Length ?? 0)+(doc.Segments?.Length ?? 0)+
                    (doc.TimeEntries?.Length ?? 0)+(doc.DayMarkers?.Length ?? 0)+
                    (doc.TaskMetadata?.Length ?? 0)+(doc.TicketBookings?.Length ?? 0)+
                    (doc.MonthlyAdjustments?.Length ?? 0)-inserted,
                    reminderLeadMinutes = doc.Settings?.ReminderLeadMinutes });
            }
            catch (Exception ex) when (ex is PostgresException or InvalidOperationException)
            {
                // All writes are inside one DB transaction. Disposing it rolls back.
                return Results.Conflict(new { error = ex is InvalidOperationException
                    ? ex.Message
                    : "Importkonflikt in der Datenbank. Keine Daten wurden übernommen." });
            }
        });

        MapClassicArchiveRoutes(group);
        group.MapGet("/batches", async (HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT b.id,b.source_id,b.created_at,count(i.item_kind)::integer,b.changed_lead
                FROM classic_import_batches b LEFT JOIN classic_import_items i ON i.batch_id=b.id
                WHERE b.owner_user_id=@owner
                GROUP BY b.id ORDER BY b.created_at DESC LIMIT 30
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            var batches = new List<StoredBatch>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                batches.Add(new StoredBatch(reader.GetGuid(0),reader.GetString(1),
                    new DateTimeOffset(reader.GetDateTime(2),TimeSpan.Zero),
                    reader.GetInt32(3),reader.IsDBNull(4)?null:reader.GetInt16(4)));
            return Results.Ok(batches);
        });

        // Safe undo refuses changed rows, non-imported child records, and newer
        // changes to the imported reminder setting. No silent cascade of user work.
        group.MapDelete("/batches/{id:guid}", async (Guid id, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var tx = await db.BeginTransactionAsync(ct);
            await SegmentAvailability.LockOwnerAsync(db, tx, owner.Value, ct);
            await using var batch = new NpgsqlCommand("""
                SELECT previous_lead,changed_lead,settings_had_row,settings_imported_at
                FROM classic_import_batches WHERE id=@id AND owner_user_id=@owner
                """, db, tx);
            batch.Parameters.AddWithValue("id", id);
            batch.Parameters.AddWithValue("owner", owner.Value);
            int? previous = null, changed = null; bool had = false;
            DateTime? settingsWrittenAt = null;
            await using (var reader = await batch.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct)) return Results.NotFound();
                previous = reader.IsDBNull(0) ? null : reader.GetInt16(0);
                changed = reader.IsDBNull(1) ? null : reader.GetInt16(1);
                had = reader.GetBoolean(2);
                settingsWrittenAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            }
            if (changed is not null)
            {
                await using var current = new NpgsqlCommand("""
                    SELECT lead_minutes,updated_at FROM user_reminder_settings WHERE owner_user_id=@owner
                    """, db, tx);
                current.Parameters.AddWithValue("owner", owner.Value);
                await using var settingsRow = await current.ExecuteReaderAsync(ct);
                if (!await settingsRow.ReadAsync(ct) ||
                    settingsWrittenAt is null || settingsRow.GetDateTime(1) != settingsWrittenAt.Value ||
                    settingsRow.GetInt16(0) != changed.Value)
                    return Results.Conflict(new { error = "Reminder-Einstellung wurde inzwischen verändert. Import kann nicht sicher rückgängig gemacht werden." });
            }
            var items = new List<ImportSnapshot>();
            await using (var read = new NpgsqlCommand("""
                SELECT item_kind,source_key,target_id,snapshot_hash FROM classic_import_items
                WHERE batch_id=@batch AND owner_user_id=@owner
                """, db, tx))
            {
                read.Parameters.AddWithValue("batch", id);
                read.Parameters.AddWithValue("owner", owner.Value);
                await using var reader = await read.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    items.Add(new ImportSnapshot(reader.GetString(0),reader.GetString(1),
                        reader.GetGuid(2),reader.GetString(3)));
            }
            foreach (var item in items)
            {
                var currentHash = await FingerprintAsync(db, tx, owner.Value,
                    item.Kind, item.TargetId,
                    item.Kind == "marker" ? DateOnly.ParseExact(item.Key,"yyyy-MM-dd") : null, ct);
                if (currentHash != item.Hash)
                    return Results.Conflict(new { error = "Importierte Daten wurden nachträglich bearbeitet oder gelöscht. Rollback verweigert." });
            }
            foreach (var task in items.Where(x => x.Kind == "task"))
            {
                await using var dependent = new NpgsqlCommand("""
                    SELECT
                      EXISTS(SELECT 1 FROM task_segments s WHERE s.task_id=@task
                        AND NOT EXISTS(SELECT 1 FROM classic_import_items i WHERE
                            i.batch_id=@batch AND i.owner_user_id=@owner
                            AND i.item_kind='segment' AND i.target_id=s.id))
                      OR EXISTS(SELECT 1 FROM time_entries e WHERE e.task_id=@task
                        AND NOT EXISTS(SELECT 1 FROM classic_import_items i WHERE
                            i.batch_id=@batch AND i.owner_user_id=@owner
                            AND i.item_kind='time' AND i.target_id=e.id))
                      OR EXISTS(SELECT 1 FROM task_time_adjustments WHERE task_id=@task)
                    """, db, tx);
                dependent.Parameters.AddWithValue("task", task.TargetId);
                dependent.Parameters.AddWithValue("batch", id);
                dependent.Parameters.AddWithValue("owner", owner.Value);
                if ((bool)(await dependent.ExecuteScalarAsync(ct) ?? false) ||
                    await HasUntrackedArchiveChildAsync(db,tx,owner.Value,id,task.TargetId,ct))
                    return Results.Conflict(new { error = "Importierte Aufgabe besitzt neuere oder fremde Zeit-/Archivdaten. Rollback verweigert." });
            }
            foreach (var kind in new[] {"time","segment","booking","metadata","monthly","marker","task"})
            foreach (var item in items.Where(x => x.Kind == kind))
            {
                var table = kind switch {
                    "task" => "tasks", "segment" => "task_segments", "time" => "time_entries",
                    "booking" => "classic_ticket_bookings", "metadata" => "classic_task_metadata",
                    "monthly" => "classic_monthly_adjustments", _ => "day_markers"
                };
                var ownerField = kind == "marker" ? "user_id" : "owner_user_id";
                var idField = kind == "marker" ? "day" : kind == "metadata" ? "task_id" : "id";
                await using var delete = new NpgsqlCommand(
                    $"DELETE FROM {table} WHERE {ownerField}=@owner AND {idField}=@target", db, tx);
                delete.Parameters.AddWithValue("owner", owner.Value);
                if (kind == "marker") delete.Parameters.Add("target", NpgsqlDbType.Date).Value =
                    DateOnly.ParseExact(item.Key,"yyyy-MM-dd");
                else delete.Parameters.AddWithValue("target", item.TargetId);
                await delete.ExecuteNonQueryAsync(ct);
            }
            if (changed is not null)
            {
                await using var restore = new NpgsqlCommand(had
                    ? "UPDATE user_reminder_settings SET lead_minutes=@lead WHERE owner_user_id=@owner"
                    : "DELETE FROM user_reminder_settings WHERE owner_user_id=@owner", db, tx);
                restore.Parameters.AddWithValue("owner", owner.Value);
                if (had) restore.Parameters.AddWithValue("lead",previous ?? 2);
                await restore.ExecuteNonQueryAsync(ct);
            }
            await using (var deleteBatch = new NpgsqlCommand("""
                DELETE FROM classic_import_batches WHERE id=@id AND owner_user_id=@owner
                """,db,tx))
            {
                deleteBatch.Parameters.AddWithValue("id", id);
                deleteBatch.Parameters.AddWithValue("owner", owner.Value);
                await deleteBatch.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });
    }

    private static string? Validate(ImportDocument doc)
    {
        if (doc.FormatVersion is not (1 or 2) || doc.SchemaVersion is < 1 or > 32 ||
            string.IsNullOrWhiteSpace(doc.SourceId) ||
            !Regex.IsMatch(doc.SourceId, "^[a-zA-Z0-9_-]{6,64}$") ||
            (doc.Tasks?.Length ?? 0) is > 2000 or < 0 ||
            (doc.Segments?.Length ?? 0) > 4000 ||
            (doc.TimeEntries?.Length ?? 0) > 4000 ||
            (doc.DayMarkers?.Length ?? 0) > 4000)
            return "Ungültiges Exportformat, Quelle oder zu viele Datensätze.";
        var tasks = doc.Tasks ?? [];
        var taskIds = tasks.Select(x => x.LegacyId).ToHashSet();
        if (taskIds.Count != tasks.Length || tasks.Any(x =>
            x.LegacyId == Guid.Empty || string.IsNullOrWhiteSpace(x.Title) ||
            x.Title.Length > 255 || x.Description?.Length > 10000 ||
            x.Status is not ("planned" or "active" or "completed" or "cancelled") ||
            x.Priority is < 1 or > 5 ||
            x.CreatedAt.Year is < 2000 or > 2100 ||
            x.PlannedFor is { Year: < 2000 or > 2099 } ||
            (x.Tags?.Length ?? 0) > 10 ||
            (x.Tags ?? []).Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 32)))
            return "Ungültige Aufgabe im Export.";
        var segments = doc.Segments ?? [];
        if (segments.Any(x => !taskIds.Contains(x.TaskLegacyId) ||
            !ValidId(x.LegacyId) || !ValidPeriod(x.StartsAt,x.EndsAt,24) ||
            x.Note?.Length > 1000 || (x.Participants?.Length ?? 0) > 20 ||
            (x.Participants ?? []).Any(email => email.Length is < 3 or > 254 ||
                !System.Net.Mail.MailAddress.TryCreate(email, out _))) ||
            segments.Select(x => x.LegacyId).Distinct().Count() != segments.Length)
            return "Ungültiges Zeitsegment im Export.";
        var times = doc.TimeEntries ?? [];
        if (times.Any(x => !taskIds.Contains(x.TaskLegacyId) ||
            !ValidId(x.LegacyId) || !ValidPeriod(x.StartsAt,x.EndsAt,24*7) ||
            x.Note?.Length > 500) ||
            times.Select(x => x.LegacyId).Distinct().Count() != times.Length)
            return "Ungültige Zeitbuchung im Export.";
        var markers = doc.DayMarkers ?? [];
        if (markers.Any(x => x.Day.Year is < 2000 or > 2099 ||
            x.DayType is not ("Normal" or "UL" or "AM") ||
            (x.DayType == "Normal" && !x.IsBr && !x.IsHo)) ||
            markers.Select(x => x.Day).Distinct().Count() != markers.Length)
            return "Ungültiger Tagesmarker im Export.";
        if (doc.Settings?.ReminderLeadMinutes is < 0 or > 1440)
            return "Ungültiger Erinnerungsvorlauf.";
        if ((doc.Warnings?.Length ?? 0) > 500 ||
            (doc.Warnings ?? []).Any(x => x.Length > 500))
            return "Zu viele oder zu lange Importhinweise.";
        return ValidateArchive(doc, taskIds);
    }

    private static bool ValidId(string? value) =>
        long.TryParse(value, out var id) && id > 0;
    private static bool ValidPeriod(DateTimeOffset a, DateTimeOffset b, int maxHours) =>
        a.Year is >= 2000 and <= 2100 && b.Year is >= 2000 and <= 2100 &&
        b > a && b-a <= TimeSpan.FromHours(maxHours);

    private static IEnumerable<ImportKey> Keys(ImportDocument doc, Guid owner)
    {
        foreach (var x in doc.Tasks ?? [])
            yield return new ImportKey("task",x.LegacyId.ToString("D"),
                TargetId(owner,doc.SourceId,"task",x.LegacyId.ToString("D")));
        foreach (var x in doc.Segments ?? [])
            yield return new ImportKey("segment",x.LegacyId,
                TargetId(owner,doc.SourceId,"segment",x.LegacyId));
        foreach (var x in doc.TimeEntries ?? [])
            yield return new ImportKey("time",x.LegacyId,
                TargetId(owner,doc.SourceId,"time",x.LegacyId));
    }
    private static Guid TargetId(Guid owner, string source, string kind, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            owner.ToString("D")+":"+source+":"+kind+":"+key));
        return new Guid(hash.AsSpan(0,16));
    }

    private static async Task<HashSet<(string Kind,string Key)>> RegisteredAsync(
        NpgsqlConnection db,Guid owner,string source,NpgsqlTransaction? tx,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT item_kind,source_key FROM classic_import_items
            WHERE owner_user_id=@owner AND source_id=@source
            """,db,tx);
        cmd.Parameters.AddWithValue("owner",owner);
        cmd.Parameters.AddWithValue("source",source);
        var keys = new HashSet<(string,string)>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) keys.Add((reader.GetString(0),reader.GetString(1)));
        return keys;
    }
    private static async Task<bool> HasMarkerAsync(NpgsqlConnection db,Guid owner,
        DateOnly day,NpgsqlTransaction? tx,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM day_markers WHERE user_id=@owner AND day=@day)",db,tx);
        cmd.Parameters.AddWithValue("owner",owner);
        cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task TrackAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid owner,string source,string kind,string key,Guid id,Guid batch,DateOnly? day,CancellationToken ct)
    {
        var fingerprint = await FingerprintAsync(db,tx,owner,kind,id,day,ct)
            ?? throw new InvalidOperationException("Importiertes Objekt nicht lesbar.");
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO classic_import_items
                (owner_user_id,source_id,item_kind,source_key,target_id,batch_id,snapshot_hash)
            VALUES(@owner,@source,@kind,@key,@target,@batch,@hash)
            """,db,tx);
        cmd.Parameters.AddWithValue("owner",owner);
        cmd.Parameters.AddWithValue("source",source);
        cmd.Parameters.AddWithValue("kind",kind);
        cmd.Parameters.AddWithValue("key",key);
        cmd.Parameters.AddWithValue("target",id);
        cmd.Parameters.AddWithValue("batch",batch);
        cmd.Parameters.AddWithValue("hash",fingerprint);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<string?> FingerprintAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid owner,string kind,Guid id,DateOnly? day,CancellationToken ct)
    {
        var table = kind switch {
            "task"=>"tasks", "segment"=>"task_segments", "time"=>"time_entries",
            "marker"=>"day_markers", "metadata"=>"classic_task_metadata",
            "booking"=>"classic_ticket_bookings", "monthly"=>"classic_monthly_adjustments",
            _=>throw new ArgumentException("Invalid kind")
        };
        var ownerField = kind=="marker" ? "user_id" : "owner_user_id";
        var idField = kind=="marker" ? "day" : kind=="metadata" ? "task_id" : "id";
        await using var cmd = new NpgsqlCommand(
            $"SELECT md5(row_to_json(t)::text) FROM {table} t WHERE {ownerField}=@owner AND {idField}=@target",
            db,tx);
        cmd.Parameters.AddWithValue("owner",owner);
        if (kind=="marker") cmd.Parameters.Add("target",NpgsqlDbType.Date).Value = day!.Value;
        else cmd.Parameters.AddWithValue("target",id);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }
    private static async Task<Guid?> OwnerAsync(HttpContext http,UserManager<ApplicationUser> users)
    {
        var user=await users.GetUserAsync(http.User);
        return user is null || user.IsDisabled ? null : user.Id;
    }
    private static async Task<NpgsqlConnection> OpenAsync(IConfiguration cfg,CancellationToken ct)
    {
        var db=new NpgsqlConnection(cfg.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);return db;
    }
    private static async Task<bool> ValidCsrf(HttpContext http,IAntiforgery anti)
    {
        try { await anti.ValidateRequestAsync(http);return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult CsrfError() =>
        Results.BadRequest(new {error="Sicherheitstoken fehlt oder ist ungültig."});
}
