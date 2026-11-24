using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Plenaro.Api.Favorites;

public sealed partial class FavoriteProxy
{
    // ATOSS Staff Efficiency Suite renders its login as ZK widgets, NOT an
    // HTML form. The authorized, session-specific zkmx payload provides the
    // desktop ID, AJAX URL and component IDs; never guess static IDs or URLs.
    // Credentials stay in the server vault and this private HttpClient.
    internal sealed record AtossLoginResult(bool Recognized,bool Submitted,
        bool Authenticated,string Code,int? Status=null);

    internal sealed record AtossZkForm(
        string Desktop,string UpdatePath,string UsernameId,
        string PasswordId,string LoginId,string DialogId);

    private const RegexOptions ZkRegex=RegexOptions.CultureInvariant|
        RegexOptions.Singleline;
    private static readonly TimeSpan ZkRegexTimeout=TimeSpan.FromMilliseconds(200);

    internal static bool AtossEntry(Uri entry)
        => entry.Scheme==Uri.UriSchemeHttps &&
           entry.AbsolutePath.EndsWith("/SES/html",StringComparison.OrdinalIgnoreCase);

    // The outer ATOSS page creates an iframe from the current server-side
    // UUID. Read only that exact, single same-origin relative URL.
    internal static Uri? FindAtossFrame(string html,Uri outer,Uri origin)
    {
        if(!html.Contains("ATOSS Staff Efficiency Suite",
                StringComparison.OrdinalIgnoreCase))return null;
        var matches=Regex.Matches(html,
            @"\biframeUrl\s*=\s*'(?<path>uuid[0-9a-fA-F-]{36}/html)'",
            ZkRegex,ZkRegexTimeout);
        if(matches.Count!=1 ||
           !Uri.TryCreate(outer,matches[0].Groups["path"].Value,
               out var result) || !SameUpstream(origin,result))
            return null;
        // Must be exactly the server-issued /SES/uuid.../html URL.
        return Regex.IsMatch(result.AbsolutePath,
            @"^/SES/uuid[0-9a-fA-F-]{36}/html$",
            RegexOptions.CultureInvariant,ZkRegexTimeout)?result:null;
    }

    private static string UnescapeZkUrl(string encoded)
    {
        return Regex.Replace(encoded,@"\\x(?<hex>[0-9a-fA-F]{2})",
            m=>((char)Convert.ToInt32(m.Groups["hex"].Value,16)).ToString(),
            RegexOptions.CultureInvariant,ZkRegexTimeout);
    }

    // Match widget declarations from the zkmx server-rendered tree, not
    // arbitrary script text or user-provided input. IDs must be unique.
    private static string? UniqueZkWidget(string html,string type,
        string property,string value)
    {
        var pattern=@"\['"+Regex.Escape(type)+
            @"','(?<id>[a-zA-Z0-9_-]{2,64})',\{(?<attrs>[^{}]{0,1536})\}";
        var ids=Regex.Matches(html,pattern,ZkRegex,ZkRegexTimeout)
            .Cast<Match>()
            .Where(m=>m.Groups["attrs"].Value.Contains(
                property+":'"+value+"'",StringComparison.Ordinal))
            .Select(m=>m.Groups["id"].Value).Distinct().ToArray();
        return ids.Length==1?ids[0]:null;
    }

    // The real ATOSS HAR sends onClientInfo + onDialogRendered before any
    // login events. The dialog ID is generated anew for each ZK session.
    private static string? FindAtossLoginDialog(string html)
    {
        var declarations=Regex.Matches(html,
            @"\['widgets\.layout\.DialogPanel','(?<id>[a-zA-Z0-9_-]{2,64})',\{(?<attrs>.{0,8192}?)\},\{\},\[",
            ZkRegex,ZkRegexTimeout);
        var ids=declarations.Cast<Match>()
            .Where(m=>m.Groups["attrs"].Value.Contains("modalDialog:true",
                StringComparison.Ordinal) &&
                m.Groups["attrs"].Value.Contains("$onDialogRendered:true",
                    StringComparison.Ordinal))
            .Select(m=>m.Groups["id"].Value).Distinct().ToArray();
        return ids.Length==1?ids[0]:null;
    }

