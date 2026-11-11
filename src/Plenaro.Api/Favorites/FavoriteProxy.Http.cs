using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Plenaro.Api.Favorites;

public sealed partial class FavoriteProxy
{
    private static readonly Regex FormRegex=new(
        @"<form\b(?<attrs>[^>]*)>(?<body>.*?)</form\s*>",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));
    private static readonly Regex InputRegex=new(
        @"<input\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));
    private static readonly Regex AttributeRegex=new(
        @"(?<name>[a-z_:][a-z0-9_:.-]*)\s*=\s*(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))",
        RegexOptions.Compiled|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static string? Attribute(string attrs,string key)
    {
        foreach(Match match in AttributeRegex.Matches(attrs)){
            if(!match.Groups["name"].Value.Equals(key,StringComparison.OrdinalIgnoreCase))
                continue;
            var raw=match.Groups["dq"].Success?match.Groups["dq"].Value:
                match.Groups["sq"].Success?match.Groups["sq"].Value:
                match.Groups["bare"].Value;
            return WebUtility.HtmlDecode(raw);
        }
        return null;
    }

    // Standard HTML username/password forms only. Non-standard sign-in
    // (SSO, 2FA, JavaScript-generated fields) remains interactive. Credentials
    // are NEVER emitted to the browser, GET query or another upstream origin.
    private static async Task TryLoginAsync(HttpClient client,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        using var initial=new HttpRequestMessage(HttpMethod.Get,entry);
        using var response=await client.SendAsync(initial,
            HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode ||
           response.Content.Headers.ContentLength>262144)return;
        var bytes=await ReadLimitedAsync(response.Content,262144,ct);
        var html=Encoding.UTF8.GetString(bytes);
        if(!LooksLikeHtml(html))return;
        foreach(Match form in FormRegex.Matches(html)){
            if(Attribute(form.Groups["attrs"].Value,"method") is { } method &&
                !method.Equals("post",StringComparison.OrdinalIgnoreCase))
                continue;
            var names=new List<KeyValuePair<string,string>>();
            var userFound=false;
            var passwordFound=false;
            foreach(Match input in InputRegex.Matches(form.Groups["body"].Value)){
                var attrs=input.Groups["attrs"].Value;
                var name=Attribute(attrs,"name");
                if(string.IsNullOrEmpty(name))continue;
                var type=Attribute(attrs,"type")?.ToLowerInvariant()??"text";
                if(type=="password"){
                    names.Add(new(name,password));passwordFound=true;
                }else if(type=="text"||type=="email"||type==""){
                    if(name.Equals("User",StringComparison.OrdinalIgnoreCase)||
                       name.Equals("username",StringComparison.OrdinalIgnoreCase)||
                       name.Equals("login",StringComparison.OrdinalIgnoreCase)||
                       name.Equals("UserLogin",StringComparison.OrdinalIgnoreCase)||
                       name.Equals("email",StringComparison.OrdinalIgnoreCase)){
                        names.Add(new(name,username));userFound=true;
                    }
                }else if(type=="hidden"||type=="submit"){
                    names.Add(new(name,Attribute(attrs,"value")??""));
                }
            }
            if(!userFound||!passwordFound)continue;
            var action=Attribute(form.Groups["attrs"].Value,"action")??"/";
            if(!Uri.TryCreate(entry,action,out var target)||
                !SameUpstream(origin,target))return;
            using var req=new HttpRequestMessage(HttpMethod.Post,target){
                Content=new FormUrlEncodedContent(names)
            };
            using var result=await client.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead,ct);
            if(result.Headers.Location is { } next){
                var redirect=new Uri(target,next);
                if(!SameUpstream(origin,redirect))return;
                using var follow=new HttpRequestMessage(HttpMethod.Get,redirect);
                using var confirmed=await client.SendAsync(follow,
                    HttpCompletionOption.ResponseHeadersRead,ct);
            }
            return;
        }
    }

    private static bool LooksLikeHtml(string html)
    {
        var sample=html.AsSpan().TrimStart(" \r\n\t\uFEFF".AsSpan());
        if(sample.IsEmpty||sample[0]!='<')return false;
        return Regex.IsMatch(sample[..Math.Min(4096,sample.Length)].ToString(),
            @"<(?:!doctype\s+html\b|html\b|head\b|body\b|form\b|div\b|script\b)",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    // Rewrite only URL-shaped root paths. A shared path-host cannot serve
    // /assets/... directly: every request must remain under /<slug>/.
    // Keep protocol-relative CDN URLs and already-prefixed URLs unchanged.
    private static string Rewrite(string source,Uri upstream,Uri external,
        string pathPrefix="")
    {
        var from=upstream.GetLeftPart(UriPartial.Authority);
        var to=external.GetLeftPart(UriPartial.Authority)+pathPrefix;
        var result=source.Replace(from,to,StringComparison.OrdinalIgnoreCase)
            .Replace(from.Replace("/","\\/"),to.Replace("/","\\/"),
                StringComparison.OrdinalIgnoreCase)
            .Replace(Uri.EscapeDataString(from),Uri.EscapeDataString(to),
                StringComparison.OrdinalIgnoreCase)
            .Replace("//"+upstream.Authority,
                "//"+external.Authority+pathPrefix,
                StringComparison.OrdinalIgnoreCase);
        if(pathPrefix.Length==0)return result;
        var prefix=Regex.Escape(pathPrefix.TrimStart('/'));
        // HTML attributes, CSS strings, JS imports/fetch and template strings.
        // Check delimiter boundaries so /wiki, /wiki/, /wiki?x and /wiki#id
        // never acquire a second prefix.
        const string boundary=@"(?:[/""'\x60?#\s)>]|$)";
        result=Regex.Replace(result,
            @"(?<quote>[""'\x60])/(?!/|"+prefix+boundary+@")",
            match=>match.Groups["quote"].Value+pathPrefix+"/",
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        // CSS url(/font.woff2), including unquoted and whitespace variants.
        return Regex.Replace(result,
            @"(?<url>url\(\s*[""']?)/(?!/|"+prefix+boundary+@")",
            match=>match.Groups["url"].Value+pathPrefix+"/",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    // Unquoted HTML attributes and secondary srcset candidates are not
    // covered by the text/JS/CSS string rules above. Limit these rules to
    // HTML; never rewrite arbitrary JS identifiers or binary responses.
    private static string RewriteHtmlResources(string source,string pathPrefix)
    {
        if(pathPrefix.Length==0)return source;
        var prefix=Regex.Escape(pathPrefix.TrimStart('/'));
        const string boundary=@"(?:[/""'\x60?#\s)>]|$)";
        var result=Regex.Replace(source,
            @"(?<attr>\b(?:href|src|action|poster|data-src|data-href|xlink:href)\s*=\s*)/(?!/|"+
                prefix+boundary+@")(?<path>[^\s>""'\x60]*)",
            m=>m.Groups["attr"].Value+pathPrefix+"/"+m.Groups["path"].Value,
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        return Regex.Replace(result,
            @"(?<attr>\bsrcset\s*=\s*)(?<q>[""'])(?<values>.*?)\k<q>",
            m=>{
                var candidates=Regex.Replace(m.Groups["values"].Value,
                    @"(?<start>^|,\s*)/(?!/|"+prefix+boundary+@")",
                    x=>x.Groups["start"].Value+pathPrefix+"/",
                    RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
                return m.Groups["attr"].Value+m.Groups["q"].Value+
                    candidates+m.Groups["q"].Value;
            },RegexOptions.IgnoreCase|RegexOptions.Singleline|
              RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    }

    // The HTML rewriter also runs on HTML that embeds inline JS. Preserve
    // raw script bodies: they can contain regex literals and other JS syntax
    // that must never pass through global string/URL replacement. The script
    // opening tag remains eligible for src= rewriting. No script is executed.
    private static string RewriteHtmlPreservingScripts(string source,
        Uri upstream,Uri external,string pathPrefix)
    {
        var bodies=new List<string>();
        var masked=Regex.Replace(source,
            @"(?<open><script\b[^>]*>)(?<body>.*?)(?<close></script\s*>)",
            match=>{
                var marker="\uE000plenaro-script-"+bodies.Count+"\uE001";
                if(source.Contains(marker,StringComparison.Ordinal))
                    throw new InvalidOperationException("Unsafe HTML script marker collision.");
                bodies.Add(match.Groups["body"].Value);
                return match.Groups["open"].Value+marker+match.Groups["close"].Value;
            },RegexOptions.IgnoreCase|RegexOptions.Singleline|
              RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        var rewritten=Rewrite(masked,upstream,external,pathPrefix);
        if(pathPrefix.Length>0){
            rewritten=RewriteHtmlResources(rewritten,pathPrefix);
            rewritten=RewriteHtmlAppContext(rewritten,pathPrefix);
        }
        for(var i=0;i<bodies.Count;i++)
            rewritten=rewritten.Replace("\uE000plenaro-script-"+i+"\uE001",
                bodies[i],StringComparison.Ordinal);
        return rewritten;
    }

    // Confluence (and some Atlassian plugins) constructs AJAX and web-resource
    // URLs from these meta tags, not from the resource URLs in HTML. An empty
    // context path is correct on the upstream origin, but not on /<slug>/.
    // Only alter these specific metadata tags in HTML, never arbitrary values,
    // JSON responses, or third-party URLs.
    private static string RewriteHtmlAppContext(string source,string pathPrefix)
    {
        if(pathPrefix.Length==0)return source;
        return Regex.Replace(source,@"<meta\b[^>]*>",
            meta=>{
                var name=Attribute(meta.Value,"name");
                if(name is null ||
                   (!name.Equals("ajs-context-path",StringComparison.OrdinalIgnoreCase)&&
                    !name.Equals("confluence-context-path",StringComparison.OrdinalIgnoreCase)))
                    return meta.Value;
                return Regex.Replace(meta.Value,
                    @"(?<key>\bcontent\s*=\s*)(?:""(?<dq>[^""]*)""|'(?<sq>[^']*)'|(?<bare>[^\s>]+))",
                    value=>{
                        var old=WebUtility.HtmlDecode(
                            value.Groups["dq"].Success?value.Groups["dq"].Value:
                            value.Groups["sq"].Success?value.Groups["sq"].Value:
                            value.Groups["bare"].Value);
                        string corrected;
                        if(old is "" or "/")corrected=pathPrefix;
                        else if(old==pathPrefix ||
                            old.StartsWith(pathPrefix+"/",StringComparison.Ordinal))
                            return value.Value;
                        else if(old.StartsWith('/')&&!old.StartsWith("//"))
                            corrected=pathPrefix+old;
                        else return value.Value;
                        return value.Groups["key"].Value+"\""+
                            WebUtility.HtmlEncode(corrected)+"\"";
                    },RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
            },RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }

    // HTML/API JSON remains deliberately bounded at 6 MiB. Some application
    // web-resource JavaScript/CSS bundles exceed that size, which previously
    // caused 413 and broke complete toolbars/forms. Give only script/style
    // bundles a separate, bounded cap rather than disabling size checks.
    private const int MaxStyleScriptBytes=24*1024*1024;
    private static int RewriteLimit(MediaTypeHeaderValue? type)
    {
        var media=type?.MediaType??"";
        if(media.Equals("text/css",StringComparison.OrdinalIgnoreCase)||
           media.Equals("text/javascript",StringComparison.OrdinalIgnoreCase)||
           media.Equals("application/javascript",StringComparison.OrdinalIgnoreCase)||
           media.Equals("application/x-javascript",StringComparison.OrdinalIgnoreCase)||
           media.Equals("application/ecmascript",StringComparison.OrdinalIgnoreCase)||
           media.Equals("application/x-ecmascript",StringComparison.OrdinalIgnoreCase))
            return MaxStyleScriptBytes;
        return MaxTextBytes;
    }

    // Never rewrite executable JavaScript, JSON or unknown text. The broad
    // URL-regex rewriting cannot distinguish a JS regex literal (/"/g) from
    // a quoted URL. It produced invalid Confluence contextbatch/batch.js
    // (/"/wiki/g), preventing AJS/AMD initialization and whole UI sections.
    // HTML/CSS/XML markup still needs /<slug>/ URL mapping.
    private static bool RewriteMime(MediaTypeHeaderValue? type)
    {
        var media=type?.MediaType??"";
        return media.Equals("text/html",StringComparison.OrdinalIgnoreCase)||
            media.Equals("application/xhtml+xml",StringComparison.OrdinalIgnoreCase)||
            media.Equals("text/css",StringComparison.OrdinalIgnoreCase)||
            media.Equals("text/xml",StringComparison.OrdinalIgnoreCase)||
            media.Equals("application/xml",StringComparison.OrdinalIgnoreCase)||
            media.Equals("image/svg+xml",StringComparison.OrdinalIgnoreCase);
    }

    // The upstream might incorrectly label styles/scripts as text/plain or
    // octet-stream. With X-Content-Type-Options: nosniff this blocks assets.
    // Correct only well-known URL extensions; never guess unknown payloads.
    private static string? CorrectAssetMediaType(string appPath,
        MediaTypeHeaderValue? actual)
    {
        var media=actual?.MediaType;
        if(!string.IsNullOrWhiteSpace(media) &&
            !media.Equals("text/plain",StringComparison.OrdinalIgnoreCase) &&
            !media.Equals("application/octet-stream",StringComparison.OrdinalIgnoreCase) &&
            !media.Equals("binary/octet-stream",StringComparison.OrdinalIgnoreCase))
            return null;
        var extension=Path.GetExtension(appPath).ToLowerInvariant();
        return extension switch {
            ".css"=>"text/css",
            ".js" or ".mjs"=>"text/javascript",
            ".json"=>"application/json",
            ".webmanifest"=>"application/manifest+json",
            ".svg"=>"image/svg+xml",
            ".wasm"=>"application/wasm",
            _=>null
        };
    }

    private static bool Navigation(HttpRequest request)
    {
        if(!HttpMethods.IsGet(request.Method))return false;
        var dest=request.Headers["Sec-Fetch-Dest"].ToString();
        return dest is "document" or "iframe" ||
            request.Headers["Accept"].ToString()
                .Contains("text/html",StringComparison.OrdinalIgnoreCase);
    }

    private static bool AllowedMutation(HttpRequest request,Uri external,
        string pathPrefix)
    {
        if(HttpMethods.IsGet(request.Method)||HttpMethods.IsHead(request.Method))
            return true;
        if(request.Headers.TryGetValue("Origin",out var origin)&&
            !origin.ToString().Equals(external.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))return false;
        if(request.Headers.TryGetValue("Sec-Fetch-Site",out var site)&&
            site.ToString() is not ("same-origin" or "none"))return false;
        if(request.Headers.TryGetValue("Referer",out var referer)&&
            Uri.TryCreate(referer.ToString(),UriKind.Absolute,out var value)&&
            (!SameUpstream(external,value)||
             (pathPrefix.Length>0&&!value.AbsolutePath.StartsWith(
                pathPrefix+"/",StringComparison.Ordinal))))return false;
        return true;
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content,
        int max,CancellationToken ct)
    {
        if(content.Headers.ContentLength>max)
            throw new InvalidOperationException("Resource limit exceeded.");
        await using var source=await content.ReadAsStreamAsync(ct);
        using var output=new MemoryStream();
        var buffer=new byte[16384];
        int count;
        while((count=await source.ReadAsync(buffer,ct))>0){
            if(output.Length+count>max)
                throw new InvalidOperationException("Resource limit exceeded.");
            output.Write(buffer,0,count);
        }
        return output.ToArray();
    }

    // Response with application/octet-stream or missing MIME may actually be
    // HTML. Check an initial prefix only for document navigations; otherwise
    // stream unmodified so attachment downloads are never HTML-normalized.
    private static async Task<byte[]?> ReadOpaqueAsync(HttpContent content,
        Stream destination,CancellationToken ct)
    {
        await using var stream=await content.ReadAsStreamAsync(ct);
        var prefix=new byte[4096];
        var count=0;
        while(count<prefix.Length){
            var received=await stream.ReadAsync(prefix.AsMemory(count),ct);
            if(received==0)break;
            count+=received;
        }
        if(!LooksLikeHtml(Encoding.UTF8.GetString(prefix,0,count))){
            await destination.WriteAsync(prefix.AsMemory(0,count),ct);
            await stream.CopyToAsync(destination,ct);
            return null;
        }
        using var buffer=new MemoryStream();
        buffer.Write(prefix,0,count);
        var chunk=new byte[32768];
        int next;
        while((next=await stream.ReadAsync(chunk,ct))>0){
            if(buffer.Length+next>MaxTextBytes)
                throw new InvalidOperationException("HTML too large.");
            buffer.Write(chunk,0,next);
        }
        return buffer.ToArray();
    }

    private async Task ForwardAsync(HttpContext ctx,ProxySession session,
        Uri external,string appPath,string pathPrefix,CancellationToken ct)
    {
        var method=ctx.Request.Method;
        if(method is not ("GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE")||
            !AllowedMutation(ctx.Request,external,pathPrefix)){
            ctx.Response.StatusCode=403;return;
        }
        var target=new Uri(session.Upstream,appPath+
            ctx.Request.QueryString.ToUriComponent());
        if(!SameUpstream(session.Upstream,target)){
            ctx.Response.StatusCode=403;return;
        }
        using var req=new HttpRequestMessage(new HttpMethod(method),target);
        if(ctx.Request.ContentLength>0||ctx.Request.Headers.ContainsKey("Transfer-Encoding")){
            req.Content=new StreamContent(ctx.Request.Body);
            if(!string.IsNullOrEmpty(ctx.Request.ContentType))
                req.Content.Headers.TryAddWithoutValidation(
                    "Content-Type",ctx.Request.ContentType);
            if(ctx.Request.ContentLength is { } len)
                req.Content.Headers.ContentLength=len;
        }
        foreach(var header in new[]{"Accept","Accept-Language","User-Agent",
            "X-Requested-With","X-Requested-By","If-None-Match","If-Modified-Since",
            "Range"}){
            if(ctx.Request.Headers.TryGetValue(header,out var values))
                req.Headers.TryAddWithoutValidation(header,values.ToArray());
        }
        if(ctx.Request.Headers.TryGetValue("Origin",out var browserOrigin)&&
            browserOrigin.ToString().Equals(
                external.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
            req.Headers.TryAddWithoutValidation("Origin",
                session.Upstream.GetLeftPart(UriPartial.Authority));
        if(ctx.Request.Headers.TryGetValue("Referer",out var refValue)&&
           Uri.TryCreate(refValue.ToString(),UriKind.Absolute,out var from)&&
           SameUpstream(external,from)){
            var refPath=pathPrefix.Length>0
                ? from.AbsolutePath[pathPrefix.Length..] : from.AbsolutePath;
            req.Headers.Referrer=new Uri(session.Upstream,
                (refPath.Length==0?"/":refPath)+from.Query);
        }
        try{
            using var res=await session.Client.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead,ct);
            ctx.Response.StatusCode=(int)res.StatusCode;
            if(res.Headers.Location is { } location){
                var redirect=new Uri(target,location);
                if(!SameUpstream(session.Upstream,redirect)){
                    ctx.Response.StatusCode=502;return;
                }
                ctx.Response.Headers.Location=external.GetLeftPart(
                    UriPartial.Authority)+pathPrefix+redirect.PathAndQuery;
            }
            var disposition=res.Content.Headers.ContentDisposition;
            if(disposition is not null)
                ctx.Response.Headers.ContentDisposition=disposition.ToString();
            if(res.Content.Headers.ContentRange is { } range)
                ctx.Response.Headers.ContentRange=range.ToString();
            if(res.Headers.TryGetValues("Accept-Ranges",out var ranges))
                ctx.Response.Headers["Accept-Ranges"]=string.Join(",",ranges);
            var originalMime=res.Content.Headers.ContentType;
            var correctedMime=CorrectAssetMediaType(appPath,originalMime);
            var mime=correctedMime is not null
                ? MediaTypeHeaderValue.Parse(correctedMime):originalMime;
            if(mime is not null)ctx.Response.ContentType=mime.ToString();
            if(HttpMethods.IsHead(method)||res.StatusCode is
                HttpStatusCode.NotModified or HttpStatusCode.NoContent)
                return;
            var sniff=Navigation(ctx.Request)&&res.IsSuccessStatusCode&&
                disposition is null && (mime?.MediaType is null or
                    "text/plain" or "application/octet-stream" or
                    "application/x-octet-stream" or "text/xml" or "application/xml");
            var textual=RewriteMime(mime);
            if(textual||sniff){
                var binarySniff=sniff&&!textual;
                var bytes=binarySniff?
                    await ReadOpaqueAsync(res.Content,ctx.Response.Body,ct):
                    await ReadLimitedAsync(res.Content,RewriteLimit(mime),ct);
                if(bytes is null)return;
                var charset=mime?.CharSet?.Trim('"');
                Encoding encoding;
                try{encoding=charset is null?Encoding.UTF8:
                    Encoding.GetEncoding(charset);}
                catch(ArgumentException){encoding=Encoding.UTF8;}
                var html=encoding.GetString(bytes);
                if(sniff&&LooksLikeHtml(html))
                    ctx.Response.ContentType="text/html; charset="+encoding.WebName;
                else if(binarySniff){
                    await ctx.Response.Body.WriteAsync(bytes,ct);return;
                }
                var mediaType=mime?.MediaType;
                var isHtml=mediaType is not null &&
                    (mediaType.Equals("text/html",StringComparison.OrdinalIgnoreCase)||
                     mediaType.Equals("application/xhtml+xml",StringComparison.OrdinalIgnoreCase))||
                    sniff&&LooksLikeHtml(html);
                var transformed=isHtml
                    ? RewriteHtmlPreservingScripts(html,session.Upstream,external,pathPrefix)
                    : Rewrite(html,session.Upstream,external,pathPrefix);
                await ctx.Response.Body.WriteAsync(encoding.GetBytes(transformed),ct);
            }else{
                await res.Content.CopyToAsync(ctx.Response.Body,ct);
            }
        }catch(HttpRequestException){
            if(!ctx.Response.HasStarted)ctx.Response.StatusCode=502;
        }catch(OperationCanceledException) when(!ct.IsCancellationRequested){
            if(!ctx.Response.HasStarted)ctx.Response.StatusCode=504;
        }catch(InvalidOperationException){
            if(!ctx.Response.HasStarted)ctx.Response.StatusCode=413;
        }
    }
}
