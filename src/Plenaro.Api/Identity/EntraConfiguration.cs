using System.Text.Json;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Identity;

/// <summary>
/// In-memory immutable snapshot backed by the AES-256-GCM vault. No Entra
/// credentials are read from environment variables or written to logs.
/// </summary>
public sealed class EntraConfiguration(bool requireHttps)
{
    public const string Scheme = "Plenaro.Entra";
    public const string Provider = "Entra";
    public const string SecretName = "entra.config";

    private sealed record Snapshot(bool Enabled, string TenantId, string ClientId, string ClientSecret);
    private volatile Snapshot _current = new(false, "", "", "");

    public bool Enabled => _current.Enabled;
    public string TenantId => _current.TenantId;
    public string ClientId => _current.ClientId;
    public string ClientSecret => _current.ClientSecret;
    public bool RequireHttps => requireHttps;

    public async Task InitializeAsync(SecretVault vault, CancellationToken ct)
    {
        if (!vault.IsConfigured) return;
        var raw = await vault.GetForIntegrationAsync("global", null, SecretName, ct);
        if (string.IsNullOrWhiteSpace(raw)) return;
        var data = JsonSerializer.Deserialize<Snapshot>(raw);
        if (data is null) throw new InvalidOperationException("Invalid stored Entra configuration.");
        Validate(data);
        _current = data;
    }

    public async Task SaveAsync(SecretVault vault, bool enabled, string tenant, string client,
        string secret, CancellationToken ct)
    {
        var data = new Snapshot(enabled, tenant.Trim().ToLowerInvariant(),
            client.Trim().ToLowerInvariant(), secret);
        Validate(data);
        await vault.SetAsync("global", null, SecretName, JsonSerializer.Serialize(data), ct);
        _current = data; // publish atomically only after successful encrypted persist
    }

    public async Task DisableAsync(SecretVault vault, CancellationToken ct)
    {
        await vault.DeleteAsync("global", null, SecretName, ct);
        _current = new(false, "", "", "");
    }

    private void Validate(Snapshot data)
    {
        if (!data.Enabled) throw new InvalidOperationException("Only enabled Entra configurations may be stored.");
        if (!requireHttps)
            throw new InvalidOperationException("Entra SSO requires HTTPS and a trusted public name.");
        if (!Guid.TryParse(data.TenantId, out var tenant) || tenant == Guid.Empty ||
            !Guid.TryParse(data.ClientId, out var client) || client == Guid.Empty ||
            data.ClientSecret.Length is < 16 or > 4096)
            throw new InvalidOperationException("Invalid tenant/client ID or client secret length.");
    }
}
