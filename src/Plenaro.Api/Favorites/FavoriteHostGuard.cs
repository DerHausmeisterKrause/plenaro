using System.Net;

namespace Plenaro.Api.Favorites;

/// <summary>
/// Reserves wildcard application hostnames for a future, explicitly authorized
/// per-site proxy router. Unknown aliases must never fall through to Plenaro's
/// application UI, authentication cookies, static assets or APIs.
/// </summary>
public static class FavoriteHostGuard
{
    public static bool IsUnassignedAlias(
        string requestHost,string? baseDomain,string? plenaroOrigin,string? znunyOrigin)
    {
        if(string.IsNullOrWhiteSpace(baseDomain))return false;
        var suffix=baseDomain.Trim().TrimEnd('.');
        if(suffix.Length<4||!suffix.Contains('.')||
            Uri.CheckHostName(suffix)!=UriHostNameType.Dns||
            suffix.Contains('*'))
            throw new InvalidOperationException(
                "PLENARO_FAVORITES_BASE_DOMAIN muss eine gültige DNS-Domain sein.");

        var host=requestHost.TrimEnd('.');
        // Only aliases below the configured base domain are affected. This
        // preserves local IP / healthcheck access and the current deployment.
        if(!host.EndsWith("."+suffix,StringComparison.OrdinalIgnoreCase))
            return false;

        // The primary Plenaro origin is trusted independently; Znuny's
        // dedicated proxy is intercepted before this guard in Program.cs.
        if(MatchesOrigin(host,plenaroOrigin)||
            MatchesOrigin(host,znunyOrigin))
            return false;

        // Includes unconfigured first-level aliases and deeper names. A
        // wildcard NGINX server_name can match several levels, whereas a
        // one-level wildcard TLS certificate normally does not.
        return true;
    }

    private static bool MatchesOrigin(string host,string? origin)=>
        Uri.TryCreate(origin,UriKind.Absolute,out var url) &&
        url.Scheme==Uri.UriSchemeHttps &&
        host.Equals(url.Host,StringComparison.OrdinalIgnoreCase);
}
