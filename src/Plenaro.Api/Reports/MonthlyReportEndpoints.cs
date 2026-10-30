using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Reports;

public static class MonthlyReportEndpoints
{
    public sealed record ReportDay(Guid TaskId,string TaskTitle,DateOnly Date,long TrackedSeconds,long AdjustmentMinutes);
    public sealed record MonthlyReport(int Year,int Month,string TimeZone,long TrackedSeconds,
        long AdjustmentMinutes,long NetSeconds,int CompletedTasks,int PlannedSegments,
        long PlannedSegmentSeconds,int UlDays,int AmDays,int BrDays,int HoDays,
        IReadOnlyList<ReportDay> Days);

    public static void MapPlenaroReports(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/reports").RequireAuthorization();
        group.MapGet("/monthly",async (HttpContext http,int year,int month,string? timeZone,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            if (!Validate(year,month,timeZone,out var zone)) return Results.BadRequest(new {error="Ungültiger Monat oder Zeitzone."});
            var user=await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            return Results.Ok(await Build(user.Id,year,month,zone!,config,ct));
        });
        group.MapGet("/monthly.csv",async (HttpContext http,int year,int month,string? timeZone,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            if (!Validate(year,month,timeZone,out var zone)) return Results.BadRequest(new {error="Ungültiger Monat oder Zeitzone."});
            var user=await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            var report=await Build(user.Id,year,month,zone!,config,ct);
            http.Response.Headers.CacheControl="no-store";
            return Results.File(Encoding.UTF8.GetBytes("\uFEFF"+Csv(report)),
                "text/csv; charset=utf-8",$"plenaro-report-{year:D4}-{month:D2}.csv");
        });
    }
    private static bool Validate(int y,int m,string? name,out TimeZoneInfo? zone)
    {
        zone=null;
        if(y is < 2000 or > 2099 || m is < 1 or > 12 || string.IsNullOrWhiteSpace(name) || name.Length>96) return false;
        try { zone=TimeZoneInfo.FindSystemTimeZoneById(name);return true; }
        catch(TimeZoneNotFoundException){return false;}
        catch(InvalidTimeZoneException){return false;}
    }
    private static async Task<MonthlyReport> Build(Guid owner,int year,int month,TimeZoneInfo zone,
        IConfiguration config,CancellationToken ct)
    {
        var first=new DateOnly(year,month,1);
        var next=first.AddMonths(1);
        var from=TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue),zone);
        var to=TimeZoneInfo.ConvertTimeToUtc(next.ToDateTime(TimeOnly.MinValue),zone);
        await using var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        await using var work=new NpgsqlCommand("""
            WITH bounds AS (
                SELECT d::date AS local_day,
                    d::timestamp AT TIME ZONE @zone AS day_start,
                    (d::date+1)::timestamp AT TIME ZONE @zone AS day_end
                FROM generate_series(@first::date,(@next::date-1),interval '1 day') AS d
            ), activity AS (
                SELECT t.id AS task_id,t.title,b.local_day,
                    SUM(EXTRACT(EPOCH FROM LEAST(e.ended_at,b.day_end) -
                        GREATEST(e.started_at,b.day_start)))::bigint AS seconds,
                    0::bigint AS minutes
                FROM bounds b
                JOIN time_entries e ON e.owner_user_id=@owner AND e.task_id IS NOT NULL
                    AND e.entry_type='work' AND e.ended_at IS NOT NULL
                    AND e.ended_at>b.day_start AND e.started_at<b.day_end
                JOIN tasks t ON t.id=e.task_id AND t.owner_user_id=e.owner_user_id
                GROUP BY t.id,t.title,b.local_day
                UNION ALL
                SELECT t.id,t.title,(a.created_at AT TIME ZONE @zone)::date,
                    0::bigint,SUM(a.delta_minutes)::bigint
                FROM task_time_adjustments a
                JOIN tasks t ON t.id=a.task_id AND t.owner_user_id=a.owner_user_id
                WHERE a.owner_user_id=@owner AND a.created_at>=@from AND a.created_at<@to
                GROUP BY t.id,t.title,(a.created_at AT TIME ZONE @zone)::date
            )
            SELECT task_id,title,local_day,SUM(seconds)::bigint,SUM(minutes)::bigint
            FROM activity GROUP BY task_id,title,local_day ORDER BY local_day,title,task_id
            """,db);
        Bind(work,owner,first,next,from,to,zone.Id);
        var days=new List<ReportDay>();
        await using(var reader=await work.ExecuteReaderAsync(ct))
        {
            while(await reader.ReadAsync(ct))
                days.Add(new ReportDay(reader.GetGuid(0),reader.GetString(1),
                    reader.GetFieldValue<DateOnly>(2),reader.GetInt64(3),reader.GetInt64(4)));
        }
        await using var stats=new NpgsqlCommand("""
            SELECT
                (SELECT count(*)::integer FROM tasks WHERE owner_user_id=@owner AND
                 status='completed' AND completed_at>=@from AND completed_at<@to),
                (SELECT count(*)::integer FROM task_segments WHERE owner_user_id=@owner
                 AND starts_at<@to AND ends_at>@from),
                (SELECT COALESCE(SUM(EXTRACT(EPOCH FROM
                 LEAST(ends_at,@to)-GREATEST(starts_at,@from))),0)::bigint
                 FROM task_segments WHERE owner_user_id=@owner AND starts_at<@to AND ends_at>@from),
                (SELECT count(*)::integer FROM day_markers WHERE user_id=@owner
                 AND day>=@first AND day<@next AND day_type='UL'),
                (SELECT count(*)::integer FROM day_markers WHERE user_id=@owner
                 AND day>=@first AND day<@next AND day_type='AM'),
                (SELECT count(*)::integer FROM day_markers WHERE user_id=@owner
                 AND day>=@first AND day<@next AND is_br),
                (SELECT count(*)::integer FROM day_markers WHERE user_id=@owner
                 AND day>=@first AND day<@next AND is_ho)
            """,db);
        Bind(stats,owner,first,next,from,to,zone.Id);
        int completed,segments,ul,am,br,ho;long planned;
        await using(var reader=await stats.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            completed=reader.GetInt32(0);segments=reader.GetInt32(1);
            planned=reader.GetInt64(2);ul=reader.GetInt32(3);am=reader.GetInt32(4);
            br=reader.GetInt32(5);ho=reader.GetInt32(6);
        }
        var tracked=days.Sum(x=>x.TrackedSeconds);
        var adjustment=days.Sum(x=>x.AdjustmentMinutes);
        return new MonthlyReport(year,month,zone.Id,tracked,adjustment,
            tracked+adjustment*60L,completed,segments,planned,ul,am,br,ho,days);
    }
    private static void Bind(NpgsqlCommand c,Guid owner,DateOnly first,DateOnly next,
        DateTime from,DateTime to,string zone)
    {
        c.Parameters.AddWithValue("owner",owner);
        c.Parameters.Add("first",NpgsqlDbType.Date).Value=first;
        c.Parameters.Add("next",NpgsqlDbType.Date).Value=next;
        c.Parameters.Add("from",NpgsqlDbType.TimestampTz).Value=from;
        c.Parameters.Add("to",NpgsqlDbType.TimestampTz).Value=to;
        c.Parameters.AddWithValue("zone",zone);
    }
    private static string Csv(MonthlyReport r)
    {
        var b=new StringBuilder("Plenaro Monatsreport\n");
        b.Append("Monat;").Append(r.Year.ToString("D4",CultureInfo.InvariantCulture))
         .Append('-').Append(r.Month.ToString("D2",CultureInfo.InvariantCulture)).AppendLine();
        b.Append("Zeitzone;").Append(Cell(r.TimeZone)).AppendLine();
        b.Append("Gebuchte Sekunden;").Append(r.TrackedSeconds).AppendLine();
        b.Append("Korrektur Minuten;").Append(r.AdjustmentMinutes).AppendLine();
        b.Append("Netto Sekunden;").Append(r.NetSeconds).AppendLine();
        b.Append("Erledigte Aufgaben;").Append(r.CompletedTasks).AppendLine();
        b.Append("Geplante Segmente;").Append(r.PlannedSegments).AppendLine();
        b.Append("Geplante Sekunden;").Append(r.PlannedSegmentSeconds).AppendLine();
        b.Append("UL Tage;").Append(r.UlDays).AppendLine();
        b.Append("AM Tage;").Append(r.AmDays).AppendLine();
        b.Append("BR Tage;").Append(r.BrDays).AppendLine();
        b.Append("HO Tage;").Append(r.HoDays).AppendLine().AppendLine();
        b.AppendLine("Datum;Aufgabe;Gebuchte Sekunden;Korrektur Minuten;Netto Sekunden");
        foreach(var d in r.Days)
            b.Append(d.Date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))
             .Append(';').Append(Cell(d.TaskTitle)).Append(';').Append(d.TrackedSeconds)
             .Append(';').Append(d.AdjustmentMinutes).Append(';')
             .Append(d.TrackedSeconds+d.AdjustmentMinutes*60L).AppendLine();
        return b.ToString();
    }
    private static string Cell(string input)
    {
        var trimmed=input.TrimStart();
        var dangerous=trimmed.StartsWith('=')||trimmed.StartsWith('+')||
            trimmed.StartsWith('-')||trimmed.StartsWith('@');
        var value=(dangerous?"'":"")+input;
        return "\""+value.Replace("\"","\"\"").Replace("\r"," ").Replace("\n"," ")+"\"";
    }
}
