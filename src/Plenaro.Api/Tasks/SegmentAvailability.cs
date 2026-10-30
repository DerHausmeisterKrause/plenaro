using Npgsql;
using NpgsqlTypes;

namespace Plenaro.Api.Tasks;

/// <summary>
/// Checks only the authenticated user's own local segments. This is not a
/// Microsoft Graph free/busy or participant-availability service.
/// </summary>
internal static class SegmentAvailability
{
    internal sealed record Conflict(Guid Id, Guid TaskId, string TaskTitle,
        DateTimeOffset StartsAt, DateTimeOffset EndsAt);

    internal sealed record Result(bool Available, IReadOnlyList<Conflict> Conflicts);

    // Every write path acquires the same per-user transaction-scoped advisory lock
    // before evaluating overlap; concurrent writes from this application serialize.
    internal static async Task LockOwnerAsync(NpgsqlConnection db, NpgsqlTransaction transaction,
        Guid owner, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@owner::text, 0))", db, transaction);
        command.Parameters.AddWithValue("owner", owner);
        await command.ExecuteNonQueryAsync(ct);
    }

    internal static async Task<Result> ReadAsync(NpgsqlConnection db, Guid owner,
        DateTimeOffset startsAt, DateTimeOffset endsAt, Guid? except,
        NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT s.id, s.task_id, t.title, s.starts_at, s.ends_at
            FROM task_segments s
            JOIN tasks t ON t.id=s.task_id AND t.owner_user_id=s.owner_user_id
            WHERE s.owner_user_id=@owner
              AND s.starts_at < @end AND s.ends_at > @start
              AND (@except IS NULL OR s.id <> @except)
            ORDER BY s.starts_at, s.id
            LIMIT 21
            """, db, transaction);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.Add("start", NpgsqlDbType.TimestampTz).Value = startsAt.UtcDateTime;
        command.Parameters.Add("end", NpgsqlDbType.TimestampTz).Value = endsAt.UtcDateTime;
        command.Parameters.Add("except", NpgsqlDbType.Uuid).Value =
            except.HasValue ? except.Value : DBNull.Value;
        var result = new List<Conflict>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new Conflict(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2),
                new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero),
                new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero)));
        // Never imply that the slot is free just because the response is truncated.
        return new Result(result.Count == 0, result.Take(20).ToArray());
    }
}
