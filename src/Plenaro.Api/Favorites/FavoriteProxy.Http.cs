using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Text.RegularExpressions;
using Plenaro.Api.Secrets;

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

    // Verified against the existing Confluence login network trace:
    // POST /rest/tsv/1.0/authenticate, JSON username/password/rememberMe/
    // targetUrl/captchaId. Only the approved Wiki upstream receives these
    // secrets, server-to-server over verified HTTPS. A 200 JSON response with
    // a same-origin navigation target confirms successful authentication.
    // Captcha/MFA/SSO, wrong passwords and unsupported plugins fail closed.
    private static async Task<bool> TryConfluenceWikiLoginAsync(
        HttpClient client,Uri origin,string username,string password,
        CancellationToken ct)
    {
        using(var page=await client.GetAsync(new Uri(origin,"/login.action"),
            HttpCompletionOption.ResponseHeadersRead,ct)){
            if(!page.IsSuccessStatusCode)return false;
        }
        using var request=new HttpRequestMessage(HttpMethod.Post,
            new Uri(origin,"/rest/tsv/1.0/authenticate")){
            Content=System.Net.Http.Json.JsonContent.Create(new {
                username,password,rememberMe=false,targetUrl="/",captchaId=""
            })
        };
        request.Headers.Referrer=new Uri(origin,"/login.action");
        request.Headers.TryAddWithoutValidation("Origin",
            origin.GetLeftPart(UriPartial.Authority));
        using var result=await client.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead,ct);
        if(!result.IsSuccessStatusCode ||
           result.Content.Headers.ContentType?.MediaType!="application/json" ||
           result.Content.Headers.ContentLength>8192)return false;
        var raw=await ReadLimitedAsync(result.Content,8192,ct);
        try{
            using var document=System.Text.Json.JsonDocument.Parse(raw);
            if(!document.RootElement.TryGetProperty("next",out var next)||
               next.ValueKind!=System.Text.Json.JsonValueKind.String)return false;
            var value=next.GetString();
            if(string.IsNullOrEmpty(value) ||
               !Uri.TryCreate(origin,value,out var redirect) ||
               !SameUpstream(origin,redirect))return false;
            return true;
        }catch(System.Text.Json.JsonException){return false;}
    }

    // Desktop parity for classic HTML forms: detect username fields by
    // autocomplete/name/id/placeholder, including single unfamiliar text
    // inputs such as the real login fields used by some enterprise apps.
    // This remains a server-to-server POST: credentials never enter the
    // favorite iframe/HTML/browser. Do not attempt JS forms, SSO or MFA
    // by guessing an API endpoint or injecting a password into script.
    private static bool UsernameHint(string attrs)
    {
        var autocomplete=Attribute(attrs,"autocomplete")??"";
        if(autocomplete.Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Any(x=>x.Equals("username",StringComparison.OrdinalIgnoreCase) ||
                    x.Equals("email",StringComparison.OrdinalIgnoreCase)))return true;
        var signature=string.Join(" ",new[]{
            Attribute(attrs,"name"),Attribute(attrs,"id"),
            Attribute(attrs,"placeholder"),Attribute(attrs,"aria-label")
        }.Where(x=>x is not null));
        return Regex.IsMatch(signature,
            @"user(?:name|id|login)?|benutzer(?:name|kennung)?|e-?mail|login(?:name|id)?|account(?:name|id)?|mitarbeiter(?:nummer|id)?",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
    }

    private static bool PasswordHint(string attrs)
    {
        var type=Attribute(attrs,"type")??"text";
        var autocomplete=Attribute(attrs,"autocomplete")??"";
        return type.Equals("password",StringComparison.OrdinalIgnoreCase) &&
            !autocomplete.Split(' ',StringSplitOptions.RemoveEmptyEntries)
                .Any(x=>x.Equals("new-password",StringComparison.OrdinalIgnoreCase));
    }

    // Build one safe request, or none. Never send credentials via GET,
    // to another host, to a registration/reset form, or to a form with
    // multiple password fields (possible password-change dialog).
    // A form requires an actual named HTML input; JS-only fields are not
    // synthesizable without an interactive browser.
    private static HttpRequestMessage? BuildHtmlLoginRequest(
        string html,Uri page,Uri origin,string username,string password)
    {
        foreach(Match form in FormRegex.Matches(html)){
            var formAttrs=form.Groups["attrs"].Value;
            if(!string.Equals(Attribute(formAttrs,"method"),"post",
                StringComparison.OrdinalIgnoreCase))continue;
            var action=Attribute(formAttrs,"action")??page.AbsoluteUri;
            if(!Uri.TryCreate(page,action,out var target)||
               !SameUpstream(origin,target)||
               Regex.IsMatch(target.AbsolutePath,
                   @"(?:^|/)(?:register|registration|signup|sign-up|reset|forgot|change-password)(?:/|$)",
                   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
                   TimeSpan.FromMilliseconds(100)))continue;
            var candidates=new List<(string Name,string Attrs,bool Hinted)>();
            var result=new List<KeyValuePair<string,string>>();
            string? passwordName=null;
            var passwordCount=0;
            var newPasswordPresent=false;
            foreach(Match input in InputRegex.Matches(form.Groups["body"].Value)){
                var attrs=input.Groups["attrs"].Value;
                if(Attribute(attrs,"disabled") is not null)continue;
                var name=Attribute(attrs,"name");
                if(string.IsNullOrWhiteSpace(name))continue;
                var type=(Attribute(attrs,"type")??"text").Trim().ToLowerInvariant();
                if(type=="password"){
                    passwordCount++;
                    if(!PasswordHint(attrs))newPasswordPresent=true;
                    else passwordName=name;
                }else if(type is "text" or "email" or "tel"){
                    if((Attribute(attrs,"autocomplete")??"")
                        .Contains("one-time-code",StringComparison.OrdinalIgnoreCase))
                        continue;
                    candidates.Add((name,attrs,UsernameHint(attrs)||
                        type=="email"));
                }else if(type=="hidden"){
                    // Preserve the page's original CSRF/anti-forgery token.
                    result.Add(new(name,Attribute(attrs,"value")??""));
                }
            }
            if(newPasswordPresent||passwordCount!=1||passwordName is null)continue;
            var hinted=candidates.Where(x=>x.Hinted).ToArray();
            // Prefer one clearly identified username field; if none is
            // identified, only one plain text/email input is unambiguous.
            var chosen=hinted.Length==1?hinted[0]:
                hinted.Length==0&&candidates.Count==1?candidates[0]:default;
            if(string.IsNullOrEmpty(chosen.Name))continue;
            result.Add(new(chosen.Name,username));
            result.Add(new(passwordName,password));
            // One login submit button's name/value can be significant.
            var submitFields=InputRegex.Matches(form.Groups["body"].Value)
                .Cast<Match>().Select(m=>m.Groups["attrs"].Value)
                .Where(a=>string.Equals(Attribute(a,"type"),"submit",
                    StringComparison.OrdinalIgnoreCase) &&
                    Attribute(a,"name") is not null).ToArray();
            if(submitFields.Length==1)
                result.Add(new(Attribute(submitFields[0],"name")!,
                    Attribute(submitFields[0],"value")??""));
            var request=new HttpRequestMessage(HttpMethod.Post,target){
                Content=new FormUrlEncodedContent(result)
            };
            request.Headers.Referrer=page;
            request.Headers.TryAddWithoutValidation("Origin",
                origin.GetLeftPart(UriPartial.Authority));
            return request;
        }
        return null;
    }

    private static readonly Regex LoginLinkRegex=new(
        @"<a\b(?<attrs>[^>]*)>(?<text>.*?)</a\s*>",
        RegexOptions.Compiled|RegexOptions.Singleline|RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    // The saved URL might be the app landing page rather than the login
    // document. Follow only ONE explicit same-origin "Log in" HTML link,
    // never guessed paths or a user-controlled cross-domain SSO redirect.
    private static Uri? FindHtmlLoginLink(string html,Uri page,Uri origin)
    {
        var matches=new HashSet<string>(StringComparer.Ordinal);
        foreach(Match link in LoginLinkRegex.Matches(html)){
            var href=Attribute(link.Groups["attrs"].Value,"href");
            if(string.IsNullOrWhiteSpace(href)||
               !Uri.TryCreate(page,href,out var target)||
               !SameUpstream(origin,target) || target==page)continue;
            var label=Regex.Replace(link.Groups["text"].Value,
                "<[^>]*>","",RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
            var path=target.AbsolutePath;
            if(!Regex.IsMatch(label,
                   @"\b(anmelden|einloggen|login|log in|sign in)\b",
                   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
                   TimeSpan.FromMilliseconds(100)) &&
               !Regex.IsMatch(path,
                   @"(?:^|/)(?:login|signin|sign-in)(?:/|\.|$)",
                   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,
                   TimeSpan.FromMilliseconds(100)))continue;
            matches.Add(target.AbsoluteUri);
        }
        return matches.Count==1?new Uri(matches.Single()):null;
    }

    // Bounded classic HTML auto-login: one landing GET, optionally one
    // discovered login-page GET, exactly ONE POST and optional same-origin
    // redirect follow. Cookies are retained by the private HttpClient.
    private sealed record HtmlLoginAttempt(string Code,int? Status=null);

    // A completed HTML login attempt is NOT proof of an authenticated session.
    // Return a fixed, safe diagnostic reason instead of false-positive success.
    private static async Task<HtmlLoginAttempt> TryLoginAsync(
        HttpClient client,Uri origin,Uri entry,
        string username,string password,CancellationToken ct)
    {
        var current=entry;
        var followedLoginLink=false;
        var visited=new HashSet<string>(StringComparer.Ordinal);
        for(var pageIndex=0;pageIndex<4;pageIndex++){
            if(!SameUpstream(origin,current)||
               !visited.Add(current.AbsoluteUri))
                return new("html-login-navigation-blocked");
            using var initial=new HttpRequestMessage(HttpMethod.Get,current);
            using var response=await client.SendAsync(initial,
                HttpCompletionOption.ResponseHeadersRead,ct);
            if(response.StatusCode is HttpStatusCode.MovedPermanently or
                HttpStatusCode.Redirect or HttpStatusCode.SeeOther or
                HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect){
                if(response.Headers.Location is not { } next)
                    return new("html-login-redirect-missing");
                var redirect=new Uri(current,next);
                if(!SameUpstream(origin,redirect))
                    return new("html-login-redirect-cross-origin");
                current=redirect;
                continue;
            }
            if(!response.IsSuccessStatusCode)
                return new("html-login-page-rejected",(int)response.StatusCode);
            if(response.Content.Headers.ContentLength>262144)
                return new("html-login-page-too-large");
            var bytes=await ReadLimitedAsync(response.Content,262144,ct);
            var html=Encoding.UTF8.GetString(bytes);
            if(!LooksLikeHtml(html))return new("html-login-page-not-html");
            using var request=BuildHtmlLoginRequest(html,current,origin,
                username,password);
            if(request is not null){
                using var result=await client.SendAsync(request,
                    HttpCompletionOption.ResponseHeadersRead,ct);
                if(!result.IsSuccessStatusCode && (int)result.StatusCode>=400)
                    return new("html-login-post-rejected",(int)result.StatusCode);
                if(result.Headers.Location is { } next){
                    var redirect=new Uri(request.RequestUri!,next);
                    if(!SameUpstream(origin,redirect))
                        return new("html-login-redirect-cross-origin");
                    using var follow=new HttpRequestMessage(HttpMethod.Get,redirect);
                    using var confirmed=await client.SendAsync(follow,
                        HttpCompletionOption.ResponseHeadersRead,ct);
                    if(!confirmed.IsSuccessStatusCode)
                        return new("html-login-followup-rejected",
                            (int)confirmed.StatusCode);
                }
                return new("html-login-post-sent-unconfirmed",(int)result.StatusCode);
            }
            if(!followedLoginLink &&
               FindHtmlLoginLink(html,current,origin) is { } found){
                followedLoginLink=true;
                current=found;
            }else return new("html-login-form-not-found");
        }
        return new("html-login-navigation-limit");
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

    // Some Confluence plugins ignore ajs-context-path and emit origin-root
    // AJAX /rest/... or dynamically added /s/... scripts. Rewriting downloaded
    // JS was unsafe (PR #24); inject this scoped, early HTML bootstrap instead.
    // It NEVER exposes /rest or /s routes at the shared host root, so the
    // request still needs the normal /<slug>/ path-scoped proxy cookie and ACL.
    // The script runs only in selected favorite HTML, not in Plenaro itself.
    private const string BrowserPathShim = """
        <script data-plenaro-browser-shim>
        (function () {
          "use strict";
          const base = "__PREFIX__";
          const routes = ["/rest/", "/s/"];
          const allRootPaths = __ALL_ROOT_PATHS__;
          // Only the personal favorite knows its own approved HTTPS upstream.
          // Allow exact-origin browser URLs to be routed through its proxy;
          // never rewrite unrelated domains or pass them server-side.
          const upstreamOrigin = __UPSTREAM_ORIGIN__;
          function rewrite(value) {
            if (typeof value !== "string" &&
                !(typeof URL !== "undefined" && value instanceof URL))
              return value;
            const raw = String(value);
            if (!raw.startsWith("/") &&
                !raw.startsWith(window.location.origin + "/") &&
                !(allRootPaths && upstreamOrigin &&
                  raw.startsWith(upstreamOrigin + "/")))
              return value;
            if (raw.startsWith("//")) return value;
            let url;
            try { url = new URL(raw, window.location.href); }
            catch (_) { return value; }
            if ((url.origin !== window.location.origin &&
                 !(allRootPaths && upstreamOrigin &&
                   url.origin === upstreamOrigin)) ||
                url.pathname === base || url.pathname.startsWith(base + "/") ||
                url.pathname.startsWith("/_plenaro/") ||
                (!allRootPaths &&
                  !routes.some(route => url.pathname.startsWith(route))))
              return value;
            return base + url.pathname + url.search + url.hash;
          }
          const xhrOpen = XMLHttpRequest.prototype.open;
          XMLHttpRequest.prototype.open = function () {
            const args = Array.prototype.slice.call(arguments);
            args[1] = rewrite(args[1]);
            return Reflect.apply(xhrOpen, this, args);
          };
          // Confluence/Atlassian plugins sometimes construct Request objects
          // before passing them to fetch(). Rewriting only fetch(string)
          // leaves those origin-root requests unmodified. Rebase the URL at
          // construction time; native Request still owns the method, body,
          // headers, credentials, signal and other request options.
          const NativeRequest = window.Request;
          if (typeof NativeRequest === "function") {
            window.Request = class PlenaroScopedRequest extends NativeRequest {
              constructor(input, init) {
                if (typeof input === "string" ||
                    (typeof URL !== "undefined" && input instanceof URL))
                  input = rewrite(input);
                super(input, init);
              }
            };
          }
          const nativeFetch = window.fetch;
          if (typeof nativeFetch === "function") {
            window.fetch = function (input, init) {
              // Preserve Request objects as-is: they carry body/stream,
              // credentials and request options that must not be reconstructed.
              if (typeof input === "string" ||
                  (typeof URL !== "undefined" && input instanceof URL))
                input = rewrite(input);
              return nativeFetch.call(this, input, init);
            };
          }
          const nativeBeacon = navigator.sendBeacon;
          if (typeof nativeBeacon === "function") {
            navigator.sendBeacon = function (url, data) {
              return nativeBeacon.call(this, rewrite(url), data);
            };
          }
          // Modern image galleries often use srcset, <picture>/<source>,
          // lazy-load data-src and dynamically assigned form actions.
          // These routes are app-scoped; never modify external CDN origins.
          function rewriteSrcset(value) {
            if (typeof value !== "string") return value;
            return value.replace(
              /(^|,\s*)(\/(?!\/)[^\s,]+|https?:\/\/[^\s,]+)/g,
              function (_match, leading, candidate) {
                return leading + rewrite(candidate);
              });
          }
          function controlledAttribute(node, name) {
            if (!node || node.nodeType !== 1) return false;
            const attr = String(name).toLowerCase();
            const tag = node.tagName;
            if (tag === "SCRIPT") return attr === "src";
            if (tag === "LINK") return attr === "href";
            if (tag === "IMG") return attr === "src" ||
                (allRootPaths && ["srcset","data-src","data-srcset",
                  "data-original"].includes(attr));
            if (!allRootPaths) return false;
            if (tag === "SOURCE") return attr === "src" || attr === "srcset";
            if (tag === "VIDEO") return attr === "poster" || attr === "src";
            if (tag === "FORM") return attr === "action";
            return false;
          }
          function correctAttribute(node, name, value) {
            if (!controlledAttribute(node, name)) return value;
            return /srcset/i.test(name) ? rewriteSrcset(value) : rewrite(value);
          }
          const setAttr = Element.prototype.setAttribute;
          Element.prototype.setAttribute = function (name, value) {
            return setAttr.call(this, name, correctAttribute(this, name, value));
          };
          function adjust(node) {
            if (!node || node.nodeType !== 1) return;
            const names = ["src","href","srcset","poster","action",
              "data-src","data-srcset","data-original"];
            for (const name of names) {
              if (!controlledAttribute(node, name)) continue;
              const current = node.getAttribute(name);
              if (current === null) continue;
              const corrected = correctAttribute(node, name, current);
              if (corrected !== current)
                setAttr.call(node, name, corrected);
            }
          }
          // URL property setters do not necessarily call setAttribute.
          function patchAssetUrlProperty(prototype, property) {
            if (!prototype) return;
            const descriptor = Object.getOwnPropertyDescriptor(prototype, property);
            if (!descriptor || !descriptor.configurable ||
                typeof descriptor.set !== "function") return;
            Object.defineProperty(prototype, property, {
              get: descriptor.get,
              set: function (value) {
                return descriptor.set.call(this,
                  correctAttribute(this, property, value));
              },
              enumerable: descriptor.enumerable,
              configurable: true
            });
          }
          if (typeof HTMLScriptElement !== "undefined")
            patchAssetUrlProperty(HTMLScriptElement.prototype, "src");
          if (typeof HTMLLinkElement !== "undefined")
            patchAssetUrlProperty(HTMLLinkElement.prototype, "href");
          if (typeof HTMLImageElement !== "undefined") {
            patchAssetUrlProperty(HTMLImageElement.prototype, "src");
            if (allRootPaths)
              patchAssetUrlProperty(HTMLImageElement.prototype, "srcset");
          }
          if (allRootPaths) {
            if (typeof HTMLSourceElement !== "undefined") {
              patchAssetUrlProperty(HTMLSourceElement.prototype, "src");
              patchAssetUrlProperty(HTMLSourceElement.prototype, "srcset");
            }
            if (typeof HTMLVideoElement !== "undefined")
              patchAssetUrlProperty(HTMLVideoElement.prototype, "poster");
            if (typeof HTMLFormElement !== "undefined")
              patchAssetUrlProperty(HTMLFormElement.prototype, "action");
            // SPA login/navigation often changes history rather than the
            // document. Keep the new same-origin path beneath /site-<id>/.
            for (const key of ["pushState","replaceState"]) {
              const original = history[key];
              if (typeof original !== "function") continue;
              history[key] = function (state, unused, url) {
                if (url !== undefined && url !== null)
                  url = rewrite(url);
                return original.call(this, state, unused, url);
              };
            }
          }
          const append = Node.prototype.appendChild;
          Node.prototype.appendChild = function (node) {
            adjust(node);
            return append.call(this, node);
          };
          const insert = Node.prototype.insertBefore;
          Node.prototype.insertBefore = function (node, reference) {
            adjust(node);
            return insert.call(this, node, reference);
          };
          // innerHTML/template fragments bypass setAttribute and DOM setters.
          // Correct their resource URLs once they enter the document.
          if (allRootPaths && typeof MutationObserver === "function") {
            const observer = new MutationObserver(function (mutations) {
              for (const change of mutations) {
                if (change.type === "attributes") {
                  adjust(change.target);
                } else {
                  for (const node of change.addedNodes) {
                    adjust(node);
                    if (node.querySelectorAll)
                      node.querySelectorAll(
                        "img,source,script,link,video,form"
                      ).forEach(adjust);
                  }
                }
              }
            });
            const watch = function () {
              if (!document.documentElement) return;
              observer.observe(document.documentElement, {
                subtree:true, childList:true, attributes:true,
                attributeFilter:["src","srcset","href","poster",
                  "action","data-src","data-srcset","data-original"]
              });
            };
            if (document.documentElement) watch();
            else document.addEventListener("DOMContentLoaded",watch,{once:true});
          }
        })();
        </script>
        """;

    // Confluence space menus can create anchors after the initial HTML
    // rewrite. Keep navigation inside the wiki's cookie path without changing
    // other favorites, external links or browser security settings.
    private const string WikiNavigationShim = """
        <script data-plenaro-wiki-navigation>
        (function () {
          "use strict";
          const base = "/wiki";
          const upstream = __WIKI_UPSTREAM__;
          const origin = location.origin;
          function rebase(raw) {
            if (typeof raw !== "string" || !raw.trim() ||
                raw.trim().startsWith("#")) return raw;
            let url;
            try { url = new URL(raw, document.baseURI); }
            catch (_) { return raw; }
            if (url.username || url.password ||
                (url.origin !== origin && url.origin !== upstream)) return raw;
            const path = url.pathname;
            if (url.origin === origin &&
                (path === base || path.startsWith(base + "/"))) return raw;
            if (path.startsWith("//") || path.startsWith("/_plenaro") ||
                path.startsWith("/site-")) return raw;
            // On the shared host, only Confluence navigation routes are
            // recoverable. Do not capture links to unrelated favorite apps.
            if (url.origin === origin &&
                !/^\/(?:display|spaces|pages|browse|dashboard|search|plugins|users|label)(?:\/|\.action$|$)/i.test(path) &&
                !/^\/(?:index|homepage|viewpage|recentlyupdated)\.action$/i.test(path) &&
                path !== "/") return raw;
            return base + path + url.search + url.hash;
          }
          function adjust(anchor) {
            if (!anchor || !anchor.matches("a[href],area[href]")) return;
            const raw = anchor.getAttribute("href");
            const next = rebase(raw);
            if (next !== raw) anchor.setAttribute("href", next);
          }
          function scan(node) {
            if (!node || node.nodeType !== 1) return;
            adjust(node);
            node.querySelectorAll("a[href],area[href]").forEach(adjust);
          }
          // Capture before Confluence's click handlers read the link.
          function beforeNavigation(event) {
            const anchor = event.composedPath().find(node =>
              node instanceof Element && node.matches("a[href],area[href]"));
            adjust(anchor);
          }
          document.addEventListener("click", beforeNavigation, true);
          document.addEventListener("auxclick", beforeNavigation, true);
          document.addEventListener("contextmenu", beforeNavigation, true);
          const observer = new MutationObserver(changes => {
            for (const change of changes) {
              if (change.type === "attributes") adjust(change.target);
              else for (const node of change.addedNodes) scan(node);
            }
          });
          function watch() {
            if (!document.documentElement) return;
            observer.observe(document.documentElement, {
              subtree:true, childList:true, attributes:true, attributeFilter:["href"]
            });
            scan(document.documentElement);
          }
          if (document.documentElement) watch();
          else document.addEventListener("DOMContentLoaded", watch, {once:true});
        })();
        </script>
        """;

    private static string InjectWikiNavigationShim(string html,Uri upstream)
    {
        if(html.Contains("data-plenaro-wiki-navigation",StringComparison.Ordinal))
            return html;
        var script=WikiNavigationShim.Replace("__WIKI_UPSTREAM__",
            JsonSerializer.Serialize(upstream.GetLeftPart(UriPartial.Authority)),
            StringComparison.Ordinal);
        return Regex.Replace(html,@"<head\b[^>]*>",m=>m.Value+script,
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
    }

    private static string InjectBrowserPathShim(string html,string pathPrefix)=>
        InjectPathShim(html,pathPrefix,false);

    private static string InjectPersonalSitePathShim(string html,string pathPrefix)=>
        InjectPathShim(html,pathPrefix,true);

    private static string InjectPersonalSitePathShimWithOrigin(
        string html,string pathPrefix,Uri upstream)=>
        InjectPathShim(html,pathPrefix,true,
            upstream.GetLeftPart(UriPartial.Authority));

    private static string InjectPathShim(string html,string pathPrefix,
        bool allRootPaths,string? upstreamOrigin=null)
    {
        if(pathPrefix.Length==0 ||
           html.Contains("data-plenaro-browser-shim",StringComparison.Ordinal))
            return html;
        // Slug is server-created and verified before inclusion in JS.
        var slug=pathPrefix.TrimStart('/');
        if(pathPrefix!="/"+slug || !FavoriteHostNames.ValidSlug(slug))
            throw new InvalidOperationException("Invalid shim path.");
        var script=BrowserPathShim.Replace("__PREFIX__",pathPrefix,
            StringComparison.Ordinal).Replace("__ALL_ROOT_PATHS__",
                allRootPaths?"true":"false",StringComparison.Ordinal)
            .Replace("__UPSTREAM_ORIGIN__",
                JsonSerializer.Serialize(allRootPaths?upstreamOrigin:null),
                StringComparison.Ordinal);
        return Regex.Replace(html,@"<head\b[^>]*>",
            match=>match.Value+script,RegexOptions.IgnoreCase|
            RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
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
            ".png" or ".apng"=>"image/png",
            ".jpg" or ".jpeg" or ".jpe"=>"image/jpeg",
            ".webp"=>"image/webp",
            ".gif"=>"image/gif",
            ".avif"=>"image/avif",
            ".ico"=>"image/x-icon",
            ".bmp"=>"image/bmp",
            ".wasm"=>"application/wasm",
            _=>null
        };
    }

    // Atlassian's interactive login plugin returns {"next":"/"} after a
    // successful POST /rest/tsv/1.0/authenticate. In the shared path-host
    // configuration, browser navigation to "/" is intentionally blocked.
    // Rewrite only this plugin's successful JSON login response and only
    // the 'next' navigation property, never generic API JSON or JS bundles.
    private static string RewriteConfluenceLoginNext(string json,string pathPrefix)
    {
        if(pathPrefix.Length==0)return json;
        try{
            var document=JsonNode.Parse(json) as JsonObject;
            if(document is null ||
               document["next"] is not JsonValue value ||
               !value.TryGetValue<string>(out var next)||
               string.IsNullOrEmpty(next)||
               !next.StartsWith('/')||next.StartsWith("//")||
               next==pathPrefix||
               next.StartsWith(pathPrefix+"/",StringComparison.Ordinal)||
               next.StartsWith(pathPrefix+"?",StringComparison.Ordinal)||
               next.StartsWith(pathPrefix+"#",StringComparison.Ordinal))
                return json;
            document["next"]=pathPrefix+next;
            return document.ToJsonString();
        }catch(JsonException){
            return json;
        }catch(InvalidOperationException){
            return json;
        }
    }

    private static bool ShouldRewriteConfluenceLoginNext(
        string method,HttpStatusCode status,string appPath,string pathPrefix,
        string slug,MediaTypeHeaderValue? contentType,string? enabledSlugs)
    {
        return HttpMethods.IsPost(method)&&
            (int)status>=200&&(int)status<300 &&
            pathPrefix.Length>0 &&
            appPath.Equals("/rest/tsv/1.0/authenticate",
                StringComparison.OrdinalIgnoreCase)&&
            contentType?.MediaType?.Equals("application/json",
                StringComparison.OrdinalIgnoreCase)==true &&
            FavoritePathRouting.BrowserShimEnabled(enabledSlugs,slug);
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

    // Never derive an upstream Referer from another favorite, the shared
    // browser root, or a path shorter than the current /site-<id>/ prefix.
    // For GET resources those references are optional; for writes the
    // AllowedMutation gate already requires the current favorite.
    private static Uri? UpstreamReferer(Uri external,Uri upstream,
        string pathPrefix,string? raw)
    {
        if(!Uri.TryCreate(raw,UriKind.Absolute,out var from)||
           !SameUpstream(external,from))return null;
        var path=from.AbsolutePath;
        if(pathPrefix.Length>0){
            if(path==pathPrefix)path="/";
            else if(path.StartsWith(pathPrefix+"/",StringComparison.Ordinal))
                path=path[pathPrefix.Length..];
            else return null;
        }
        return new Uri(upstream,(path.Length==0?"/":path)+from.Query);
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

    // A failed iframe navigation used to produce an empty 502/403 page.
    // Return a small, actionable, self-contained document instead; never
    // include the upstream URL, credentials or internal network addresses.
    internal static (int Status,string Code,string Message) NetworkFailure(HttpRequestException error)
    {
        for(Exception? cause=error;cause is not null;cause=cause.InnerException)
            if(cause.Message.Contains("Host address is blocked",StringComparison.Ordinal))
                return (502,"private-upstream-blocked","Die Zieladresse ist aus Sicherheitsgründen gesperrt "+
                    "(Loopback, Link-Local oder Metadaten-Adresse).");
        if(error.HttpRequestError==HttpRequestError.NameResolutionError)
            return (502,"upstream-dns","Der Plenaro-Server kann den DNS-Namen nicht auflösen. "+
                "Bitte DNS und Netzwerkfreigabe prüfen.");
        if(error.HttpRequestError==HttpRequestError.SecureConnectionError)
            return (502,"upstream-tls","Die HTTPS-Verbindung ist an der Zertifikats-/TLS-Prüfung gescheitert. "+
                "Bei IP-Adressen muss das Zertifikat auch für diese IP gültig und vertrauenswürdig sein.");
        for(Exception? cause=error;cause is not null;cause=cause.InnerException)
            if(cause is SocketException socket){
                if(socket.SocketErrorCode==SocketError.TimedOut)
                    return (504,"upstream-timeout","Der Zielserver antwortet nicht. "+
                        "Firewall-Freigabe, Routing und Zielserver prüfen; eine Sperre ist nicht sicher nachgewiesen.");
                if(socket.SocketErrorCode==SocketError.ConnectionRefused)
                    return (502,"upstream-refused","Die TCP-Verbindung wurde abgelehnt. "+
                        "Bitte HTTPS-Port, Zielserver und Firewall-Regeln prüfen.");
                if(socket.SocketErrorCode is SocketError.HostUnreachable or SocketError.NetworkUnreachable)
                    return (502,"upstream-unreachable","Der Zielserver ist vom Plenaro-Server nicht erreichbar. "+
                        "Bitte Routing und Firewall-Freigabe prüfen.");
            }
        return (502,"upstream-unavailable","Der Zielserver ist über den Plenaro-Proxy nicht erreichbar. "+
            "Bitte HTTPS-Port, Firewall-Freigabe und Netzwerk vom Plenaro-Server prüfen.");
    }

    private static async Task ProxyNavigationErrorAsync(
        HttpContext ctx,int status,string code,string message,CancellationToken ct)
    {
        if(ctx.Response.HasStarted)return;
        ctx.Response.StatusCode=status;
        ctx.Response.Headers["X-Plenaro-Favorite-Error"]=code;
        ctx.Response.Headers.CacheControl="no-store";
        if(!Navigation(ctx.Request))return;
        ctx.Response.ContentType="text/html; charset=utf-8";
        // Only fixed, server-authored text is passed into this template.
        var html="<html lang=\"de\"><head><meta charset=\"utf-8\">"+
            "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">"+
            "<title>Favorit nicht erreichbar</title></head>"+
            "<body style=\"font:16px system-ui;background:#111827;color:#e5e7eb;"+
            "padding:38px;max-width:680px;margin:0 auto;line-height:1.6\">"+
            "<h2>Diese Webseite konnte nicht angezeigt werden</h2><p>"+
            System.Net.WebUtility.HtmlEncode(message)+"</p>"+
            "<p>Prüfe die gespeicherte HTTPS-Adresse oder öffne die "+
            "Originalseite über den Link oberhalb des Fensters.</p>"+
            "<small>Fehlercode: "+System.Net.WebUtility.HtmlEncode(code)+"</small>"+
            "</body></html>";
        await ctx.Response.WriteAsync(html,ct);
    }

    // Only return a fixed diagnostic category. Never return upstream error
    // bodies, which might contain credentials, URLs, query strings or PII.
    private static string? ClassifyHttp400(string content)
    {
        var sample=content.Length>16384?content[..16384]:content;
        const RegexOptions flags=RegexOptions.IgnoreCase|
            RegexOptions.CultureInvariant;
        if(Regex.IsMatch(sample,@"\b(?:csrf|xsrf|anti.?forgery|requestverificationtoken)\b",
            flags,TimeSpan.FromMilliseconds(50)))return "upstream-400-csrf-hint";
        if(Regex.IsMatch(sample,@"\b(?:invalid.origin|origin.mismatch|invalid.referer|referer.mismatch)\b",
            flags,TimeSpan.FromMilliseconds(50)))return "upstream-400-origin-hint";
        if(Regex.IsMatch(sample,@"\b(?:session.expired|invalid.session|not.authenticated|unauthenticated)\b",
            flags,TimeSpan.FromMilliseconds(50)))return "upstream-400-session-hint";
        if(Regex.IsMatch(sample,@"\b(?:validation.failed|required.field|one.or.more.validation.errors|invalid.payload)\b",
            flags,TimeSpan.FromMilliseconds(50)))return "upstream-400-validation-hint";
        if(Regex.IsMatch(sample,@"\b(?:invalid.content.type|unsupported.media.type)\b",
            flags,TimeSpan.FromMilliseconds(50)))return "upstream-400-content-type-hint";
        return null;
    }

    // Header allowlist: only the approved HTTPS favorite's upstream receives
    // these values. In particular OrgManager/ATOSS sends its anti-CSRF token
    // in the literal 'CSRF' header (NOT 'X-CSRF-Token'); its endpoints also
    // send 'CurrentLanguage'. Forward both unmodified but never log values.
    // Do not forward Cookie/Host/Forwarded/Proxy-Authorization or arbitrary
    // browser headers between personal favorites on the shared origin.
    private static void CopyApprovedRequestHeaders(
        HttpRequest incoming,HttpRequestMessage upstream)
    {
        foreach(var header in new[]{"Accept","Accept-Language","User-Agent",
            "X-Requested-With","X-Requested-By","If-None-Match","If-Modified-Since",
            "Range","Authorization","X-CSRF-Token","X-CSRFToken","X-XSRF-TOKEN",
            "X-Atlassian-Token","CSRF","CurrentLanguage"}){
            if(incoming.Headers.TryGetValue(header,out var values))
                upstream.Headers.TryAddWithoutValidation(header,values.ToArray());
        }
    }

    private async Task ForwardAsync(HttpContext ctx,ProxySession session,
        Uri external,string appPath,string pathPrefix,CancellationToken ct)
    {
        var method=ctx.Request.Method;
        var resource=DebugResource(appPath,Navigation(ctx.Request));
        if(method is not ("GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE")||
            !AllowedMutation(ctx.Request,external,pathPrefix)){
            DebugHttp(session.User,session.Favorite,"warning","request-blocked",method,resource,403,appPath);
            ctx.Response.StatusCode=403;return;
        }
        var target=new Uri(session.Upstream,appPath+
            ctx.Request.QueryString.ToUriComponent());
        if(!SameUpstream(session.Upstream,target)){
            DebugHttp(session.User,session.Favorite,"warning","target-blocked",method,resource,403,appPath);
            ctx.Response.StatusCode=403;return;
        }
        using var req=new HttpRequestMessage(new HttpMethod(method),target);
        // Some SPAs send zero-length JSON/FORM POSTs with a Content-Type.
        // Creating no HttpContent dropped that header and caused HTTP 400.
        if(ctx.Request.ContentLength>0||
           ctx.Request.Headers.ContainsKey("Transfer-Encoding")||
           (!HttpMethods.IsGet(method)&&!HttpMethods.IsHead(method)&&
            !string.IsNullOrEmpty(ctx.Request.ContentType))){
            req.Content=new StreamContent(ctx.Request.Body);
            if(!string.IsNullOrEmpty(ctx.Request.ContentType))
                req.Content.Headers.TryAddWithoutValidation(
                    "Content-Type",ctx.Request.ContentType);
            if(ctx.Request.ContentLength is { } len)
                req.Content.Headers.ContentLength=len;
        }
        CopyApprovedRequestHeaders(ctx.Request,req);
        if(ctx.Request.Headers.TryGetValue("Origin",out var browserOrigin)&&
            browserOrigin.ToString().Equals(
                external.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase))
            req.Headers.TryAddWithoutValidation("Origin",
                session.Upstream.GetLeftPart(UriPartial.Authority));
        if(ctx.Request.Headers.TryGetValue("Referer",out var refValue) &&
           UpstreamReferer(external,session.Upstream,pathPrefix,
               refValue.ToString()) is { } upstreamReferer)
            req.Headers.Referrer=upstreamReferer;
        try{
            using var res=await session.Client.SendAsync(req,
                HttpCompletionOption.ResponseHeadersRead,ct);
            ctx.Response.StatusCode=(int)res.StatusCode;
            DebugHttp(session.User,session.Favorite,
                (int)res.StatusCode>=500?"error":
                (int)res.StatusCode>=400?"warning":"info",
                "http-response",method,resource,(int)res.StatusCode,appPath);
            if(res.StatusCode==HttpStatusCode.Forbidden&&HttpMethods.IsGet(method)&&Navigation(ctx.Request)){
                await ProxyNavigationErrorAsync(ctx,403,"upstream-http-forbidden",
                    "Der Zielserver oder ein vorgeschalteter Filter verweigert den Zugriff (HTTP 403). "+
                    "Bitte Berechtigungen, Firewall-/Proxy-Freigaben und Anmeldung prüfen. "+
                    "Die Netzwerkverbindung besteht; die genaue Sperrursache ist nicht eindeutig.",ct);
                return;
            }
            if(res.Headers.Location is { } location){
                var redirect=new Uri(target,location);
                if(!SameUpstream(session.Upstream,redirect)){
                    DebugHttp(session.User,session.Favorite,"error","redirect-cross-origin",method,resource,502,appPath);
                    await ProxyNavigationErrorAsync(ctx,502,"upstream-redirect",
                        "Die Webseite leitet auf einen anderen Server weiter, z. B. zur externen SSO-Anmeldung. "+
                        "Diese Weiterleitung ist im isolierten Proxy nicht freigegeben. "+
                        "Hinterlege die endgültige HTTPS-Adresse als Favorit oder verwende bei externem SSO „Originalseite öffnen“.",ct);
                    return;
                }
                ctx.Response.Headers.Location=external.GetLeftPart(
                    UriPartial.Authority)+pathPrefix+redirect.PathAndQuery+redirect.Fragment;
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
            if(ShouldRewriteConfluenceLoginNext(method,res.StatusCode,
                appPath,pathPrefix,session.Slug,mime,
                configuration["PLENARO_FAVORITES_BROWSER_SHIM_SLUGS"])){
                // Keep the login response small and avoid streaming an
                // unbounded upstream JSON payload. The upstream session
                // cookies remain in this user's private HttpClient jar.
                var payload=await ReadLimitedAsync(res.Content,65536,ct);
                var content=Encoding.UTF8.GetString(payload);
                var rewritten=RewriteConfluenceLoginNext(content,pathPrefix);
                // Non-sensitive deployment diagnostic: a browser trace can
                // prove this code path ran, without logging login contents.
                ctx.Response.Headers["X-Plenaro-Login-Next"]=
                    rewritten==content?"unchanged":"prefixed";
                Debug(session.User,session.Favorite,"info",
                    rewritten==content?"login-next-unchanged":"login-next-rewritten",method,resource);
                ctx.Response.ContentType="application/json; charset=utf-8";
                await ctx.Response.WriteAsync(rewritten,ct);
                return;
            }
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
                if(res.StatusCode==HttpStatusCode.BadRequest &&
                   ClassifyHttp400(html) is { } hint)
                    DebugHttp(session.User,session.Favorite,"warning",hint,
                        method,resource,400,appPath);
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
                if(isHtml&&pathPrefix.Length>0){
                    if(session.Slug=="wiki")
                        transformed=InjectWikiNavigationShim(transformed,session.Upstream);
                    if(session.Slug.StartsWith("site-",StringComparison.Ordinal))
                        transformed=InjectPersonalSitePathShimWithOrigin(
                            transformed,pathPrefix,session.Upstream);
                    else if(FavoritePathRouting.BrowserShimEnabled(
                        configuration["PLENARO_FAVORITES_BROWSER_SHIM_SLUGS"],
                        session.Slug))
                        transformed=InjectBrowserPathShim(transformed,pathPrefix);
                }
                // The WPF desktop client fills *rendered* DOM fields, not
                // server-side HTML forms. Transmit this browser helper ONLY
                // inside a per-favorite dedicated HTTPS origin. On the
                // shared path host, other favorite JS would have same-origin
                // access to the injected credentials: intentionally skip.
                if(isHtml && res.IsSuccessStatusCode &&
                   disposition is null && HttpMethods.IsGet(method) &&
                   Navigation(ctx.Request) && session.AutoLogin &&
                   session.Slug.StartsWith("site-",StringComparison.Ordinal)){
                    var dedicated=FavoriteHostNames.PreferredOrigin(session.Slug,
                        configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
                        configuration["PLENARO_FAVORITES_HOST_TEMPLATE"]);
                    if(!FavoriteDomAutoLogin.Eligible(
                        session.Slug,session.AutoLogin,pathPrefix.Length>0,
                        session.Username,external.Host,dedicated?.Host)){
                        ctx.Response.Headers["X-Plenaro-Autofill"]="isolated-origin-required";
                        // The shared host is intentional; don't warn on
                        // every HTML navigation or recommend more DNS aliases.
                    }else{
                        var vault=ctx.RequestServices.GetService<SecretVault>();
                        if(vault?.IsConfigured==true){
                            var secret=await vault.GetForIntegrationAsync(
                                "user",session.User,
                                FavoritesEndpoints.PasswordName(session.Favorite),ct);
                            if(!string.IsNullOrEmpty(secret)){
                                transformed=FavoriteDomAutoLogin.Inject(
                                    transformed,session.Username,secret,
                                    external.Host,session.AutoFillNonce);
                                ctx.Response.Headers["X-Plenaro-Autofill"]="dedicated-dom";
                                Debug(session.User,session.Favorite,"info","autofill-injected",method,resource);
                            }else{
                                ctx.Response.Headers["X-Plenaro-Autofill"]="missing-password";
                                Debug(session.User,session.Favorite,"warning","autofill-password-missing",method,resource);
                            }
                        }else{
                            ctx.Response.Headers["X-Plenaro-Autofill"]="vault-unconfigured";
                            Debug(session.User,session.Favorite,"warning","autofill-vault-unconfigured",method,resource);
                        }
                    }
                }
                await ctx.Response.Body.WriteAsync(encoding.GetBytes(transformed),ct);
            }else{
                await res.Content.CopyToAsync(ctx.Response.Body,ct);
            }
        }catch(HttpRequestException error) when(!ct.IsCancellationRequested){
            var failure=NetworkFailure(error);
            DebugHttp(session.User,session.Favorite,"error",
                failure.Code=="private-upstream-blocked"?"private-address-blocked":failure.Code,
                method,resource,failure.Status,appPath);
            await ProxyNavigationErrorAsync(ctx,failure.Status,failure.Code,failure.Message,ct);
        }catch(OperationCanceledException) when(!ct.IsCancellationRequested){
            DebugHttp(session.User,session.Favorite,"error","upstream-timeout",method,resource,504,appPath);
            await ProxyNavigationErrorAsync(ctx,504,"upstream-timeout",
                "Der Zielserver hat nicht rechtzeitig geantwortet. "+
                "Bitte Firewall-Freigabe, Routing und Erreichbarkeit vom Plenaro-Server prüfen. "+
                "Ein Timeout allein beweist keine Firewall-Sperre.",ct);
        }catch(InvalidOperationException){
            DebugHttp(session.User,session.Favorite,"error","upstream-resource-limit",method,resource,413,appPath);
            await ProxyNavigationErrorAsync(ctx,413,"upstream-resource-limit",
                "Die Antwort des Zielservers ist für den Proxy zu groß.",ct);
        }
    }
}
