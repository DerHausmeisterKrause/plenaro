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
        bool Authenticated,string Code);

    internal sealed record AtossZkForm(
        string Desktop,string UpdatePath,string UsernameId,
        string PasswordId,string LoginId);

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
        if(username is null||password is null||login is null||
           username==password||username==login||password==login)return null;
        return new(desktop.Groups["id"].Value,ajax.PathAndQuery,
            username,password,login);
    }

    private static async Task<string?> ReadAtossPageAsync(
        HttpClient client,Uri uri,Uri? referer,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);
        if(referer is not null)request.Headers.Referrer=referer;
        using var reply=await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead,ct);
        if(!reply.IsSuccessStatusCode ||
           reply.Content.Headers.ContentLength>262144)return null;
        var body=await ReadLimitedAsync(reply.Content,262144,ct);
        var html=Encoding.UTF8.GetString(body);
        return LooksLikeHtml(html)?html:null;
    }

    // An AU packet uses the desktop and widget IDs from the SAME HTML
    // session. A single user/password change each precedes ONE button click.
    private static async Task<bool> SendAtossZkEventAsync(
        HttpClient client,Uri origin,Uri page,Uri update,string desktop,
        string widget,string action,string json,int sid,CancellationToken ct)
    {
        if(!SameUpstream(origin,update))return false;
        using var message=new HttpRequestMessage(HttpMethod.Post,update){
            Content=new FormUrlEncodedContent(new[]{
                new KeyValuePair<string,string>("dtid",desktop),
                new KeyValuePair<string,string>("cmd_0",action),
                // The ATOSS HAR marks interactive AU events important.
                new KeyValuePair<string,string>("opt_0","i"),
                new KeyValuePair<string,string>("uuid_0",widget),
                new KeyValuePair<string,string>("data_0",json)
            })
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
        // ZK replies are normally {"rs":[...],"rid":N}, with an empty
        // response for focus/field changes. Any server error is not accepted.
        return response.StartsWith("{\"rs\":",StringComparison.Ordinal) &&
            !response.Contains("\"error\"",StringComparison.OrdinalIgnoreCase) &&
            !response.Contains("wrongValue",StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<AtossLoginResult> TryAtossZkLoginAsync(
        HttpClient client,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        if(!AtossEntry(entry)||!SameUpstream(origin,entry))
            return new(false,false,false,"atoss-zk-not-applicable");
        try{
            var outer=await ReadAtossPageAsync(client,entry,null,ct);
            if(outer is null)return new(false,false,false,"atoss-zk-not-applicable");
            var child=FindAtossFrame(outer,entry,origin);
            if(child is null)return new(false,false,false,"atoss-zk-not-applicable");
            var html=await ReadAtossPageAsync(client,child,entry,ct);
            if(html is null)return new(true,false,false,"atoss-zk-login-page-unavailable");
            var form=ParseAtossZkForm(html,child,origin);
            if(form is null)return new(true,false,false,"atoss-zk-login-form-unavailable");
            if(!Uri.TryCreate(origin,form.UpdatePath,out var update)||
               !SameUpstream(origin,update))
                return new(true,false,false,"atoss-zk-login-form-unavailable");
            var sid=RandomNumberGenerator.GetInt32(100,30000);
            var changedUser=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.UsernameId,"onChange",
                JsonSerializer.Serialize(new {value=username}),sid,ct);
            if(!changedUser)
                return new(true,false,false,"atoss-zk-field-rejected");
            // A password is not sent if the username update failed.
            var changedPassword=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.PasswordId,"onChange",
                JsonSerializer.Serialize(new {value=password}),sid,ct);
            if(!changedPassword)
                return new(true,true,false,"atoss-zk-login-unconfirmed");
            var clicked=await SendAtossZkEventAsync(client,origin,child,
                update,form.Desktop,form.LoginId,"onClick",
                "{\"pageX\":0,\"pageY\":0,\"which\":1,\"x\":0,\"y\":0}",
                sid,ct);
            if(!clicked)
                return new(true,true,false,"atoss-zk-login-unconfirmed");
            // A 200 ZK response (or session cookie) is NOT proof of login:
            // re-enter the server session and require the login form to be
            // absent from an authentic ATOSS widget page.
            var checkOuter=await ReadAtossPageAsync(client,entry,null,ct);
            if(checkOuter is null)
                return new(true,true,false,"atoss-zk-login-unconfirmed");
            var nextFrame=FindAtossFrame(checkOuter,entry,origin);
            if(nextFrame is null)
                return new(true,true,false,"atoss-zk-login-unconfirmed");
            var checkHtml=await ReadAtossPageAsync(
                client,nextFrame,entry,ct);
            if(checkHtml is null||
               !checkHtml.Contains("ATOSS Staff Efficiency Suite",
                   StringComparison.OrdinalIgnoreCase) ||
               !checkHtml.Contains("zkmx(",StringComparison.Ordinal) ||
               ParseAtossZkForm(checkHtml,nextFrame,origin) is not null)
                return new(true,true,false,"atoss-zk-login-unconfirmed");
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