    internal static AtossZkForm? ParseAtossZkForm(
        string html,Uri page,Uri origin)
    {
        if(!SameUpstream(origin,page) ||
           !html.Contains("ATOSS Staff Efficiency Suite",
               StringComparison.OrdinalIgnoreCase) ||
           !html.Contains("zkmx(",StringComparison.Ordinal) ||
           html.Contains("type:'new-password'",StringComparison.OrdinalIgnoreCase))
            return null;
        var desktop=Regex.Match(html,
            @"\bdt:'(?<id>z_[a-zA-Z0-9_]{1,64})'",
            ZkRegex,ZkRegexTimeout);
        var update=Regex.Match(html,
            @"\buu:'(?<path>[^']{8,512})'",
            ZkRegex,ZkRegexTimeout);
        if(!desktop.Success||!update.Success)return null;
        var updatePath=UnescapeZkUrl(update.Groups["path"].Value);
        if(!Uri.TryCreate(origin,updatePath,out var ajax) ||
           !SameUpstream(origin,ajax) ||
           !Regex.IsMatch(ajax.AbsolutePath,
               @"^/SES/uuid[0-9a-fA-F-]{36}/zkau[A-Za-z0-9]+(?:;jsessionid=[A-Za-z0-9._-]{1,128})?$",
               RegexOptions.CultureInvariant,ZkRegexTimeout) ||
           !ajax.AbsolutePath.StartsWith(
               page.AbsolutePath[..page.AbsolutePath.LastIndexOf('/')]+"/",
               StringComparison.Ordinal))
            return null;
        var username=UniqueZkWidget(html,"widgets.input.FocusTextbox",
            "placeholder","Benutzername");
        var password=UniqueZkWidget(html,"widgets.input.FocusTextbox",
            "placeholder","Kennwort");
        var login=UniqueZkWidget(html,"widgets.wgt.AtossDialogButton",
            "label","Anmelden");
        var dialog=FindAtossLoginDialog(html);
        if(username is null||password is null||login is null||
           dialog is null || username==password||username==login||
           password==login||dialog==username||dialog==password||
           dialog==login)return null;
        return new(desktop.Groups["id"].Value,ajax.PathAndQuery,
            username,password,login,dialog);
    }

    internal sealed record AtossPageResult(string? Html,string Code,int? Status);

