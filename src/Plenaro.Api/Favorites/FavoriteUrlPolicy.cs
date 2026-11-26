using System.Net;
using Plenaro.Api.Znuny;

namespace Plenaro.Api.Favorites;

public static class FavoriteUrlPolicy
{
    public static bool AllowedHost(Uri url)
    {
        if(url.HostNameType==UriHostNameType.Dns)
            return !url.Host.TrimEnd('.').Equals("localhost",StringComparison.OrdinalIgnoreCase)
                && !url.Host.TrimEnd('.').EndsWith(".localhost",StringComparison.OrdinalIgnoreCase);
        if(url.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return false;
        // The shared connector checks the actual TCP peer again for every
        // connection. Explicit IP literals must pass exactly the same policy.
        return IPAddress.TryParse(url.IdnHost.Trim('[',']'),out var ip)
            && ZnunyConnectionTester.Allowed(ip);
    }

    public static bool ValidPath(string? path)
    {
        if(string.IsNullOrEmpty(path)||path.Length>2048||!path.StartsWith('/')||
           path.StartsWith("//",StringComparison.Ordinal)||path.Contains('\\')||
           path.Any(char.IsControl))return false;
        var root=new Uri("https://example.invalid/");
        if(!Uri.TryCreate(root,path,out var target)||
           target.Scheme!=root.Scheme||target.Host!=root.Host||
           target.Port!=root.Port||target.UserInfo.Length!=0||
           target.AbsolutePath.StartsWith("//",StringComparison.Ordinal)||
           target.AbsolutePath.Contains("://",StringComparison.Ordinal))return false;
        // Queries and hash-router fragments may themselves contain URLs.
        // They never change the actual HTTPS authority or TCP destination.
        return true;
    }

    public static bool TryPersonalUrl(string? raw,out string origin,out string path)
    {
        origin="";path="";
        if(string.IsNullOrWhiteSpace(raw)||raw.Length>2048||
           raw.Any(char.IsControl)||raw.Contains('\\')||
           !Uri.TryCreate(raw.Trim(),UriKind.Absolute,out var url)||
           url.Scheme!=Uri.UriSchemeHttps||url.UserInfo.Length!=0||
           !AllowedHost(url))return false;
        origin=url.GetLeftPart(UriPartial.Authority)+"/";
        path=url.PathAndQuery+url.Fragment;
        return ValidPath(path);
    }
}
