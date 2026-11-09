using System.Text.RegularExpressions;

namespace Plenaro.Api.Favorites;

/// <summary>
/// Resolves isolated favorite origins. The optional new one-label hostname
/// template takes precedence for new sessions, while legacy slug.base-domain
/// hosts remain routable. No browser-supplied upstream addresses are accepted.
/// </summary>
public static class FavoriteHostNames
{
    private const string Marker = "{slug}";
    private static readonly Regex ValidSlugPattern = new(
        "^[a-z][a-z0-9-]{1,39}$", RegexOptions.CultureInvariant);

    public static bool ValidSlug(string? slug) =>
        slug is not null && ValidSlugPattern.IsMatch(slug);

    private static string? LegacyDomain(string? baseDomain)
    {
        if (string.IsNullOrWhiteSpace(baseDomain)) return null;
        var domain = baseDomain.Trim().TrimEnd('.').ToLowerInvariant();
        if (!domain.Contains('.') || Uri.CheckHostName(domain) != UriHostNameType.Dns ||
            domain.Contains('*'))
            throw new InvalidOperationException(
                "PLENARO_FAVORITES_BASE_DOMAIN muss eine gültige DNS-Domain sein.");
        return domain;
    }

    private static (string Prefix, string Suffix)? TemplateParts(string? template)
    {
        if (string.IsNullOrWhiteSpace(template)) return null;
        var pattern = template.Trim().ToLowerInvariant();
        var marker = pattern.IndexOf(Marker, StringComparison.Ordinal);
        if (marker < 0 || pattern.LastIndexOf(Marker, StringComparison.Ordinal) != marker)
            throw new InvalidOperationException(
                "PLENARO_FAVORITES_HOST_TEMPLATE benötigt genau einmal {slug}.");
        var prefix = pattern[..marker];
        var suffix = pattern[(marker + Marker.Length)..];
        // {slug} must be inside the FIRST DNS label. Only literal DNS
        // characters are allowed; schemes, ports, wildcards and paths are not.
        if (prefix.Contains('.') || !suffix.Contains('.') ||
            prefix.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-') ||
            suffix.Any(c => !char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) &&
                c != '-' && c != '.'))
            throw new InvalidOperationException(
                "PLENARO_FAVORITES_HOST_TEMPLATE muss ein DNS-Hostname mit {slug} in der ersten Ebene sein.");
        // Validate both a short and the maximum-length supported slug, so
        // a template cannot generate an invalid or deeper DNS name.
        foreach (var sample in new[] { "wiki", "w" + new string('x', 39) })
        {
            var host = prefix + sample + suffix;
            if (host.Length > 253 ||
                host.Split('.').Any(label => label.Length is < 1 or > 63 ||
                    label[0] == '-' || label[^1] == '-') ||
                Uri.CheckHostName(host) != UriHostNameType.Dns)
                throw new InvalidOperationException(
                    "PLENARO_FAVORITES_HOST_TEMPLATE erzeugt keinen gültigen DNS-Hostname.");
        }
        return (prefix, suffix);
    }

    public static Uri? PreferredOrigin(string? slug, string? baseDomain,
        string? hostTemplate)
    {
        if (!ValidSlug(slug)) return null;
        var parts = TemplateParts(hostTemplate);
        var domain = LegacyDomain(baseDomain);
        var host = parts is { } pattern
            ? pattern.Prefix + slug + pattern.Suffix
            : domain is not null ? slug + "." + domain : null;
        return host is not null && Uri.CheckHostName(host) == UriHostNameType.Dns
            ? new Uri("https://" + host) : null;
    }

    public static bool IsReservedHost(string requestHost, string? baseDomain,
        string? hostTemplate)
    {
        var host = requestHost.TrimEnd('.');
        var domain = LegacyDomain(baseDomain);
        if (domain is not null &&
            host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            return true;
        var parts = TemplateParts(hostTemplate);
        return parts is { } pattern &&
            host.Length > pattern.Prefix.Length + pattern.Suffix.Length &&
            host.StartsWith(pattern.Prefix, StringComparison.OrdinalIgnoreCase) &&
            host.EndsWith(pattern.Suffix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetSlug(string requestHost, string? baseDomain,
        string? hostTemplate, out string slug)
    {
        var host = requestHost.TrimEnd('.').ToLowerInvariant();
        var parts = TemplateParts(hostTemplate);
        if (parts is { } pattern &&
            host.Length > pattern.Prefix.Length + pattern.Suffix.Length &&
            host.StartsWith(pattern.Prefix, StringComparison.Ordinal) &&
            host.EndsWith(pattern.Suffix, StringComparison.Ordinal))
        {
            var value = host.Substring(pattern.Prefix.Length,
                host.Length - pattern.Prefix.Length - pattern.Suffix.Length);
            if (ValidSlug(value)) { slug = value; return true; }
        }
        var domain = LegacyDomain(baseDomain);
        if (domain is not null)
        {
            var suffix = "." + domain;
            if (host.EndsWith(suffix, StringComparison.Ordinal))
            {
                var value = host[..^suffix.Length];
                if (ValidSlug(value)) { slug = value; return true; }
            }
        }
        slug = "";
        return false;
    }
}
