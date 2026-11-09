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
        string requestHost,string? baseDomain,string? plenaroOrigin,string? znunyOrigin,
        string? hostTemplate=null)
    {
        var host=requestHost.TrimEnd('.');
        // The application and native Znuny origins must not be intercepted.
        // The generic wildcard router only owns configured alias families.
        if(MatchesOrigin(host,plenaroOrigin)||
            MatchesOrigin(host,znunyOrigin))
            return false;

        // Includes malformed/deep aliases: they receive 404 in FavoriteProxy
        // instead of accidentally falling through to Plenaro login or APIs.
        return FavoriteHostNames.IsReservedHost(host,baseDomain,hostTemplate);
    }

    private static bool MatchesOrigin(string host,string? origin)=>
        Uri.TryCreate(origin,UriKind.Absolute,out var url) &&
        url.Scheme==Uri.UriSchemeHttps &&
        host.Equals(url.Host,StringComparison.OrdinalIgnoreCase);
}
