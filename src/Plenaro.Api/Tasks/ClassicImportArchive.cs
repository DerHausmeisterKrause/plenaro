using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Inert archive of Classic-only fields. Historical ticket bookings are NEVER
/// sent to Znuny or included as new work timer entries.
/// </summary>
public static partial class ClassicImportEndpoints
{
    public sealed record ImportMetadata(Guid TaskLegacyId, string? TicketUrl = null,
        bool IsPinned = false, bool IsZnunyAssigned = true, bool IsPlenaroShared = false,
        string? TicketState = null, string? TicketStateType = null,
        int TicketMinutesBooked = 0, long TicketSecondsBooked = 0,
        DateTimeOffset? TicketCreatedUtc = null, DateTimeOffset? TicketChangedUtc = null,
        DateTimeOffset? LocalActivityUtc = null, string? OutlookEntryId = null);

    public sealed record ImportBooking(string LegacyId, Guid TaskLegacyId,
        string TicketId, string TicketNumber, string BookingId, string ArticleId,
        string Status, long SourceSeconds, double BookedMinutes, DateTimeOffset BookedAtUtc,
        string? ShortDescription, string? CostCenter, string? OrderValue, string? Note);

    public sealed record ImportMonthly(Guid LegacyId, DateOnly Month, long Seconds,
        string? Note, DateTimeOffset CreatedAt);

    // Allowlist only. This is a reference archive, not active web configuration.
    public sealed record ImportLegacySettings(
        bool? ShowWeekendInWeekView = null, bool? HidePastTodayItems = null,
        bool? NotificationSoundEnabled = null, bool? NotifyOnNewAssignedTickets = null,
        bool? NotifyOnTicketChanges = null, int? DefaultSegmentDurationMinutes = null,
        string? CalendarTimeZoneId = null);

    private static readonly JsonSerializerOptions ArchiveJson = new(JsonSerializerDefaults.Web);

    private static string? ValidateArchive(ImportDocument doc, HashSet<Guid> taskIds)
    {
        if (doc.FormatVersion == 1 && ((doc.TaskMetadata?.Length ?? 0) > 0 ||
            (doc.TicketBookings?.Length ?? 0) > 0 || (doc.MonthlyAdjustments?.Length ?? 0) > 0))
            return "Archivdaten erfordern Exportformat 2.";

        var metadata = doc.TaskMetadata ?? [];
        if (metadata.Length > 2000 ||
            metadata.Select(x => x.TaskLegacyId).Distinct().Count() != metadata.Length ||
            metadata.Any(x => !taskIds.Contains(x.TaskLegacyId) ||
                x.TicketUrl?.Length > 2048 || x.TicketState?.Length > 128 ||
                x.TicketStateType?.Length > 128 || x.OutlookEntryId?.Length > 1024 ||
                x.TicketMinutesBooked < 0 || x.TicketSecondsBooked < 0 ||
                !ValidOptionalDate(x.TicketCreatedUtc) ||
                !ValidOptionalDate(x.TicketChangedUtc) ||
                !ValidOptionalDate(x.LocalActivityUtc)))
            return "Ungültige historische Aufgabenmetadaten.";

        var bookings = doc.TicketBookings ?? [];
        if (bookings.Length > 4000 ||
            bookings.Select(x => x.LegacyId).Distinct(StringComparer.Ordinal).Count() != bookings.Length ||
            bookings.Any(x => !taskIds.Contains(x.TaskLegacyId) ||
                !ValidArchiveKey(x.LegacyId) || x.Status is not ("Pending" or "Succeeded" or "Failed") ||
                x.TicketId is null || x.TicketId.Length > 128 ||
                x.TicketNumber is null || x.TicketNumber.Length > 128 ||
                x.BookingId is null || x.BookingId.Length > 128 ||
                x.ArticleId is null || x.ArticleId.Length > 128 ||
                x.ShortDescription?.Length > 500 || x.CostCenter?.Length > 256 ||
                x.OrderValue?.Length > 256 || x.Note?.Length > 500 ||
                x.SourceSeconds is < 0 or > 10000000000L ||
                !double.IsFinite(x.BookedMinutes) || x.BookedMinutes < 0 ||
                x.BookedMinutes > 10000000 || !ValidOptionalDate(x.BookedAtUtc)))
            return "Ungültige historische Ticketbuchung.";

        var months = doc.MonthlyAdjustments ?? [];
        if (months.Length > 4000 ||
            months.Select(x => x.LegacyId).Distinct().Count() != months.Length ||
            months.Any(x => x.LegacyId == Guid.Empty || x.Month.Year is < 2000 or > 2099 ||
                x.Month.Day != 1 || x.Seconds is <= 0 or > 10000000000L ||
                x.Note?.Length > 500 || !ValidOptionalDate(x.CreatedAt)))
            return "Ungültige historische Monatskorrektur.";

        var prefs = doc.LegacySettings;
        if (prefs?.DefaultSegmentDurationMinutes is < 0 or > 1440 ||
            prefs?.CalendarTimeZoneId is { } zone &&
            (zone.Length is < 1 or > 64 || !Regex.IsMatch(zone, "^[a-zA-Z0-9_+/-]+$")))
            return "Ungültige archivierte Classic-Einstellung.";
        return null;
    }

