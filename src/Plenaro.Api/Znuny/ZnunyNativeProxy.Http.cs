using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Plenaro.Api.Znuny;

public sealed partial class ZnunyNativeProxy
{
    private const int MaxHtmlBytes=6*1024*1024;
    private static readonly Regex FormPattern=new(
        @"<form\b(?<attrs>[^>]*)>(?<body>.*?)</form\s*>",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(2));
    private static readonly Regex InputPattern=new(
        @"<input\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(2));
    private static readonly Regex AttributePattern=new(
        @"(?<name>[a-z_:][a-z0-9_:.-]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))",
        RegexOptions.Compiled|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(2));

    private static HttpClient CreateSessionClient()
    {
        // Reuse the existing SSRF-safe DNS+TCP validation. Unlike API clients,
        // the native GUI needs a separate CookieContainer for every user.
        var handler=ZnunyConnectionTester.SecureHandler();
        handler.UseCookies=true;
        handler.CookieContainer=new CookieContainer(256,64,8192);
        handler.AutomaticDecompression=DecompressionMethods.GZip|
            DecompressionMethods.Deflate|DecompressionMethods.Brotli;
        handler.MaxConnectionsPerServer=24;
        return new HttpClient(handler,disposeHandler:true){
            Timeout=TimeSpan.FromSeconds(40)
        };
    }

    private static bool Allowed(Uri origin,Uri target)=>
        target.Scheme==Uri.UriSchemeHttps &&
        target.Host.Equals(origin.Host,StringComparison.OrdinalIgnoreCase) &&
        target.Port==origin.Port &&
        string.IsNullOrEmpty(target.UserInfo);

    private sealed record LoginForm(Uri Action,List<KeyValuePair<string,string>> Fields);

    private enum LoginState { Automatic, Interactive }

    // Only short, non-sensitive category codes may be surfaced to the user.
    // Never forward upstream error bodies, redirect targets or authentication
    // form content. Credentials are never logged.
    private sealed class NativeLoginException(string code):InvalidOperationException(code)
    {
        public string Code {get;}=code;
    }

    private static string? Attribute(string input,string name)
    {
        foreach(Match match in AttributePattern.Matches(input)){
            if(!match.Groups["name"].Value.Equals(name,
                StringComparison.OrdinalIgnoreCase))continue;
            var raw=match.Groups["dq"].Success?match.Groups["dq"].Value:
                match.Groups["sq"].Success?match.Groups["sq"].Value:
                match.Groups["bare"].Value;
            return WebUtility.HtmlDecode(raw);
        }
        return null;
    }

    private static LoginForm? FindLoginForm(string html,Uri page,Uri origin,
        string username,string password)
    {
        foreach(Match form in FormPattern.Matches(html)){
            var fields=new List<KeyValuePair<string,string>>();
            var foundPassword=false;
            var foundUsername=false;
            foreach(Match input in InputPattern.Matches(form.Groups["body"].Value)){
                var attrs=input.Groups["attrs"].Value;
                var name=Attribute(attrs,"name");
                if(string.IsNullOrEmpty(name))continue;
                var type=Attribute(attrs,"type")??"text";
                if(type.Equals("password",StringComparison.OrdinalIgnoreCase)||
                    name.Equals("Password",StringComparison.OrdinalIgnoreCase)){
                    foundPassword=true;
                    fields.Add(new(name,password));
                }else if(name.Equals("User",StringComparison.OrdinalIgnoreCase)||
                    name.Equals("UserLogin",StringComparison.OrdinalIgnoreCase)){
                    foundUsername=true;
                    fields.Add(new(name,username));
                }else if(type.Equals("hidden",StringComparison.OrdinalIgnoreCase)||
                    type.Equals("submit",StringComparison.OrdinalIgnoreCase)){
                    fields.Add(new(name,Attribute(attrs,"value")??""));
                }
            }
            if(!foundUsername||!foundPassword)continue;
            var action=Attribute(form.Groups["attrs"].Value,"action")??page.AbsoluteUri;
            if(!Uri.TryCreate(page,action,out var destination)||
                !Allowed(origin,destination))
                throw new NativeLoginException("unsafe-form-target");
            var method=Attribute(form.Groups["attrs"].Value,"method")??"post";
            if(!method.Equals("post",StringComparison.OrdinalIgnoreCase))
                throw new NativeLoginException("unsupported-login-method");
            return new LoginForm(destination,fields);
        }
        return null;
    }

    private static async Task<(HttpResponseMessage Response,Uri Url)> SendAndFollowAsync(
        HttpClient client,Uri origin,Uri target,CancellationToken ct)
    {
        if(!Allowed(origin,target))throw new NativeLoginException("unsafe-redirect");
        for(var hop=0;hop<6;hop++){
            using var request=new HttpRequestMessage(HttpMethod.Get,target);
            var response=await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,ct);
            if((int)response.StatusCode is >=300 and <400 &&
                response.Headers.Location is { } location){
                var next=new Uri(target,location);
                response.Dispose();
                if(!Allowed(origin,next))throw new NativeLoginException("unsafe-redirect");
                target=next;
                continue;
            }
            return (response,target);
        }
        throw new NativeLoginException("redirect-loop");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content,
        int max,CancellationToken ct)
    {
        if(content.Headers.ContentLength>max)throw new InvalidOperationException();
        await using var stream=await content.ReadAsStreamAsync(ct);
        using var output=new MemoryStream();
        var buffer=new byte[32768];
        int read;
        while((read=await stream.ReadAsync(buffer,ct))>0){
            if(output.Length+read>max)throw new InvalidOperationException();
            output.Write(buffer,0,read);
        }
        return output.ToArray();
    }

