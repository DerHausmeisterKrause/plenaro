using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace Plenaro.Api.Znuny;

/// <summary>
/// WPF TicketArticleReadStateService parity without user-specific Znuny polling.
/// Shared article snapshots update only each subscribed user's local read state.
/// The first reliable snapshot becomes a silent baseline; only later articles
/// are unread, and each article is acknowledged individually.
/// </summary>
public static class ZnunyArticleReadTracker
{
    private sealed record Position(string Id, DateTime? CreatedAt);
    private sealed record Watch(Guid UserId, Position LastSeen);

    private static DateTime? ParseCreated(string created)
    {
        if (DateTime.TryParse(created, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date))
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return null;
    }

    /// <summary>Matches the Desktop watermark: compare timestamps first,
    /// then numeric article IDs if times match or are unknown.</summary>
    public static bool IsReliablyAfter(DateTime? created, string id,
        DateTime? watermarkCreated, string watermarkId)
    {
        if (created.HasValue && watermarkCreated.HasValue)
        {
            var comparison = created.Value.CompareTo(watermarkCreated.Value);
            if (comparison != 0) return comparison > 0;
        }
        return long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture,
                   out var next) &&
               long.TryParse(watermarkId, NumberStyles.None, CultureInfo.InvariantCulture,
                   out var previous) && next > previous;
    }

    private static Position? Newest(IEnumerable<Position> articles)
    {
        Position? newest = null;
        foreach (var article in articles)
            if (newest is null || IsReliablyAfter(
                article.CreatedAt, article.Id, newest.CreatedAt, newest.Id))
                newest = article;
        return newest;
    }

    /// <summary>Initialize exactly the tickets already authorized for a user by
    /// the enclosing list endpoint. An initial list never invents unread items.</summary>
    public static async Task InitializeFromHeadsAsync(
        NpgsqlConnection db, Guid userId, IEnumerable<string> ticketIds, CancellationToken ct)
    {
        var ids = ticketIds.Distinct(StringComparer.Ordinal).Take(500).ToArray();
        if (ids.Length == 0) return;
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO znuny_ticket_article_watch
                (user_id,ticket_id,newest_article_id,newest_created_at)
            SELECT @user,h.ticket_id,h.newest_article_id,h.newest_created_at
            FROM znuny_ticket_article_heads h
            WHERE h.ticket_id=ANY(@ids)
            ON CONFLICT(user_id,ticket_id) DO NOTHING
            """, db);
        cmd.Parameters.AddWithValue("user", userId);
        cmd.Parameters.Add("ids", NpgsqlDbType.Array | NpgsqlDbType.Varchar).Value = ids;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Called by the existing central article index, or by a successful
    /// personal article fetch. Only a successful, complete enough TicketGet
    /// response is passed here; transient Znuny errors leave unread state intact.</summary>
    public static async Task ObserveAsync(NpgsqlConnection db, string ticketId,
        IReadOnlyList<ZnunyReader.Article> fetched, Guid? currentUser, CancellationToken ct)
    {
        var articles = fetched
            .Where(article => article.Id.Length is > 0 and <= 80)
            .DistinctBy(article => article.Id, StringComparer.Ordinal)
            .Take(500)
            .Select(article => new Position(article.Id, ParseCreated(article.Created)))
            .ToList();
        var newest = Newest(articles);
        if (newest is null) return;

        await using var tx = await db.BeginTransactionAsync(ct);
        // Index and personal live TicketGet can overlap. Serialize snapshots per
        // ticket so older fetches never regress the watermark or reflag read mail.
        await using (var guard = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(24024,hashtext(@ticket))", db, tx))
        {
            guard.Parameters.AddWithValue("ticket", ticketId);
            await guard.ExecuteNonQueryAsync(ct);
        }

        // A person's FIRST reliable fetch is their baseline, not a flood of
        // historical unread mail. Existing subscribers are reconciled below.
        if (currentUser is Guid user)
        {
            await using var init = new NpgsqlCommand("""
                INSERT INTO znuny_ticket_article_watch
                    (user_id,ticket_id,newest_article_id,newest_created_at)
                VALUES(@user,@ticket,@article,@created)
                ON CONFLICT(user_id,ticket_id) DO NOTHING
                """, db, tx);
            init.Parameters.AddWithValue("user", user);
            init.Parameters.AddWithValue("ticket", ticketId);
            init.Parameters.AddWithValue("article", newest.Id);
            init.Parameters.Add("created", NpgsqlDbType.TimestampTz).Value =
                (object?)newest.CreatedAt ?? DBNull.Value;
            await init.ExecuteNonQueryAsync(ct);
        }

        var watchers = new List<Watch>();
        await using (var query = new NpgsqlCommand("""
            SELECT user_id,newest_article_id,newest_created_at
            FROM znuny_ticket_article_watch WHERE ticket_id=@ticket
            FOR UPDATE
            """, db, tx))
        {
            query.Parameters.AddWithValue("ticket", ticketId);
            await using var rows = await query.ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct))
                watchers.Add(new Watch(rows.GetGuid(0),
                    new Position(rows.GetString(1),
                        rows.IsDBNull(2) ? null : rows.GetDateTime(2))));
        }

        foreach (var watch in watchers)
        {
            foreach (var article in articles)
            {
                if (!IsReliablyAfter(article.CreatedAt,article.Id,
                    watch.LastSeen.CreatedAt,watch.LastSeen.Id)) continue;
                await using var add = new NpgsqlCommand("""
                    INSERT INTO znuny_ticket_article_read_state
                        (user_id,ticket_id,article_id)
                    VALUES(@user,@ticket,@article)
                    ON CONFLICT(user_id,ticket_id,article_id) DO NOTHING
                    """, db, tx);
                add.Parameters.AddWithValue("user", watch.UserId);
                add.Parameters.AddWithValue("ticket", ticketId);
                add.Parameters.AddWithValue("article", article.Id);
                await add.ExecuteNonQueryAsync(ct);
            }

            if (!IsReliablyAfter(newest.CreatedAt,newest.Id,
                watch.LastSeen.CreatedAt,watch.LastSeen.Id)) continue;
            await using var advance = new NpgsqlCommand("""
                UPDATE znuny_ticket_article_watch
                SET newest_article_id=@article,newest_created_at=@created
                WHERE user_id=@user AND ticket_id=@ticket
                """, db, tx);
            advance.Parameters.AddWithValue("user", watch.UserId);
            advance.Parameters.AddWithValue("ticket", ticketId);
            advance.Parameters.AddWithValue("article", newest.Id);
            advance.Parameters.Add("created",NpgsqlDbType.TimestampTz).Value =
                (object?)newest.CreatedAt ?? DBNull.Value;
            await advance.ExecuteNonQueryAsync(ct);
        }

        Position? head = null;
        await using (var load = new NpgsqlCommand("""
            SELECT newest_article_id,newest_created_at
            FROM znuny_ticket_article_heads WHERE ticket_id=@ticket
            """, db, tx))
        {
            load.Parameters.AddWithValue("ticket", ticketId);
            await using var rows = await load.ExecuteReaderAsync(ct);
            if (await rows.ReadAsync(ct))
                head = new Position(rows.GetString(0),
                    rows.IsDBNull(1) ? null : rows.GetDateTime(1));
        }
        if (head is null || IsReliablyAfter(newest.CreatedAt,newest.Id,
            head.CreatedAt,head.Id))
        {
            await using var save = new NpgsqlCommand("""
                INSERT INTO znuny_ticket_article_heads
                    (ticket_id,newest_article_id,newest_created_at)
                VALUES(@ticket,@article,@created)
                ON CONFLICT(ticket_id) DO UPDATE SET
                    newest_article_id=EXCLUDED.newest_article_id,
                    newest_created_at=EXCLUDED.newest_created_at,
                    checked_at=now()
                """, db, tx);
            save.Parameters.AddWithValue("ticket", ticketId);
            save.Parameters.AddWithValue("article", newest.Id);
            save.Parameters.Add("created",NpgsqlDbType.TimestampTz).Value =
                (object?)newest.CreatedAt ?? DBNull.Value;
            await save.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    public static async Task<string[]> UnreadIdsAsync(
        NpgsqlConnection db, Guid userId, string ticketId, CancellationToken ct)
    {
        var result = new List<string>();
        await using var query = new NpgsqlCommand("""
            SELECT article_id FROM znuny_ticket_article_read_state
            WHERE user_id=@user AND ticket_id=@ticket AND read_at IS NULL
            ORDER BY first_seen_at,article_id LIMIT 500
            """, db);
        query.Parameters.AddWithValue("user", userId);
        query.Parameters.AddWithValue("ticket", ticketId);
        await using var rows = await query.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct)) result.Add(rows.GetString(0));
        return result.ToArray();
    }

    public static async Task MarkReadAsync(NpgsqlConnection db, Guid userId,
        string ticketId, string articleId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            UPDATE znuny_ticket_article_read_state SET read_at=now()
            WHERE user_id=@user AND ticket_id=@ticket
              AND article_id=@article AND read_at IS NULL
            """, db);
        cmd.Parameters.AddWithValue("user", userId);
        cmd.Parameters.AddWithValue("ticket", ticketId);
        cmd.Parameters.AddWithValue("article", articleId);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