    private static bool ValidArchiveKey(string? key) =>
        key is { Length: >= 1 and <= 80 } && Regex.IsMatch(key, "^[a-zA-Z0-9_-]+$");
    private static bool ValidOptionalDate(DateTimeOffset? date) =>
        date is null || date.Value.Year is >= 2000 and <= 2100;

    private static IEnumerable<ImportKey> ArchiveKeys(ImportDocument doc, Guid owner)
    {
        foreach (var item in doc.TaskMetadata ?? [])
        {
            var key = item.TaskLegacyId.ToString("D");
            yield return new ImportKey("metadata",key,TargetId(owner,doc.SourceId,"task",key));
        }
        foreach (var item in doc.TicketBookings ?? [])
            yield return new ImportKey("booking",item.LegacyId,
                TargetId(owner,doc.SourceId,"booking",item.LegacyId));
        foreach (var item in doc.MonthlyAdjustments ?? [])
        {
            var key = item.LegacyId.ToString("D");
            yield return new ImportKey("monthly",key,TargetId(owner,doc.SourceId,"monthly",key));
        }
    }

    private static async Task<int> ImportArchiveAsync(ImportDocument doc,Guid owner,Guid batch,
        HashSet<(string Kind,string Key)> known,NpgsqlConnection db,
        NpgsqlTransaction tx,CancellationToken ct)
    {
        var inserted = 0;
        foreach (var item in doc.TaskMetadata ?? [])
        {
            var key = item.TaskLegacyId.ToString("D");
            if (known.Contains(("metadata",key))) continue;
            var taskId = TargetId(owner,doc.SourceId,"task",key);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO classic_task_metadata(owner_user_id,task_id,legacy_data)
                VALUES(@owner,@task,@json)
                """,db,tx);
            cmd.Parameters.AddWithValue("owner",owner);
            cmd.Parameters.AddWithValue("task",taskId);
            cmd.Parameters.Add("json",NpgsqlDbType.Jsonb).Value =
                JsonSerializer.Serialize(item,ArchiveJson);
            await cmd.ExecuteNonQueryAsync(ct);
            await TrackAsync(db,tx,owner,doc.SourceId,"metadata",key,taskId,batch,null,ct);
            inserted++;
        }
        foreach (var item in doc.TicketBookings ?? [])
        {
            if (known.Contains(("booking",item.LegacyId))) continue;
            var id = TargetId(owner,doc.SourceId,"booking",item.LegacyId);
            var taskId = TargetId(owner,doc.SourceId,"task",item.TaskLegacyId.ToString("D"));
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO classic_ticket_bookings(id,owner_user_id,task_id,legacy_data)
                VALUES(@id,@owner,@task,@json)
                """,db,tx);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("owner",owner);
            cmd.Parameters.AddWithValue("task",taskId);
            cmd.Parameters.Add("json",NpgsqlDbType.Jsonb).Value =
                JsonSerializer.Serialize(item,ArchiveJson);
            await cmd.ExecuteNonQueryAsync(ct);
            await TrackAsync(db,tx,owner,doc.SourceId,"booking",item.LegacyId,id,batch,null,ct);
            inserted++;
        }
        foreach (var item in doc.MonthlyAdjustments ?? [])
        {
            var key = item.LegacyId.ToString("D");
            if (known.Contains(("monthly",key))) continue;
            var id = TargetId(owner,doc.SourceId,"monthly",key);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO classic_monthly_adjustments(id,owner_user_id,month,seconds,note,created_at)
                VALUES(@id,@owner,@month,@seconds,@note,@created)
                """,db,tx);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("owner",owner);
            cmd.Parameters.Add("month",NpgsqlDbType.Date).Value=item.Month;
            cmd.Parameters.AddWithValue("seconds",item.Seconds);
            cmd.Parameters.AddWithValue("note",item.Note ?? "");
            cmd.Parameters.Add("created",NpgsqlDbType.TimestampTz).Value=item.CreatedAt.UtcDateTime;
            await cmd.ExecuteNonQueryAsync(ct);
            await TrackAsync(db,tx,owner,doc.SourceId,"monthly",key,id,batch,null,ct);
            inserted++;
        }
        return inserted;
    }

    private static async Task<bool> HasUntrackedArchiveChildAsync(
        NpgsqlConnection db,NpgsqlTransaction tx,Guid owner,Guid batch,Guid taskId,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM classic_task_metadata m WHERE m.task_id=@task AND m.owner_user_id=@owner
                  AND NOT EXISTS (SELECT 1 FROM classic_import_items i
                    WHERE i.batch_id=@batch AND i.item_kind='metadata' AND i.target_id=m.task_id)
            ) OR EXISTS (
                SELECT 1 FROM classic_ticket_bookings b WHERE b.task_id=@task AND b.owner_user_id=@owner
                  AND NOT EXISTS (SELECT 1 FROM classic_import_items i
                    WHERE i.batch_id=@batch AND i.item_kind='booking' AND i.target_id=b.id)
            )
            """,db,tx);
        cmd.Parameters.AddWithValue("owner",owner);
        cmd.Parameters.AddWithValue("batch",batch);
        cmd.Parameters.AddWithValue("task",taskId);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static void MapClassicArchiveRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/archive/overview",async (HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            var owner=await OwnerAsync(http,users);
            if(owner is null)return Results.Unauthorized();
            await using var db=await OpenAsync(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT
                  (SELECT count(*) FROM classic_task_metadata WHERE owner_user_id=@owner),
                  (SELECT count(*) FROM classic_ticket_bookings WHERE owner_user_id=@owner),
                  (SELECT count(*) FROM classic_monthly_adjustments WHERE owner_user_id=@owner),
                  (SELECT count(*) FROM classic_import_batches
                    WHERE owner_user_id=@owner AND legacy_settings <> '{}'::jsonb)
                """,db);
            cmd.Parameters.AddWithValue("owner",owner.Value);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            return Results.Ok(new {
                metadataCount=reader.GetInt64(0),bookingCount=reader.GetInt64(1),
                monthlyCount=reader.GetInt64(2),settingsBatches=reader.GetInt64(3)
            });
        });

        group.MapGet("/archive/tasks/{id:guid}",async (Guid id,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            var owner=await OwnerAsync(http,users);
            if(owner is null)return Results.Unauthorized();
            await using var db=await OpenAsync(config,ct);
            await using (var check=new NpgsqlCommand("""
                SELECT EXISTS (SELECT 1 FROM tasks WHERE id=@task AND owner_user_id=@owner)
                """,db))
            {
                check.Parameters.AddWithValue("task",id);
                check.Parameters.AddWithValue("owner",owner.Value);
                if(await check.ExecuteScalarAsync(ct) is not true)return Results.NotFound();
            }
            string? metadata=null;
            await using (var query=new NpgsqlCommand("""
                SELECT legacy_data::text FROM classic_task_metadata
                WHERE owner_user_id=@owner AND task_id=@task
                """,db))
            {
                query.Parameters.AddWithValue("task",id);
                query.Parameters.AddWithValue("owner",owner.Value);
                metadata=await query.ExecuteScalarAsync(ct) as string;
            }
            var bookings=new List<JsonElement>();
            await using(var query=new NpgsqlCommand("""
                SELECT legacy_data::text FROM classic_ticket_bookings
                WHERE owner_user_id=@owner AND task_id=@task
                ORDER BY created_at DESC LIMIT 100
                """,db))
            {
                query.Parameters.AddWithValue("task",id);
                query.Parameters.AddWithValue("owner",owner.Value);
                await using var reader=await query.ExecuteReaderAsync(ct);
                while(await reader.ReadAsync(ct))
                    bookings.Add(JsonSerializer.Deserialize<JsonElement>(reader.GetString(0)));
            }
            return Results.Ok(new {
                metadata=metadata is null?(JsonElement?)null:JsonSerializer.Deserialize<JsonElement>(metadata),
                ticketBookings=bookings
            });
        });

        group.MapGet("/archive/settings/{batchId:guid}",async (Guid batchId,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            var owner=await OwnerAsync(http,users);
            if(owner is null)return Results.Unauthorized();
            await using var db=await OpenAsync(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT legacy_settings::text FROM classic_import_batches
                WHERE owner_user_id=@owner AND id=@batch
                """,db);
            cmd.Parameters.AddWithValue("owner",owner.Value);
            cmd.Parameters.AddWithValue("batch",batchId);
            var value=await cmd.ExecuteScalarAsync(ct) as string;
            return value is null ? Results.NotFound()
                : Results.Ok(JsonSerializer.Deserialize<JsonElement>(value));
        });
    }
}