    // Some upstream application gateways serve genuine agent HTML as
    // text/plain, application/octet-stream or without a Content-Type. Trust
    // the actual document shape, never the header alone; JSON and error
    // bodies must not be mistaken for successful agent pages.
    private static bool LooksLikeHtmlDocument(string text)
    {
        var prefix=text.AsSpan().TrimStart(" \t\r\n\uFEFF".AsSpan());
        if(prefix.IsEmpty||prefix[0]!='<')return false;
        var sample=prefix[..Math.Min(prefix.Length,8192)].ToString();
        return Regex.IsMatch(sample,
            @"<(?:!doctype\s+html\b|html\b|head\b|body\b|form\b|main\b|div\b|meta\b|title\b|script\b)",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    private static async Task<string?> ReadLoginHtmlAsync(HttpContent content,
        CancellationToken ct,bool required=true)
    {
        var bytes=await ReadLimitedAsync(content,MaxHtmlBytes,ct);
        if(bytes.Length==0){
            if(required)throw new NativeLoginException("page-empty");
            return null;
        }
        Encoding encoding=Encoding.UTF8;
        var charset=content.Headers.ContentType?.CharSet?.Trim('"');
        if(!string.IsNullOrEmpty(charset)){
            try{encoding=Encoding.GetEncoding(charset);}
            catch(ArgumentException){encoding=Encoding.UTF8;}
        }
        // Respect BOMs on UTF-8/UTF-16 installations; the decoding always
        // happens server-side and no raw response is logged or returned.
        using var stream=new MemoryStream(bytes,writable:false);
        using var reader=new StreamReader(stream,encoding,
            detectEncodingFromByteOrderMarks:true);
        var html=await reader.ReadToEndAsync(ct);
        if(LooksLikeHtmlDocument(html))return html;
        if(!required)return null;
        var trimmed=html.AsSpan().TrimStart(" \t\r\n\uFEFF".AsSpan());
        if(trimmed.StartsWith("{",StringComparison.Ordinal)||
            trimmed.StartsWith("[",StringComparison.Ordinal)||
            content.Headers.ContentType?.MediaType is
                "application/json" or "application/problem+json")
            throw new NativeLoginException("page-json");
        throw new NativeLoginException("page-not-html");
    }

    // The standard agent form can be submitted server-side, preserving the
    // initial challenge/cookie jar. For custom SSO/MFA/JS-only logins, keep
    // the already isolated upstream browser session and let the agent sign
    // in through the real HTML UI. Never invent a successful login.
    private static async Task<LoginState> LoginAsync(
        HttpClient client,Uri entry,Uri origin,
        string username,string password,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(35));
        var token=timeout.Token;
        var (landing,landingUrl)=await SendAndFollowAsync(client,origin,entry,token);
        using(landing){
            if(!landing.IsSuccessStatusCode)
                throw new NativeLoginException("page-http");
            var html=await ReadLoginHtmlAsync(landing.Content,token)
                ?? throw new NativeLoginException("page-not-html");
            var login=FindLoginForm(html,landingUrl,origin,username,password);
            if(login is null)
                return LoginState.Interactive; // e.g. custom SSO / pre-authenticated portal

            using var request=new HttpRequestMessage(HttpMethod.Post,login.Action){
                Content=new FormUrlEncodedContent(login.Fields)
            };
            request.Headers.Referrer=landingUrl;
            // Real browser form posts include Origin. Some upstream CSRF
            // defenses reject a credential POST when that header is absent.
            // Never copy an Origin provided by the end-user client.
            request.Headers.TryAddWithoutValidation("Origin",
                origin.GetLeftPart(UriPartial.Authority));
            request.Headers.TryAddWithoutValidation("Accept","text/html,application/xhtml+xml");
            using var submitted=await client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,token);
            if((int)submitted.StatusCode is >=300 and <400){
                if(submitted.Headers.Location is not { } location)
                    throw new NativeLoginException("login-redirect-missing");
                var next=new Uri(login.Action,location);
                if(!Allowed(origin,next))
                    throw new NativeLoginException("unsafe-redirect");
                var (result,finalUrl)=await SendAndFollowAsync(
                    client,origin,next,token);
                using(result){
                    if(!result.IsSuccessStatusCode)
                        throw new NativeLoginException("login-http");
                    var page=await ReadLoginHtmlAsync(result.Content,token,
                        required:false);
                    if(page is not null&&
                        FindLoginForm(page,finalUrl,origin,username,password) is not null)
                        return LoginState.Interactive;
                }
                return LoginState.Automatic;
            }
            if(submitted.StatusCode is HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
                return LoginState.Interactive;
            if(!submitted.IsSuccessStatusCode)
                throw new NativeLoginException("login-http");
            var submittedPage=await ReadLoginHtmlAsync(submitted.Content,token,
                required:false);
            if(submittedPage is not null&&
                FindLoginForm(submittedPage,login.Action,origin,username,password) is not null)
                return LoginState.Interactive;
            return LoginState.Automatic;
        }
    }

    private static string Rewrite(string text,Uri origin,Uri publicOrigin)
    {
        var source=origin.GetLeftPart(UriPartial.Authority);
        var destination=publicOrigin.GetLeftPart(UriPartial.Authority);
        // Native HTML, CSS, JS and JSON are served from the second origin.
        // Relative paths need no rewrite. Replace absolute canonical URLs,
        // escaped JSON/JS URLs and common percent-encoded URL forms.
        return text.Replace(source,destination,StringComparison.OrdinalIgnoreCase)
            .Replace(source.Replace("/","\\/"),
                destination.Replace("/","\\/"),StringComparison.OrdinalIgnoreCase)
            .Replace(Uri.EscapeDataString(source),
                Uri.EscapeDataString(destination),StringComparison.OrdinalIgnoreCase)
            .Replace(source.Replace("/","&#47;"),
                destination.Replace("/","&#47;"),StringComparison.OrdinalIgnoreCase)
            .Replace("//"+origin.Authority,
                "//"+publicOrigin.Authority,StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEditableText(MediaTypeHeaderValue? type)
    {
        var value=type?.MediaType??"";
        return value.StartsWith("text/",StringComparison.OrdinalIgnoreCase) ||
            value is "application/javascript" or "application/x-javascript" or
                "application/json" or "application/xml" or "application/xhtml+xml" or
                "image/svg+xml";
    }

    private static bool ApprovedBrowserMutation(HttpRequest request,Uri external)
    {
        if(HttpMethods.IsGet(request.Method)||HttpMethods.IsHead(request.Method))
            return true;
        // Protect the proxy cookie against cross-origin form/JS POSTs.
        if(request.Headers.TryGetValue("Origin",out var origin)&&
            !origin.ToString().Equals(external.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
            return false;
        if(request.Headers.TryGetValue("Sec-Fetch-Site",out var site)&&
            site.ToString() is not ("same-origin" or "none"))
            return false;
        if(request.Headers.TryGetValue("Referer",out var referer)&&
            Uri.TryCreate(referer.ToString(),UriKind.Absolute,out var from)&&
            !Allowed(external,from))
            return false;
        return true;
    }

    // A document navigation (including the CGI agent entry point) may
    // legitimately return HTML with an incorrect MIME header. Never perform
    // content sniffing on background assets, downloads or ticket attachments.
    private static bool IsDocumentNavigation(HttpRequest request,Uri target,Uri entry)
    {
        if(!HttpMethods.IsGet(request.Method)&&!HttpMethods.IsPost(request.Method))
            return false;
        var dest=request.Headers["Sec-Fetch-Dest"].ToString();
        if(dest is "document" or "iframe")return true;
        // Browser form submissions can load a full compose/action page.
        // Do not MIME-sniff background POST/AJAX calls, whose Accept header
        // can also include text/html even when the response is a fragment.
        if(HttpMethods.IsPost(request.Method))
            return request.Headers["Sec-Fetch-Mode"].ToString()=="navigate" &&
                request.Headers["Accept"].ToString().Contains("text/html",
                    StringComparison.OrdinalIgnoreCase);
        if(request.Headers["Accept"].ToString().Contains("text/html",
            StringComparison.OrdinalIgnoreCase))return true;
        // Fallback only for a plain entry URL. CGI requests with a query
        // may return attachments rather than navigable documents.
        return target.Query.Length==0 &&
            target.AbsolutePath.Equals(entry.AbsolutePath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool PotentialHtmlMime(string? media)=>media is null or
        "text/plain" or "text/xml" or "application/xml" or "application/octet-stream" or
        "application/x-octet-stream" or "binary/octet-stream";

    // For an octet-stream navigation, inspect only a small prefix first.
    // A non-HTML download must stream normally, even if it is much larger
    // than the maximum HTML rewrite size.
    private static async Task<byte[]?> ReadOpaqueNavigationAsync(
        HttpContent content,Stream destination,CancellationToken ct)
    {
        await using var source=await content.ReadAsStreamAsync(ct);
        var first=new byte[2048];
        var count=0;
        while(count<first.Length){
            var read=await source.ReadAsync(first.AsMemory(count),ct);
            if(read==0)break;
            count+=read;
        }
        if(!LooksLikeHtmlDocument(Encoding.UTF8.GetString(first,0,count))){
            await destination.WriteAsync(first.AsMemory(0,count),ct);
            await source.CopyToAsync(destination,ct);
            return null;
        }
        using var output=new MemoryStream();
        output.Write(first,0,count);
        var buffer=new byte[32768];
        int next;
        while((next=await source.ReadAsync(buffer,ct))>0){
            if(output.Length+next>MaxHtmlBytes)
                throw new InvalidOperationException("HTML document too large");
            output.Write(buffer,0,next);
        }
        return output.ToArray();
    }

    private async Task ProxyRequestAsync(HttpContext ctx,ProxySession session,
        Uri publicOrigin,CancellationToken ct)
    {
        if(ctx.Request.Method is not ("GET" or "POST" or "HEAD" or "PUT" or
            "PATCH" or "DELETE")||!ApprovedBrowserMutation(ctx.Request,publicOrigin)){
            ctx.Response.StatusCode=403;return;
        }
        var builder=new UriBuilder(session.Upstream){
            Path=ctx.Request.Path.ToUriComponent(),
            Query=ctx.Request.QueryString.ToUriComponent().TrimStart('?'),
            Fragment=""
        };
        var target=builder.Uri;
        if(!Allowed(session.Upstream,target)){
            ctx.Response.StatusCode=403;return;
        }
        using var request=new HttpRequestMessage(new HttpMethod(ctx.Request.Method),target);
        if(ctx.Request.ContentLength>0||ctx.Request.Headers.ContainsKey("Transfer-Encoding")){
            request.Content=new StreamContent(ctx.Request.Body);
            if(!string.IsNullOrEmpty(ctx.Request.ContentType))
                request.Content.Headers.TryAddWithoutValidation("Content-Type",ctx.Request.ContentType);
            if(ctx.Request.ContentLength is { } length)
                request.Content.Headers.ContentLength=length;
        }
        foreach(var name in new[]{"Accept","Accept-Language","User-Agent",
            "X-Requested-With","X-Requested-By","X-Requested-Action",
            "If-Modified-Since","If-None-Match","Range"}){
            if(ctx.Request.Headers.TryGetValue(name,out var value))
                request.Headers.TryAddWithoutValidation(name,value.ToArray());
        }
        if(ctx.Request.Headers.TryGetValue("Origin",out _))
            request.Headers.TryAddWithoutValidation("Origin",
                session.Upstream.GetLeftPart(UriPartial.Authority));
        if(ctx.Request.Headers.TryGetValue("Referer",out var referer)&&
            Uri.TryCreate(referer.ToString(),UriKind.Absolute,out var refererUrl)&&
            Allowed(publicOrigin,refererUrl)){
            request.Headers.Referrer=new Uri(session.Upstream,
                refererUrl.PathAndQuery);
        }
        try{
            using var response=await session.Client.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead,ct);
            ctx.Response.StatusCode=(int)response.StatusCode;
            // Forward only necessary response metadata. Never expose upstream
            // Set-Cookie, internal URLs, server identifiers or hop-by-hop headers.
            if(response.Headers.Location is { } redirect){
                var destination=new Uri(target,redirect);
                if(!Allowed(session.Upstream,destination)){
                    ctx.Response.StatusCode=502;return;
                }
                ctx.Response.Headers.Location=
                    publicOrigin.GetLeftPart(UriPartial.Authority)+destination.PathAndQuery;
            }
            if(response.Content.Headers.ContentDisposition is { } disposition)
                ctx.Response.Headers.ContentDisposition=disposition.ToString();
            if(response.Headers.TryGetValues("Accept-Ranges",out var ranges))
                ctx.Response.Headers["Accept-Ranges"]=string.Join(",",ranges);
            if(response.Content.Headers.ContentRange is { } range)
                ctx.Response.Headers.ContentRange=range.ToString();
            var type=response.Content.Headers.ContentType;
            if(type is not null)ctx.Response.ContentType=type.ToString();
            if(HttpMethods.IsHead(ctx.Request.Method)||response.StatusCode is
                HttpStatusCode.NotModified or HttpStatusCode.NoContent)
                return;
            // Static assets are user-private but can be cached briefly by
            // their own browser. HTML, JSON and ticket responses are never
            // cached across navigations or login changes.
            var staticAsset=HttpMethods.IsGet(ctx.Request.Method) &&
                response.IsSuccessStatusCode &&
                (type?.MediaType?.StartsWith("image/",StringComparison.OrdinalIgnoreCase)==true ||
                type?.MediaType is "text/css" or "font/woff" or
                    "font/woff2" or "application/font-woff");
            ctx.Response.Headers.CacheControl=staticAsset?
                "private,max-age=600":"private,no-store";
            var canSniffDocument=response.IsSuccessStatusCode &&
                response.Content.Headers.ContentDisposition is null &&
                IsDocumentNavigation(ctx.Request,target,session.Entry) &&
                PotentialHtmlMime(type?.MediaType);
            var sniffOpaqueDocument=canSniffDocument&&!IsEditableText(type);
            if(IsEditableText(type)||sniffOpaqueDocument){
                var bytes=sniffOpaqueDocument?
                    await ReadOpaqueNavigationAsync(
                        response.Content,ctx.Response.Body,ct):
                    await ReadLimitedAsync(response.Content,MaxHtmlBytes,ct);
                // An opaque non-HTML response was streamed unmodified.
                if(bytes is null)return;
                var charset=type?.CharSet?.Trim('"');
                Encoding encoding;
                try{encoding=charset is null?Encoding.UTF8:
                    Encoding.GetEncoding(charset);}
                catch(ArgumentException){encoding=Encoding.UTF8;}
                var decoded=encoding.GetString(bytes);
                // The upstream CGI page may be reported as plain text,
                // binary octet-stream or with no MIME at all. Only a verified
                // HTML document from a navigation gets the browser HTML MIME.
                // Unknown binary responses are passed through unchanged.
                if(canSniffDocument&&LooksLikeHtmlDocument(decoded)){
                    ctx.Response.ContentType="text/html; charset="+encoding.WebName;
                    ctx.Response.Headers.Remove("Content-Disposition");
                }else if(sniffOpaqueDocument){
                    await ctx.Response.Body.WriteAsync(bytes,ct);
                    return;
                }
                var changed=Rewrite(decoded,session.Upstream,publicOrigin);
                var output=encoding.GetBytes(changed);
                await ctx.Response.Body.WriteAsync(output,ct);
            }else{
                // Binary attachments, images and fonts never pass through a
                // text decoder; stream to keep memory bounded per user.
                await response.Content.CopyToAsync(ctx.Response.Body,ct);
            }
        }catch(Exception e) when(e is HttpRequestException or
            TaskCanceledException or InvalidOperationException or IOException){
            if(!ctx.Response.HasStarted)
                ctx.Response.StatusCode=502;
        }
    }
}
