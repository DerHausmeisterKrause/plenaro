namespace Plenaro.Api.Favorites;

/// <summary>
/// Optional single-host, path-prefixed favorite router. This does NOT provide
/// the origin isolation of separate per-app hostnames; use trusted apps only.
/// Each path still has independent server-side sessions and path-scoped cookies.
/// </summary>
public static class FavoritePathRouting
{
    public static Uri? SharedOrigin(string? configured, string? plenaroOrigin,
        string? znunyOrigin)
    {
        if(string.IsNullOrWhiteSpace(configured))return null;
        if(!Uri.TryCreate(configured.Trim(),UriKind.Absolute,out var uri)||
            uri.Scheme!=Uri.UriSchemeHttps || uri.HostNameType!=UriHostNameType.Dns ||
            !uri.Host.Contains('.') || uri.Port!=443 || uri.UserInfo.Length!=0 ||
            uri.AbsolutePath!="/" || uri.Query.Length!=0 || uri.Fragment.Length!=0)
            throw new InvalidOperationException(
                "PLENARO_FAVORITES_PATH_ORIGIN muss eine HTTPS-DNS-Origin ohne Pfad oder Port sein.");
        foreach(var other in new[]{plenaroOrigin,znunyOrigin})
            if(Uri.TryCreate(other,UriKind.Absolute,out var reserved) &&
                uri.Host.Equals(reserved.Host,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Der Favoriten-Pfadhost muss von Plenaro und Znuny getrennt sein.");
        return new Uri(uri.GetLeftPart(UriPartial.Authority)+"/");
    }

    public static bool IsSharedHost(string host,string? configured,
        string? plenaroOrigin,string? znunyOrigin)
    {
        var baseUri=SharedOrigin(configured,plenaroOrigin,znunyOrigin);
        return baseUri is not null &&
            baseUri.Host.Equals(host.TrimEnd('.'),StringComparison.OrdinalIgnoreCase);
    }

    public static bool TrySplitPath(string? raw, out string slug,
        out string upstreamPath)
    {
        slug=""; upstreamPath="";
        if(string.IsNullOrEmpty(raw)||raw[0]!='/')return false;
        var next=raw.IndexOf('/',1);
        var value=next<0?raw[1..]:raw[1..next];
        if(!FavoriteHostNames.ValidSlug(value)||value.EndsWith('-')||
            value is "admin" or "auth" or "api" or "portal" or "tickets" ||
            value is "health" or "nginx-health")return false;
        slug=value;
        upstreamPath=next<0?"/":raw[next..];
        return true;
    }

    // Browser-side path compatibility is opt-in per slug. It never opens
    // origin-root /rest or /s on the shared host, and never changes routing
    // or cookies for any other favorite.
    public static bool BrowserShimEnabled(string? configured,string slug)
    {
        if(!FavoriteHostNames.ValidSlug(slug)||
           string.IsNullOrWhiteSpace(configured))return false;
        var enabled=false;
        foreach(var candidate in configured.Split(',',
            StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries))
        {
            if(!FavoriteHostNames.ValidSlug(candidate))
                throw new InvalidOperationException(
                    "PLENARO_FAVORITES_BROWSER_SHIM_SLUGS enthält einen ungültigen Slug.");
            if(candidate.Equals(slug,StringComparison.Ordinal))enabled=true;
        }
        return enabled;
    }

    public static string CookiePath(string slug)=>"/"+slug;
    public static string AppPath(string slug,string path)=>
        CookiePath(slug)+(path.StartsWith('/')?path:"/"+path);
    public static Uri AppOrigin(Uri root,string slug)=>
        new Uri(root,slug+"/");
}
