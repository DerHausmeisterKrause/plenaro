using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Hard cap for time bookings initiated from Plenaro: at most 24 hours
/// (1440 actual Znuny TimeUnit minutes) per approved Znuny agent and Berlin
/// calendar day, across all users and tickets. Pending writes reserve quota.
/// </summary>
public static class ZnunyAgentDailyBookingLimit
{
    public const int MaximumMinutes=24*60;
    private static readonly TimeZoneInfo Berlin=
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    public static DateOnly BookingDay(DateTimeOffset when)=>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(when,Berlin).DateTime);

    public static int RoundedMinutes(long sourceSeconds)
    {
        if(sourceSeconds<=0||sourceSeconds>864000)
            throw new ArgumentOutOfRangeException(nameof(sourceSeconds));
        return checked((int)(((sourceSeconds+899)/900)*15));
    }

    public static bool CanBook(long alreadyReservedMinutes,int requestedMinutes)=>
        alreadyReservedMinutes>=0 && requestedMinutes>0 &&
        requestedMinutes%15==0 &&
        alreadyReservedMinutes<=MaximumMinutes &&
        requestedMinutes<=MaximumMinutes-alreadyReservedMinutes;

    /// <summary>
    /// Transaction MUST remain open through checking and inserting a pending
    /// record. Every booking path must use this same lock to serialize even
    /// bookings in different tickets/users that map to the same agent.
    /// </summary>
    public static async Task AcquireAgentDayLockAsync(
        NpgsqlConnection db,NpgsqlTransaction tx,int agent,DateOnly day,
        CancellationToken ct)
    {
        if(agent<=0)throw new ArgumentOutOfRangeException(nameof(agent));
        await using var guard=new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 4))",db,tx);
        guard.Parameters.AddWithValue("key",LockKey(agent,day));
        await guard.ExecuteNonQueryAsync(ct);
    }

    public static async Task<long> ReservedMinutesAsync(
        NpgsqlConnection db,NpgsqlTransaction tx,int agent,DateOnly day,
        CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT COALESCE(SUM(booked_minutes),0)::bigint
            FROM znuny_ticket_time_bookings
            WHERE agent_id=@agent AND booking_day=@day
              AND status IN ('pending','confirmed')
            """,db,tx);
        cmd.Parameters.AddWithValue("agent",agent);
        cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
        return (long)(await cmd.ExecuteScalarAsync(ct)??0L);
    }

    public static string LockKey(int agent,DateOnly day)
    {
        if(agent<=0)throw new ArgumentOutOfRangeException(nameof(agent));
        return "znuny-time-booking-agent-day:"+
            agent.ToString(CultureInfo.InvariantCulture)+":"+
            day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
    }
}
