using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace Plenaro.Api.Database;

/// <summary>
/// Applies embedded PostgreSQL migrations atomically under a transaction-scoped advisory lock.
/// Never edits applied migrations: new schema changes require a new numbered SQL file.
/// </summary>
public sealed class SchemaMigrator
{
    private const string MigrationPrefix = "Plenaro.Api.Database.Migrations.";
    private const long AdvisoryLockKey = 496576540201L;
    private static readonly IReadOnlyList<SchemaMigration> Migrations = LoadMigrations();

    public int CurrentVersion => Migrations.Count;

    public async Task ApplyPendingAsync(string connectionString, ILogger logger, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // The lock remains held until commit/rollback: only one replica can migrate at once.
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("key", AdvisoryLockKey);
            await lockCommand.ExecuteNonQueryAsync(ct);
        }

        await using (var createHistory = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_id varchar(128) PRIMARY KEY,
                checksum_sha256 char(64) NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT now()
            );
            """, connection, transaction))
        {
            await createHistory.ExecuteNonQueryAsync(ct);
        }

        var applied = await ReadAppliedAsync(connection, transaction, ct);
        VerifyHistory(applied);

        foreach (var migration in Migrations)
        {
            if (applied.ContainsKey(migration.Id))
                continue;

            await using (var apply = new NpgsqlCommand(migration.Sql, connection, transaction))
            {
                apply.CommandTimeout = 120;
                await apply.ExecuteNonQueryAsync(ct);
            }

            await using (var record = new NpgsqlCommand("""
                INSERT INTO schema_migrations (migration_id, checksum_sha256)
                VALUES (@id, @checksum)
                """, connection, transaction))
            {
                record.Parameters.AddWithValue("id", migration.Id);
                record.Parameters.AddWithValue("checksum", migration.Checksum);
                await record.ExecuteNonQueryAsync(ct);
            }

            logger.LogInformation("Database migration {MigrationId} applied", migration.Id);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<bool> IsReadyAsync(string connectionString, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        Dictionary<string, string> applied;
        try
        {
            applied = await ReadAppliedAsync(connection, null, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return false;
        }

        if (!HistoryMatches(applied))
            return false;

        await using var schemaCheck = new NpgsqlCommand("""
            SELECT to_regclass('public.tasks') IS NOT NULL
               AND to_regclass('public.task_segments') IS NOT NULL
               AND to_regclass('public.time_entries') IS NOT NULL
               AND to_regclass('public.system_settings') IS NOT NULL;
            """, connection);
        return await schemaCheck.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<Dictionary<string, string>> ReadAppliedAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var command = new NpgsqlCommand(
            "SELECT migration_id, checksum_sha256 FROM schema_migrations ORDER BY migration_id",
            connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(reader.GetString(0), reader.GetString(1).TrimEnd());
        }
        return result;
    }

    private static bool HistoryMatches(Dictionary<string, string> applied)
    {
        return applied.Count == Migrations.Count &&
               Migrations.All(m => applied.TryGetValue(m.Id, out var checksum) &&
                                   string.Equals(checksum, m.Checksum, StringComparison.Ordinal));
    }

    private static void VerifyHistory(Dictionary<string, string> applied)
    {
        var expected = Migrations.ToDictionary(m => m.Id, m => m.Checksum, StringComparer.Ordinal);
        foreach (var pair in applied)
        {
            if (!expected.TryGetValue(pair.Key, out var checksum))
                throw new InvalidOperationException(
                    $"Unknown database migration '{pair.Key}'. Refusing a potentially destructive downgrade.");

            if (!string.Equals(checksum, pair.Value, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Database migration '{pair.Key}' differs from the original SQL file. Applied migrations are immutable.");
        }

        // Prevent an inconsistent history (e.g. migration 0002 present without 0001).
        var installedIds = applied.Keys.Order(StringComparer.Ordinal).ToArray();
        var expectedPrefix = Migrations.Take(applied.Count).Select(m => m.Id).ToArray();
        if (!installedIds.SequenceEqual(expectedPrefix, StringComparer.Ordinal))
            throw new InvalidOperationException("Database migration history is incomplete or out of order.");
    }

    private static IReadOnlyList<SchemaMigration> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(MigrationPrefix, StringComparison.Ordinal) &&
                        n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        if (resources.Length == 0)
            throw new InvalidOperationException("No embedded database migrations were found.");

        var migrations = new List<SchemaMigration>(resources.Length);
        foreach (var resource in resources)
        {
            var id = resource[MigrationPrefix.Length..^4];
            if (id.Length < 6 || id.Any(c => !char.IsAsciiLetterLower(c) &&
                                              !char.IsAsciiDigit(c) && c != '_') ||
                !char.IsAsciiDigit(id[0]) || !char.IsAsciiDigit(id[1]) ||
                !char.IsAsciiDigit(id[2]) || !char.IsAsciiDigit(id[3]) || id[4] != '_')
                throw new InvalidOperationException($"Invalid SQL migration resource: {resource}");

            using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Missing SQL migration resource: {resource}");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var sql = reader.ReadToEnd();
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            migrations.Add(new SchemaMigration(id, sql, checksum));
        }

        return migrations;
    }

    private sealed record SchemaMigration(string Id, string Sql, string Checksum);
}
