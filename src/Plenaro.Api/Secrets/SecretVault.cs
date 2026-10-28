using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;

namespace Plenaro.Api.Secrets;

/// <summary>
/// AES-256-GCM with a separate key file (not stored in Postgres). The associated
/// data binds ciphertext to its exact scope, owner and name.
/// </summary>
public sealed class SecretVault
{
    private readonly byte[]? _key;
    private readonly string _connectionString;

    public bool IsConfigured => _key is { Length: 32 };

    public SecretVault(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Plenaro")
            ?? throw new InvalidOperationException("Plenaro connection string required.");
        var path = configuration["PLENARO_VAULT_KEY_FILE"];
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!File.Exists(path))
            throw new InvalidOperationException("Vault key file missing. Refusing to start with invalid secret configuration.");
        var hex = File.ReadAllText(path).Trim();
        try { _key = Convert.FromHexString(hex); }
        catch (FormatException) { throw new InvalidOperationException("Vault key must be 32 random bytes, encoded as 64 hex characters."); }
        if (_key.Length != 32)
            throw new InvalidOperationException("Vault key must contain exactly 32 random bytes.");
    }

    public async Task<string[]> ListNamesAsync(string scope, Guid? owner, CancellationToken ct)
    {
        EnsureConfigured();
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT secret_name FROM encrypted_secrets
            WHERE scope=@scope AND owner_user_id IS NOT DISTINCT FROM @owner
            ORDER BY secret_name
            """, db);
        cmd.Parameters.AddWithValue("scope", scope);
        cmd.Parameters.Add("owner", NpgsqlDbType.Uuid).Value = owner.HasValue ? owner.Value : DBNull.Value;
        var result = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    public async Task SetAsync(string scope, Guid? owner, string name, string secret, CancellationToken ct)
    {
        EnsureConfigured();
        var bytes = Encoding.UTF8.GetBytes(secret);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var encrypted = new byte[bytes.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(_key!, 16))
            aes.Encrypt(nonce, bytes, encrypted, tag, AssociatedData(scope, owner, name));
        CryptographicOperations.ZeroMemory(bytes);

        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO encrypted_secrets(scope,owner_user_id,secret_name,ciphertext,nonce,auth_tag)
            VALUES(@scope,@owner,@name,@ciphertext,@nonce,@tag)
            ON CONFLICT (owner_user_id,secret_name) WHERE scope='user'
            DO UPDATE SET ciphertext=EXCLUDED.ciphertext,nonce=EXCLUDED.nonce,
                auth_tag=EXCLUDED.auth_tag,updated_at=now()
            """, db);
        // Use a separate upsert for the global partial unique index.
        if (scope == "global")
            cmd.CommandText = """
                INSERT INTO encrypted_secrets(scope,owner_user_id,secret_name,ciphertext,nonce,auth_tag)
                VALUES(@scope,@owner,@name,@ciphertext,@nonce,@tag)
                ON CONFLICT (secret_name) WHERE scope='global'
                DO UPDATE SET ciphertext=EXCLUDED.ciphertext,nonce=EXCLUDED.nonce,
                    auth_tag=EXCLUDED.auth_tag,updated_at=now()
                """;
        cmd.Parameters.AddWithValue("scope", scope);
        cmd.Parameters.Add("owner", NpgsqlDbType.Uuid).Value = owner.HasValue ? owner.Value : DBNull.Value;
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("ciphertext", encrypted);
        cmd.Parameters.AddWithValue("nonce", nonce);
        cmd.Parameters.AddWithValue("tag", tag);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>For trusted server-side integrations only. Never expose as a browser route.</summary>
    public async Task<string?> GetForIntegrationAsync(string scope, Guid? owner, string name, CancellationToken ct)
    {
        EnsureConfigured();
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT ciphertext,nonce,auth_tag FROM encrypted_secrets
            WHERE scope=@scope AND owner_user_id IS NOT DISTINCT FROM @owner AND secret_name=@name
            """, db);
        cmd.Parameters.AddWithValue("scope", scope);
        cmd.Parameters.Add("owner", NpgsqlDbType.Uuid).Value = owner.HasValue ? owner.Value : DBNull.Value;
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var encrypted = reader.GetFieldValue<byte[]>(0);
        var nonce = reader.GetFieldValue<byte[]>(1);
        var tag = reader.GetFieldValue<byte[]>(2);
        var plain = new byte[encrypted.Length];
        using (var aes = new AesGcm(_key!, 16))
            aes.Decrypt(nonce, encrypted, tag, plain, AssociatedData(scope, owner, name));
        var value = Encoding.UTF8.GetString(plain);
        CryptographicOperations.ZeroMemory(plain);
        return value;
    }

    public async Task DeleteAsync(string scope, Guid? owner, string name, CancellationToken ct)
    {
        EnsureConfigured();
        await using var db = new NpgsqlConnection(_connectionString);
        await db.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            DELETE FROM encrypted_secrets
            WHERE scope=@scope AND owner_user_id IS NOT DISTINCT FROM @owner AND secret_name=@name
            """, db);
        cmd.Parameters.AddWithValue("scope", scope);
        cmd.Parameters.Add("owner", NpgsqlDbType.Uuid).Value = owner.HasValue ? owner.Value : DBNull.Value;
        cmd.Parameters.AddWithValue("name", name);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public static bool ValidName(string? name) =>
        name is { Length: >= 2 and <= 64 } &&
        name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_' or '-');

    private void EnsureConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException("Vault master key is not configured.");
    }

    private static byte[] AssociatedData(string scope, Guid? owner, string name) =>
        Encoding.UTF8.GetBytes($"plenaro:v1|{scope}|{owner?.ToString("D") ?? "-"}|{name}");
}
