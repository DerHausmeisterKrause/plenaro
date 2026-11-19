using System.Net;
using System.Text;
using System.Text.Json;

namespace Plenaro.Api.Favorites;

/// <summary>
/// Optional one-shot DOM login on a PRIVATE Docker-network browser worker.
/// Only the already-approved upstream HTTPS origin and a server-side vault
/// password are sent to this worker. Never inject credentials into the shared
/// apps-plenaro browser origin. The authenticated upstream session cookies
/// are copied into the existing per-user HttpClient cookie container.
/// </summary>
internal static class FavoriteHeadlessLogin
{
    private const string WorkerUrl="http://favorites-login:3030/login";
    private static readonly HttpClient WorkerClient=new(){
        Timeout=TimeSpan.FromSeconds(30)
    };

    internal sealed record WorkerCookie(
        string Name,string Value,string Domain,string Path,
        bool Secure,bool HttpOnly);
    private sealed record BrowserRequest(
        string Url,string Username,string Password,WorkerCookie[] Cookies);
    private sealed record BrowserResponse(bool Authenticated,
        WorkerCookie[]? Cookies,string? Reason);

    /// <returns>
    /// true = browser worker responded, login attempted (do not also submit a
    /// classic form, even on failure); false = worker not running, so retain
    /// the pre-existing classic HTML fallback.
    /// </returns>
    internal static async Task<bool> TryAsync(
        CookieContainer jar,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        if(origin.Scheme!=Uri.UriSchemeHttps || entry.Scheme!=Uri.UriSchemeHttps ||
           !entry.Host.Equals(origin.Host,StringComparison.OrdinalIgnoreCase) ||
           entry.Port!=origin.Port || !string.IsNullOrEmpty(entry.UserInfo) ||
           !string.IsNullOrEmpty(origin.UserInfo))
            return false;
        var cookies=jar.GetCookies(origin).Cast<Cookie>()
            .Where(c=>ValidCookie(origin,new(c.Name,c.Value,c.Domain,
                c.Path,c.Secure,c.HttpOnly)))
            .Select(c=>new WorkerCookie(c.Name,c.Value,c.Domain,c.Path,
                c.Secure,c.HttpOnly)).ToArray();
        using var request=new HttpRequestMessage(HttpMethod.Post,WorkerUrl){
            Content=new StringContent(JsonSerializer.Serialize(new BrowserRequest(
                entry.AbsoluteUri,username,password,cookies),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                Encoding.UTF8,"application/json")
        };
        HttpResponseMessage reply;
        try{
            reply=await WorkerClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,ct);
        }catch(HttpRequestException) {
            // Browser-login profile not running: preserve standard HTML login.
            return false;
        }
        using(reply){
            // Worker replied: never do a second login attempt in this session.
            if(!reply.IsSuccessStatusCode)return true;
            if(reply.Content.Headers.ContentLength>64*1024)return true;
            try{
                using var stream=await reply.Content.ReadAsStreamAsync(ct);
                var result=await JsonSerializer.DeserializeAsync<BrowserResponse>(
                    stream,new JsonSerializerOptions{
                        PropertyNameCaseInsensitive=true
                    },ct);
                if(result?.Authenticated!=true || result.Cookies is null ||
                   result.Cookies.Length>64)return true;
                foreach(var cookie in result.Cookies){
                    if(!ValidCookie(origin,cookie))continue;
                    try{
                        jar.Add(origin,new Cookie(cookie.Name,cookie.Value,
                            cookie.Path){Secure=cookie.Secure,
                            HttpOnly=cookie.HttpOnly});
                    }catch(CookieException){/* Invalid cookie cannot escape worker. */}
                }
            }catch(JsonException){/* Untrusted worker response, no fallback. */}
            return true;
        }
    }

    internal static bool ValidCookie(Uri origin,WorkerCookie cookie)
    {
        if(cookie is null || cookie.Value is null ||
           cookie.Domain is null || cookie.Path is null ||
           string.IsNullOrWhiteSpace(cookie.Name)||
           cookie.Name.Length>256 || cookie.Value.Length>4096 ||
           cookie.Path.Length is <1 or >512 ||
           !cookie.Path.StartsWith('/') ||
           cookie.Name.Any(c=>char.IsControl(c)||c is ';' or '=' or ' ')||
           cookie.Value.Any(c=>char.IsControl(c)||c is ';' or '\r' or '\n')||
           cookie.Path.Any(char.IsControl))
            return false;
        // No third-party cookies, parent-domain escalation or HTTP sessions.
        return cookie.Secure &&
            cookie.Domain.TrimStart('.').Equals(origin.Host,
                StringComparison.OrdinalIgnoreCase);
    }
}
