namespace Plenaro.Api.Identity;

/// <summary>Single-tenant Entra OIDC is opt-in and requires TLS.</summary>
public sealed record EntraConfiguration(bool Enabled, string TenantId, string ClientId, string ClientSecret)
{
    public const string Scheme = "Plenaro.Entra";
    public const string Provider = "Entra";

    public static EntraConfiguration Load(IConfiguration config, bool requireHttps)
    {
        if (!config.GetValue<bool>("Entra:Enabled"))
            return new(false, "", "", "");

        if (!requireHttps)
            throw new InvalidOperationException("Microsoft Entra login requires PLENARO_HTTPS_ENABLED=true.");

        var tenant = config["Entra:TenantId"];
        var client = config["Entra:ClientId"];
        var path = config["Entra:ClientSecretFile"];
        if (!Guid.TryParse(tenant, out var tenantGuid) || tenantGuid == Guid.Empty ||
            !Guid.TryParse(client, out var clientGuid) || clientGuid == Guid.Empty ||
            string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException(
                "Entra requires valid tenant/client GUIDs and a readable client secret file.");

        var secret = File.ReadAllText(path).TrimEnd('\r', '\n');
        if (secret.Length is < 16 or > 4096)
            throw new InvalidOperationException("Entra client secret file is empty or invalid.");

        return new(true, tenantGuid.ToString("D"), clientGuid.ToString("D"), secret);
    }
}
