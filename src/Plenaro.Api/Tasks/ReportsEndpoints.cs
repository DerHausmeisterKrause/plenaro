using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;
using Plenaro.Api.Znuny;
using Plenaro.Api.Secrets;

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
        IReadOnlyList<TaskReportRow> Tasks,
        long AccountedSeconds,long RemoteSeconds,long LocalFallbackSeconds,
        int RemoteEntries,int LocalFallbackEntries,
        string AccountingStatus,string AccountingMessage,
        IReadOnlyList<ReportAccounting.TicketRow> AccountedTickets);

    public static void MapPlenaroReports(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/reports").RequireAuthorization();

        group.MapGet("/month", async (string? month, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config,
            SecretVault vault,ZnunyReader reader,CancellationToken ct) =>
        {
            if (!TryMonth(month, out var firstDay))
                return Results.BadRequest(new { error = "Monat im Format JJJJ-MM (2000–2100) angeben." });
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var report = await ReadMonthAsync(config, user.Id, firstDay, vault, reader, ct);
            return report is null
                ? Results.Problem("Der Monat enthält zu viele Aufgaben für einen einzelnen Export.",
                    statusCode: StatusCodes.Status413PayloadTooLarge)
                : Results.Ok(report);
        });

        group.MapGet("/month.csv", async (string? month, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config,
            SecretVault vault,ZnunyReader reader,CancellationToken ct) =>
        {
            if (!TryMonth(month, out var firstDay))
                return Results.BadRequest(new { error = "Monat im Format JJJJ-MM (2000–2100) angeben." });
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var report = await ReadMonthAsync(config, user.Id, firstDay, vault, reader, ct);
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
        DateTime monthStart, SecretVault vault,ZnunyReader znuny,CancellationToken ct)
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

        var accounted=await ReadAccountingAsync(db,owner,start,end,vault,znuny,ct);
        return new MonthReport(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ZoneId, asOf, rows.Sum(r => r.TimerSeconds),
            rows.Sum(r => r.AdjustmentMinutes), rows.Sum(r => r.TotalSeconds),
            rows.Sum(r => r.SessionCount), rows.Count, rows,
            accounted.Data.TotalSeconds,accounted.Data.RemoteSeconds,
            accounted.Data.LocalOnlySeconds,accounted.Data.RemoteEntries,
            accounted.Data.LocalOnlyEntries,
            accounted.Status,accounted.Message,accounted.Data.Tickets);
    }

    private sealed record AccountingLoad(ReportAccounting.MergeResult Data,
        string Status,string Message);

    private static async Task<AccountingLoad> ReadAccountingAsync(
        NpgsqlConnection db,Guid owner,DateTime start,DateTime end,
        SecretVault vault,ZnunyReader znuny,CancellationToken ct)
    {
        var local=new List<ReportAccounting.LocalBooking>();
        // Plenaro's journal has only one authoritative local record per
        // outgoing booking; pending and explicitly rejected writes are not
        // treated as successful bookings. Use booked_minutes, not source_seconds.
        await using(var cmd=new NpgsqlCommand("""
            SELECT COALESCE(NULLIF(t.ticket_number,''),b.ticket_id),
                   COALESCE(t.title,''), b.booked_minutes
            FROM znuny_ticket_time_bookings b
            JOIN znuny_cached_tickets t ON t.ticket_id=b.ticket_id
            WHERE b.user_id=@user AND b.status='confirmed'
              AND COALESCE(b.confirmed_at,b.created_at)>=@from
              AND COALESCE(b.confirmed_at,b.created_at)<@to
            ORDER BY b.created_at,b.id
            LIMIT @limit
            """,db)){
            cmd.Parameters.AddWithValue("user",owner);
            cmd.Parameters.Add("from",NpgsqlDbType.TimestampTz).Value=start;
            cmd.Parameters.Add("to",NpgsqlDbType.TimestampTz).Value=end;
            cmd.Parameters.AddWithValue("limit",MaxReportRows+1);
            await using var cursor=await cmd.ExecuteReaderAsync(ct);
            while(await cursor.ReadAsync(ct)){
                if(local.Count>=MaxReportRows)
                    throw new InvalidOperationException(
                        "Zu viele bestätigte Znuny-Zeitbuchungen für einen Monatsreport.");
                local.Add(new ReportAccounting.LocalBooking(cursor.GetString(0),
                    cursor.GetString(1),checked((long)cursor.GetInt32(2)*60)));
            }
        }
        AccountingLoad Partial(string status,string message)=>
            new(ReportAccounting.Reconcile([],local),status,message);
        if(!vault.IsConfigured)
            return Partial("unavailable",
                "Secret-Vault fehlt. Znuny-Zeiten konnten nicht vollständig geprüft werden.");
        string login="";
        await using(var cmd=new NpgsqlCommand("""
            SELECT COALESCE(p.username,'')
            FROM znuny_approved_agent_bindings a
            LEFT JOIN znuny_user_settings p ON p.user_id=a.user_id
            WHERE a.user_id=@user
            """,db)){
            cmd.Parameters.AddWithValue("user",owner);
            login=(await cmd.ExecuteScalarAsync(ct) as string??"").Trim();
        }
        if(string.IsNullOrWhiteSpace(login))
            return Partial("not-connected",
                "Keine freigegebene persönliche Znuny-Agentenanmeldung. Nur lokal bestätigte Buchungen sichtbar.");
        var password=await vault.GetForIntegrationAsync("user",owner,
            "znuny.password",ct);
        if(string.IsNullOrEmpty(password))
            return Partial("not-connected",
                "Persönliches Znuny-Passwort fehlt. Nur lokal bestätigte Buchungen sichtbar.");
        var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
        if(ZnunyEndpoints.ValidateServer(server) is not null ||
           new Uri(server.ApiUrl).Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
            return Partial("not-configured",
                "Znuny-API ist nicht eingerichtet. Bitte die Serveradresse prüfen.");

        try{
            // Read *all* confirmed entries attributed to this authenticated
            // Znuny agent. The official TimeAccountingGet operation requires
            // rw membership in its dedicated timeaccounting_webservice group.
            var remote=await znuny.ReadAccountedTimeAsync(server,login,password,
                new DateTimeOffset(start,TimeSpan.Zero),
                new DateTimeOffset(end,TimeSpan.Zero),
                new ZnunyReader.Counter(2),ct);
            var combined=ReportAccounting.Reconcile(remote,local);
            return new AccountingLoad(combined,"ok",
                combined.LocalOnlyEntries>0
                    ? "OTRS-Zeiten geladen. Zusätzliche lokal bestätigte Buchungen wurden ohne doppelte Zuordnung ergänzt."
                    : "Alle OTRS-Zeitbuchungen dieses Benutzers geladen und mit Plenaro abgeglichen.");
        }catch(ZnunyReader.RemoteFailure e) when(!ct.IsCancellationRequested){
            var detail=e.Code switch{
                "route-not-found" or "time-accounting-config" or "time-accounting-unsupported"=>
                    "TimeAccountingGet ist im Znuny-Webservice noch nicht freigegeben. Admin → Web Services → TimeAccountingREST prüfen.",
                "authentication" or "session-rejected" or "remote-rejected"=>
                    "Znuny verweigert den TimeAccountingGet-Abruf. Persönliche Agentenanmeldung und Gruppenzugriff timeaccounting_webservice (rw) prüfen.",
                "time-accounting-limit" or "response-too-large"=>
                    "Znuny lieferte mehr Buchungen als sicher verarbeitet werden können. Der Report ist unvollständig.",
                _=>"Znuny-Zeitbuchungen konnten nicht vollständig geladen werden ("+e.Code+")."
            };
            return Partial("unavailable",detail+
                " Nur lokal bestätigte Buchungen berücksichtigt.");
        }
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
        csv.AppendLine();
        csv.Append("OTRS-Abgleich;").Append(CsvCell(report.AccountingStatus))
            .Append(';').Append(CsvCell(report.AccountingMessage)).AppendLine();
        csv.AppendLine("OTRS-Ticket;Titel;OTRS-Sekunden;Plenaro-nur-lokal-Sekunden;Gebucht gesamt Sekunden");
        foreach(var entry in report.AccountedTickets)
            csv.Append(CsvCell(entry.TicketNumber)).Append(';')
                .Append(CsvCell(entry.Title)).Append(';')
                .Append(entry.RemoteSeconds).Append(';')
                .Append(entry.LocalOnlySeconds).Append(';')
                .Append(entry.TotalSeconds).AppendLine();
        csv.Append("SUMME GEBUCHT;;").Append(report.RemoteSeconds).Append(';')
            .Append(report.LocalFallbackSeconds).Append(';')
            .Append(report.AccountedSeconds).AppendLine();

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
