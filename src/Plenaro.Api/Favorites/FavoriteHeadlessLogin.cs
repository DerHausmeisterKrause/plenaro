using System.Net;
using System.Net.Sockets;
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
    // A browser response is not synonymous with a successful login. Report
    // only predefined reason codes, never an upstream error or worker payload.
    internal sealed record BrowserAttempt(bool Attempted,bool Authenticated,string Code);

    internal static string ClassifyReason(string? reason)=>reason switch
    {
        "no-login-fields" => "browser-no-login-fields",
        "no-login-action" => "browser-no-login-action",
        "not-confirmed" => "browser-login-not-confirmed",
        "foreign-origin" => "browser-foreign-login",
        "no-session-cookies" => "browser-no-session-cookies",
        "blocked-target" => "browser-target-blocked",
        "browser-unavailable" => "browser-worker-error",
        "busy" => "browser-worker-busy",
        _ => "browser-login-not-confirmed"
    };

    // Keep the original boolean contract for old callers: true means the
    // worker MIGHT have posted credentials, not that it authenticated.
    internal static async Task<bool> TryAsync(
        CookieContainer jar,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
        => (await TryDetailedAsync(jar,origin,entry,username,password,ct)).Attempted;

    internal static async Task<BrowserAttempt> TryDetailedAsync(
        CookieContainer jar,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        if(origin.Scheme!=Uri.UriSchemeHttps || entry.Scheme!=Uri.UriSchemeHttps ||
           !entry.Host.Equals(origin.Host,StringComparison.OrdinalIgnoreCase) ||
           entry.Port!=origin.Port || !string.IsNullOrEmpty(entry.UserInfo) ||
           !string.IsNullOrEmpty(origin.UserInfo))
            return new(false,false,"browser-target-blocked");
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
        }catch(HttpRequestException error)
            when(error.InnerException is SocketException socket &&
                socket.SocketErrorCode is SocketError.HostNotFound or
                    SocketError.TryAgain or SocketError.ConnectionRefused){
            return new(false,false,"browser-worker-unreachable");
        }catch(HttpRequestException){
            // Worker may have already submitted a login: no double POST.
            return new(true,false,"browser-transport-uncertain");
        }
        using(reply){
            if(!reply.IsSuccessStatusCode)
                return new(true,false,"browser-worker-error");
            if(reply.Content.Headers.ContentLength>64*1024)
                return new(true,false,"browser-worker-error");
            try{
                using var stream=await reply.Content.ReadAsStreamAsync(ct);
                var result=await JsonSerializer.DeserializeAsync<BrowserResponse>(
                    stream,new JsonSerializerOptions{
                        PropertyNameCaseInsensitive=true
                    },ct);
                if(result?.Authenticated!=true &&
                   result?.Reason is "no-login-fields" or "no-login-action")
                    return new(false,false,ClassifyReason(result.Reason));
                if(result?.Authenticated!=true || result.Cookies is null ||
                   result.Cookies.Length>64)
                    return new(true,false,ClassifyReason(result?.Reason));
                var imported=0;
                foreach(var cookie in result.Cookies){
                    if(!ValidCookie(origin,cookie))continue;
                    try{
                        jar.Add(origin,new Cookie(cookie.Name,cookie.Value,
                            cookie.Path){Secure=true,
                            HttpOnly=cookie.HttpOnly});
                        imported++;
                    }catch(CookieException){/* Invalid cookie never escapes worker. */}
                }
                return imported>0
                    ?new(true,true,"browser-login-confirmed")
                    :new(true,false,"browser-no-valid-session-cookies");
            }catch(JsonException){return new(true,false,"browser-worker-error");}
            catch(IOException){return new(true,false,"browser-transport-uncertain");}
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
        var domain=cookie.Domain.TrimStart('.');
        if(domain.Length<3||domain.Count(ch=>ch=='.')<1)return false;
        // An upstream site may use a parent-domain session cookie
        // (e.g. atoss.example.org from app.atoss.example.org).
        // The imported cookie is nevertheless host-only in this dedicated
        // HttpClient jar, never forwarded to another upstream.
        return (domain.Equals(origin.Host,StringComparison.OrdinalIgnoreCase) ||
             origin.Host.EndsWith("."+domain,
                 StringComparison.OrdinalIgnoreCase));
    }
}
