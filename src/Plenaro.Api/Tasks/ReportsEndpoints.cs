using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Per-user task activity. This is not attendance or employee work-time reporting.
/// Month boundaries are Europe/Berlin calendar midnights (including DST changes).
/// </summary>
public static class ReportsEndpoints
{
    private const int MaxReportRows = 5000;
    private const string ZoneId = "Europe/Berlin";

    public sealed record TaskReportRow(Guid TaskId, string Title, string Status,
        long TimerSeconds, long AdjustmentMinutes, long TotalSeconds, int SessionCount);

    public sealed record MonthReport(string Month, string TimeZone,
        DateTimeOffset GeneratedAt, long TimerSeconds, long AdjustmentMinutes,
        long TotalSeconds, int SessionCount, int TaskCount,
        IReadOnlyList<TaskReportRow> Tasks);

    public static void MapPlenaroReports(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/reports").RequireAuthorization();

        group.MapGet("/month", async (string? month, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            if (!TryMonth(month, out var firstDay))
                return Results.BadRequest(new { error = "Monat im Format JJJJ-MM (2000–2100) angeben." });
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var report = await ReadMonthAsync(config, user.Id, firstDay, ct);
            return report is null
                ? Results.Problem("Der Monat enthält zu viele Aufgaben für einen einzelnen Export.",
                    statusCode: StatusCodes.Status413PayloadTooLarge)
                : Results.Ok(report);
        });

        group.MapGet("/month.csv", async (string? month, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            if (!TryMonth(month, out var firstDay))
                return Results.BadRequest(new { error = "Monat im Format JJJJ-MM (2000–2100) angeben." });
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var report = await ReadMonthAsync(config, user.Id, firstDay, ct);
            if (report is null)
                return Results.Problem("Der Monat enthält zu viele Aufgaben für einen einzelnen Export.",
                    statusCode: StatusCodes.Status413PayloadTooLarge);

            return Results.File(ToCsv(report), "text/csv; charset=utf-8",
                $"plenaro-aufgaben-{report.Month}.csv");
        });
    }

    private static bool TryMonth(string? value, out DateTime first)
    {
        first = default;
        if (value is null || !Regex.IsMatch(value, @"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.CultureInvariant))
            return false;
        if (!DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out first)) return false;
        return first.Year is >= 2000 and <= 2100;
    }

    private static async Task<MonthReport?> ReadMonthAsync(IConfiguration config, Guid owner,
        DateTime monthStart, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);
        var endLocal = monthStart.AddMonths(1);
        var start = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(monthStart, DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), zone);
        var asOf = DateTimeOffset.UtcNow;

        await using var db = new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);

        // Aggregate time sessions and manual adjustments independently. Joining
        // raw rows to each other would multiply the booked time.
        // Sessions crossing the boundary are clipped to [start, end), and an
        // active timer contributes only up to this request's asOf timestamp.
        await using var cmd = new NpgsqlCommand("""
            WITH sessions AS (
                SELECT task_id,
                    SUM(EXTRACT(EPOCH FROM
                        (LEAST(COALESCE(ended_at, @asof), @period_end)
                          - GREATEST(started_at, @period_start)))) AS seconds,
                    COUNT(*)::integer AS count
                FROM time_entries
                WHERE owner_user_id=@owner AND task_id IS NOT NULL AND entry_type='work'
                  AND started_at < LEAST(@period_end, @asof)
                  AND COALESCE(ended_at, @asof) > @period_start
                GROUP BY task_id
            ), adjustments AS (
                SELECT task_id, SUM(delta_minutes)::bigint AS minutes
                FROM task_time_adjustments
                WHERE owner_user_id=@owner
                  AND created_at >= @period_start AND created_at < @period_end
                GROUP BY task_id
            )
            SELECT t.id, t.title, t.status,
                   COALESCE(s.seconds, 0)::numeric,
                   COALESCE(s.count, 0)::integer,
                   COALESCE(a.minutes, 0)::bigint
            FROM tasks t
            LEFT JOIN sessions s ON s.task_id=t.id
            LEFT JOIN adjustments a ON a.task_id=t.id
            WHERE t.owner_user_id=@owner
              AND (s.task_id IS NOT NULL OR a.task_id IS NOT NULL)
            ORDER BY t.title, t.id
            LIMIT @limit
            """, db);
        cmd.Parameters.AddWithValue("owner", owner);
        cmd.Parameters.Add("period_start", NpgsqlDbType.TimestampTz).Value = start;
        cmd.Parameters.Add("period_end", NpgsqlDbType.TimestampTz).Value = end;
        cmd.Parameters.Add("asof", NpgsqlDbType.TimestampTz).Value = asOf.UtcDateTime;
        cmd.Parameters.AddWithValue("limit", MaxReportRows + 1);

        var rows = new List<TaskReportRow>();
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (rows.Count >= MaxReportRows) return null;
                var seconds = Math.Max(0, (long)Math.Round(reader.GetDecimal(3), MidpointRounding.AwayFromZero));
                var minutes = reader.GetInt64(5);
                rows.Add(new TaskReportRow(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                    seconds, minutes, Math.Max(0, seconds + minutes * 60), reader.GetInt32(4)));
            }
        }

        return new MonthReport(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ZoneId, asOf, rows.Sum(r => r.TimerSeconds),
            rows.Sum(r => r.AdjustmentMinutes), rows.Sum(r => r.TotalSeconds),
            rows.Sum(r => r.SessionCount), rows.Count, rows);
    }

    private static byte[] ToCsv(MonthReport report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("Aufgabe;Status;Timer-Sekunden;Korrektur-Minuten;Gesamt-Sekunden;Sitzungen");
        foreach (var task in report.Tasks)
        {
            csv.Append(CsvCell(task.Title)).Append(';')
                .Append(CsvCell(task.Status)).Append(';')
                .Append(task.TimerSeconds).Append(';')
                .Append(task.AdjustmentMinutes).Append(';')
                .Append(task.TotalSeconds).Append(';')
                .Append(task.SessionCount).AppendLine();
        }
        csv.Append("SUMME;;").Append(report.TimerSeconds).Append(';')
            .Append(report.AdjustmentMinutes).Append(';')
            .Append(report.TotalSeconds).Append(';')
            .Append(report.SessionCount).AppendLine();

        // Excel recognizes the UTF-8 BOM. User-authored task titles must never
        // become spreadsheet formulas (=, +, -, @), even after leading whitespace.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static string CsvCell(string source)
    {
        var suspicious = source.TrimStart(' ', '\t', '\r', '\n');
        if (suspicious.Length > 0 && "=+-@".Contains(suspicious[0]))
            source = "'" + source;
        return "\"" + source.Replace("\"", "\"\"") + "\"";
    }
}
