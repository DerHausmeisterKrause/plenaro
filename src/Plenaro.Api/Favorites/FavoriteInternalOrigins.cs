namespace Plenaro.Api.Favorites;

/// <summary>
/// Operator-controlled, exact HTTPS origin exceptions for personal intranet
/// favorites. A user can still create their own bookmark, but cannot grant
/// the proxy access to an arbitrary private network destination.
/// </summary>
public static class FavoriteInternalOrigins
{
    public const string ConfigurationKey="PLENARO_FAVORITES_INTERNAL_ORIGINS";

    public static bool IsApproved(string? configured,Uri upstream)
    {
        if(string.IsNullOrWhiteSpace(configured) ||
           upstream.Scheme!=Uri.UriSchemeHttps)
            return false;

        var approved=false;
        // Semicolon-separated, full origins (including a non-default port).
        // Validate the WHOLE list even if an earlier entry matches, so
        // malformed operator configuration always fails closed.
        foreach(var item in configured.Split(';',
            StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
        {
            if(!Uri.TryCreate(item,UriKind.Absolute,out var origin) ||
               origin.Scheme!=Uri.UriSchemeHttps ||
               origin.HostNameType!=UriHostNameType.Dns ||
               origin.UserInfo.Length>0 ||
               origin.AbsolutePath!="/" ||
               origin.Query.Length>0 || origin.Fragment.Length>0)
                throw new InvalidOperationException(
                    ConfigurationKey+" enthält keine gültige HTTPS-Origin.");

            // No suffix, subdomain, path or prefix matching. The port also
            // matters: https://intranet.example and :8443 are different.
            if(origin.Host.Equals(upstream.Host,
                    StringComparison.OrdinalIgnoreCase) &&
               origin.Port==upstream.Port)
                approved=true;
        }
        return approved;
    }
}
