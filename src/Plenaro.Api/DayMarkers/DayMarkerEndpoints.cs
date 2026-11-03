using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.DayMarkers;

/// <summary>
/// UL / AM are mutually exclusive day types; BR is a separate day flag, as in the
/// legacy Plenaro model. Budgets warn but never silently reject saved day markers.
/// Every query is scoped to the authenticated, enabled local Identity user.
/// </summary>
public static class DayMarkerEndpoints
{
    public sealed record BudgetInput(int? AnnualUlDays, int? MonthlyBrDays);
    public sealed record MarkerInput(string DayType, bool IsBr, bool IsHo = false);
    public sealed record MarkerView(DateOnly Day, string DayType, bool IsBr, bool IsHo);
    public sealed record BudgetSummary(int Year, int Month, int? AnnualUlDays,
        int UsedUlDays, int? RemainingUlDays, bool UlExhausted, bool UlExceeded,
        int? MonthlyBrDays, int UsedBrDays, int? RemainingBrDays,
        bool BrExhausted, bool BrExceeded);

    public static void MapPlenaroDayMarkers(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/day-markers").RequireAuthorization();

        group.MapGet("/budget", async (HttpContext http, IConfiguration config,
            UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            var user = await OwnerAsync(http, users);
            if (user is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            var budgets = await LoadBudgetAsync(db, user.Value, ct);
            return Results.Ok(budgets);
        });

        group.MapPut("/budget", async (HttpContext http, BudgetInput input,
            IConfiguration config, UserManager<ApplicationUser> users,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            var user = await OwnerAsync(http, users);
            if (user is null) return Results.Unauthorized();
            if (input.AnnualUlDays is < 0 or > 366 || input.MonthlyBrDays is < 0 or > 31)
                return Results.BadRequest(new { error = "UL muss zwischen 0 und 366, BR zwischen 0 und 31 liegen." });

            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO day_marker_budgets(user_id,annual_ul_days,monthly_br_days,updated_at)
                VALUES(@owner,@ul,@br,now())
                ON CONFLICT (user_id) DO UPDATE SET annual_ul_days=EXCLUDED.annual_ul_days,
                    monthly_br_days=EXCLUDED.monthly_br_days,updated_at=now()
                """, db);
            cmd.Parameters.AddWithValue("owner", user.Value);
            cmd.Parameters.Add("ul", NpgsqlDbType.Smallint).Value =
                input.AnnualUlDays.HasValue ? (object)(short)input.AnnualUlDays.Value : DBNull.Value;
            cmd.Parameters.Add("br", NpgsqlDbType.Smallint).Value =
                input.MonthlyBrDays.HasValue ? (object)(short)input.MonthlyBrDays.Value : DBNull.Value;
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/summary", async (int year, int month, HttpContext http,
            IConfiguration config, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (!ValidMonth(year, month)) return Results.BadRequest(new { error = "Ungültiger Monat." });
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            var budget = await LoadBudgetAsync(db, owner.Value, ct);
            var startYear = new DateOnly(year, 1, 1);
            var nextYear = startYear.AddYears(1);
            var startMonth = new DateOnly(year, month, 1);
            var nextMonth = startMonth.AddMonths(1);
            await using var cmd = new NpgsqlCommand("""
                SELECT
                    count(*) FILTER (WHERE day_type='UL')::integer,
                    count(*) FILTER (WHERE is_br AND day>=@month_start AND day<@next_month)::integer
                FROM day_markers
                WHERE user_id=@owner AND day>=@year_start AND day<@next_year
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.Add("year_start", NpgsqlDbType.Date).Value = startYear;
            cmd.Parameters.Add("next_year", NpgsqlDbType.Date).Value = nextYear;
            cmd.Parameters.Add("month_start", NpgsqlDbType.Date).Value = startMonth;
            cmd.Parameters.Add("next_month", NpgsqlDbType.Date).Value = nextMonth;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);
            var usedUl = reader.GetInt32(0);
            var usedBr = reader.GetInt32(1);
            return Results.Ok(new BudgetSummary(year, month, budget.AnnualUlDays,
                usedUl, budget.AnnualUlDays - usedUl,
                budget.AnnualUlDays is not null && usedUl >= budget.AnnualUlDays,
                budget.AnnualUlDays is not null && usedUl > budget.AnnualUlDays,
                budget.MonthlyBrDays, usedBr, budget.MonthlyBrDays - usedBr,
                budget.MonthlyBrDays is not null && usedBr >= budget.MonthlyBrDays,
                budget.MonthlyBrDays is not null && usedBr > budget.MonthlyBrDays));
        });

        group.MapGet("/", async (int year, int month, HttpContext http,
            IConfiguration config, UserManager<ApplicationUser> users, CancellationToken ct) =>
        {
            if (!ValidMonth(year, month)) return Results.BadRequest(new { error = "Ungültiger Monat." });
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                SELECT day,day_type,is_br,is_ho FROM day_markers
                WHERE user_id=@owner AND day>=@first AND day<@last
                ORDER BY day
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.Add("first", NpgsqlDbType.Date).Value = new DateOnly(year, month, 1);
            cmd.Parameters.Add("last", NpgsqlDbType.Date).Value = new DateOnly(year, month, 1).AddMonths(1);
            var result = new List<MarkerView>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                result.Add(new MarkerView(reader.GetFieldValue<DateOnly>(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)));
            return Results.Ok(result);
        });

        group.MapPut("/{day}", async (string day, MarkerInput input,
            HttpContext http, IConfiguration config, UserManager<ApplicationUser> users,
            IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            if (!TryDate(day, out var parsed) ||
                input.DayType is not ("Normal" or "UL" or "AM"))
                return Results.BadRequest(new { error = "Ungültiger Tag oder Marker (Normal, UL, AM)." });
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();

            await using var db = await OpenAsync(config, ct);
            if (input.DayType == "Normal" && !input.IsBr && !input.IsHo)
            {
                await using var remove = new NpgsqlCommand(
                    "DELETE FROM day_markers WHERE user_id=@owner AND day=@day", db);
                remove.Parameters.AddWithValue("owner", owner.Value);
                remove.Parameters.Add("day", NpgsqlDbType.Date).Value = parsed;
                await remove.ExecuteNonQueryAsync(ct);
            }
            else
            {
                await using var cmd = new NpgsqlCommand("""
                    INSERT INTO day_markers(user_id,day,day_type,is_br,is_ho,updated_at)
                    VALUES(@owner,@day,@type,@br,@ho,now())
                    ON CONFLICT(user_id,day) DO UPDATE SET
                        day_type=EXCLUDED.day_type,is_br=EXCLUDED.is_br,is_ho=EXCLUDED.is_ho,updated_at=now()
                    """, db);
                cmd.Parameters.AddWithValue("owner", owner.Value);
                cmd.Parameters.Add("day", NpgsqlDbType.Date).Value = parsed;
                cmd.Parameters.AddWithValue("type", input.DayType);
                cmd.Parameters.AddWithValue("br", input.IsBr);
                cmd.Parameters.AddWithValue("ho", input.IsHo);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            return Results.NoContent();
        });
    }

    private static bool ValidMonth(int year, int month) =>
        year is >= 2000 and <= 2099 && month is >= 1 and <= 12;

    private static bool TryDate(string input, out DateOnly date) =>
        DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date) && date.Year is >= 2000 and <= 2099;

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

    private static async Task<BudgetInput> LoadBudgetAsync(NpgsqlConnection db, Guid owner, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT annual_ul_days,monthly_br_days FROM day_marker_budgets WHERE user_id=@owner", db);
        cmd.Parameters.AddWithValue("owner", owner);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return new BudgetInput(null, null);
        return new BudgetInput(
            reader.IsDBNull(0) ? null : reader.GetInt16(0),
            reader.IsDBNull(1) ? null : reader.GetInt16(1));
    }

    private static async Task<bool> ValidCsrfAsync(HttpContext http, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(http); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() =>
        Results.BadRequest(new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