    // The embedded iframe is opened by Chromium with a normal HTML
    // navigation request. The old server-side attempt sent neither Accept
    // nor User-Agent, although the working browser trace includes both.
    // Some ATOSS installations return a non-HTML response or redirect when
    // the request does not look like a navigation. Mirror only these
    // non-secret headers; the per-user CookieContainer remains authoritative.
    private static void ConfigureAtossNavigationHeaders(
        HttpRequestMessage request,Uri? referer)
    {
        request.Headers.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language",
            "de-DE,de;q=0.9,en;q=0.8");
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "+
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36");
        if(referer is not null)request.Headers.Referrer=referer;
    }

    // Use the real HTTP response status in the admin debug view, never the
    // body, Set-Cookie, URL or session identifier. SecureHandler deliberately
    // disables redirects: follow at most two explicit HTTPS same-origin
    // redirects within the ATOSS SES application.
    internal static async Task<AtossPageResult> ReadAtossPageDetailedAsync(
        HttpClient client,Uri origin,Uri uri,Uri? referer,CancellationToken ct)
    {
        if(!SameUpstream(origin,uri) ||
           !uri.AbsolutePath.StartsWith("/SES/",StringComparison.Ordinal))
            return new(null,"atoss-zk-frame-navigation-blocked",null);
        for(var redirectCount=0;redirectCount<=2;redirectCount++){
            using var request=new HttpRequestMessage(HttpMethod.Get,uri);
            ConfigureAtossNavigationHeaders(request,referer);
            using var reply=await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,ct);
            var status=(int)reply.StatusCode;
            if(reply.StatusCode is HttpStatusCode.MovedPermanently or
                HttpStatusCode.Redirect or HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect){
                if(reply.Headers.Location is not { } location ||
                   !Uri.TryCreate(uri,location,out var next) ||
                   !SameUpstream(origin,next) ||
                   !next.AbsolutePath.StartsWith("/SES/",StringComparison.Ordinal))
                    return new(null,"atoss-zk-frame-redirect-blocked",status);
                referer=uri;
                uri=next;
                continue;
            }
            if(!reply.IsSuccessStatusCode)
                return new(null,"atoss-zk-frame-http-error",status);
            if(reply.Content.Headers.ContentLength>262144)
                return new(null,"atoss-zk-frame-too-large",status);
            var bytes=await ReadLimitedAsync(reply.Content,262144,ct);
            var html=Encoding.UTF8.GetString(bytes);
            return LooksLikeHtml(html)
                ?new(html,"atoss-zk-frame-ok",status)
                :new(null,"atoss-zk-frame-not-html",status);
        }
        return new(null,"atoss-zk-frame-redirect-limit",null);
    }

    private static async Task<string?> ReadAtossPageAsync(
        HttpClient client,Uri origin,Uri uri,Uri? referer,CancellationToken ct)
        => (await ReadAtossPageDetailedAsync(
            client,origin,uri,referer,ct)).Html;

    // ZK's actual browser protocol: first onClientInfo and
    // onDialogRendered, then onDialogFocus. Only then send credentials.
    private static async Task<bool> InitializeAtossZkDialogAsync(
        HttpClient client,Uri origin,Uri page,Uri update,
        AtossZkForm form,int sid,CancellationToken ct)
    {
        var clientInfo=JsonSerializer.Serialize(new Dictionary<string,object>{
            [""]=new object[]{0,1280,800,24,1280,720,0,0,"1.0","landscape"}
        });
        var initialized=await SendAtossZkPacketAsync(
            client,origin,page,update,new[]{
                new KeyValuePair<string,string>("dtid",form.Desktop),
                new KeyValuePair<string,string>("cmd_0","onClientInfo"),
                new KeyValuePair<string,string>("opt_0","i"),
                new KeyValuePair<string,string>("data_0",clientInfo),
                new KeyValuePair<string,string>("cmd_1","onDialogRendered"),
                new KeyValuePair<string,string>("uuid_1",form.DialogId),
                new KeyValuePair<string,string>("data_1","{}")
            },sid,ct);
        if(!initialized)return false;
        return await SendAtossZkPacketAsync(
            client,origin,page,update,new[]{
                new KeyValuePair<string,string>("dtid",form.Desktop),
                new KeyValuePair<string,string>("cmd_0","onDialogFocus"),
                new KeyValuePair<string,string>("opt_0","i"),
                new KeyValuePair<string,string>("uuid_0",form.DialogId),
                new KeyValuePair<string,string>("data_0",
                    JsonSerializer.Serialize(new{
                        activeComponent=form.UsernameId
                    }))
            },sid,ct);
    }

    // Same ZK-SID across the ordered events of one desktop (per browser HAR).
    // ZK's response arrays may contain JS objects, not standard JSON.
    private static async Task<bool> SendAtossZkPacketAsync(
        HttpClient client,Uri origin,Uri page,Uri update,
        IEnumerable<KeyValuePair<string,string>> fields,
        int sid,CancellationToken ct)
    {
        if(!SameUpstream(origin,update))return false;
        using var message=new HttpRequestMessage(HttpMethod.Post,update){
            Content=new FormUrlEncodedContent(fields)
        };
        message.Headers.Referrer=page;
        message.Headers.TryAddWithoutValidation("Origin",
            origin.GetLeftPart(UriPartial.Authority));
        message.Headers.TryAddWithoutValidation("X-Requested-With","XMLHttpRequest");
        message.Headers.TryAddWithoutValidation("ZK-SID",sid.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        using var reply=await client.SendAsync(message,
            HttpCompletionOption.ResponseHeadersRead,ct);
        if(!reply.IsSuccessStatusCode ||
           reply.Content.Headers.ContentLength>65536)return false;
        var body=await ReadLimitedAsync(reply.Content,65536,ct);
        var response=Encoding.UTF8.GetString(body);
        return response.StartsWith("{\"rs\":[",StringComparison.Ordinal) &&
            response.Contains("\"rid\":",StringComparison.Ordinal) &&
            !response.Contains("\"error\"",StringComparison.OrdinalIgnoreCase) &&
            !response.Contains("wrongValue",StringComparison.OrdinalIgnoreCase);
    }

    private static Task<bool> SendAtossZkEventAsync(
        HttpClient client,Uri origin,Uri page,Uri update,string desktop,
        string widget,string action,string json,int sid,CancellationToken ct)
        => SendAtossZkPacketAsync(client,origin,page,update,new[]{
            new KeyValuePair<string,string>("dtid",desktop),
            new KeyValuePair<string,string>("cmd_0",action),
            new KeyValuePair<string,string>("opt_0","i"),
            new KeyValuePair<string,string>("uuid_0",widget),
            new KeyValuePair<string,string>("data_0",json)
        },sid,ct);

    internal static async Task<AtossLoginResult> TryAtossZkLoginAsync(
        HttpClient client,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        if(!AtossEntry(entry)||!SameUpstream(origin,entry))
            return new(false,false,false,"atoss-zk-not-applicable");
        try{
            var outer=await ReadAtossPageAsync(client,origin,entry,null,ct);
            if(outer is null)return new(false,false,false,"atoss-zk-not-applicable");
            var child=FindAtossFrame(outer,entry,origin);
            if(child is null)return new(false,false,false,"atoss-zk-not-applicable");
            var page=await ReadAtossPageDetailedAsync(
                client,origin,child,entry,ct);
            if(page.Html is null)
                return new(true,false,false,page.Code,page.Status);
            var form=ParseAtossZkForm(page.Html,child,origin);
            if(form is null)return new(true,false,false,"atoss-zk-login-form-unavailable");
            if(!Uri.TryCreate(origin,form.UpdatePath,out var update)||
               !SameUpstream(origin,update))
                return new(true,false,false,"atoss-zk-login-form-unavailable");
            var sid=RandomNumberGenerator.GetInt32(100,30000);
            if(!await InitializeAtossZkDialogAsync(
                   client,origin,child,update,form,sid,ct))
                return new(true,false,false,"atoss-zk-dialog-initialization-failed");
            var changedUser=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.UsernameId,"onChange",
                JsonSerializer.Serialize(new {value=username}),sid,ct);
            if(!changedUser)
                return new(true,false,false,"atoss-zk-username-rejected");
            // A password is not sent if the username update failed.
            var changedPassword=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.PasswordId,"onChange",
                JsonSerializer.Serialize(new {value=password}),sid,ct);
            if(!changedPassword)
                return new(true,true,false,"atoss-zk-password-rejected");
            var clicked=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.LoginId,"onClick",
                "{\"pageX\":0,\"pageY\":0,\"which\":1,\"x\":0,\"y\":0}",
                sid,ct);
            if(!clicked)
                return new(true,true,false,"atoss-zk-click-rejected");
            // Keep the SAME authenticated desktop for the first check.
            // Loading /SES/html creates a new UUID and could reset the
            // ZK session, masking even a successfully submitted login.
            var checkHtml=await ReadAtossPageAsync(
                client,origin,child,entry,ct);
            if(checkHtml is null||
               !checkHtml.Contains("ATOSS Staff Efficiency Suite",
                   StringComparison.OrdinalIgnoreCase)||
               !checkHtml.Contains("zkmx(",StringComparison.Ordinal)||
               ParseAtossZkForm(checkHtml,child,origin) is not null)
                return new(true,true,false,"atoss-zk-session-unconfirmed");
            return new(true,true,true,"atoss-zk-login-confirmed");
        }catch(Exception ex) when(ex is HttpRequestException or
            InvalidOperationException or TaskCanceledException or
            RegexMatchTimeoutException && !ct.IsCancellationRequested){
            // It may have submitted credentials before transport broke;
            // do not send the password again through another login strategy.
            return new(true,true,false,"atoss-zk-login-unconfirmed");
        }
    }
}
