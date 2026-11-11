using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Plenaro.Api.Znuny;
using Plenaro.Api.Favorites;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

// Favoriten-Wildcard ist zunächst nur eine reservierte, niemals
// als Plenaro-Login-Host verwendbare Origin.
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "wiki.apps.example.org",null,"https://portal.example.org",
    "https://tickets.example.org"),"wildcard guard is opt-in");
Assert(FavoriteHostGuard.IsUnassignedAlias(
    "wiki.apps.example.org","apps.example.org","https://portal.example.org",
    "https://tickets.example.org"),"unknown app alias is denied");
Assert(FavoriteHostGuard.IsUnassignedAlias(
    "nested.wiki.apps.example.org","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "nested aliases cannot expose Plenaro UI");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "portal.apps.example.org","apps.example.org",
    "https://portal.apps.example.org","https://tickets.apps.example.org"),
    "main application host is unchanged");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "tickets.apps.example.org","apps.example.org",
    "https://portal.apps.example.org","https://tickets.apps.example.org"),
    "native Znuny proxy host is unchanged");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "127.0.0.1","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "IP-based health check unaffected");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "unrelated.example.net","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "other domains unaffected by optional guard");
var invalidFavoritesDomainRejected=false;
try{
    FavoriteHostGuard.IsUnassignedAlias("wiki.apps.example.org",
        "*.apps.example.org","https://portal.example.org",
        "https://tickets.example.org");
}catch(InvalidOperationException){invalidFavoritesDomainRejected=true;}
Assert(invalidFavoritesDomainRejected,"invalid base domains fail closed");

// The new wildcard app router is opt-in and never relaxes the
// established same-origin CSRF gate for the main Plenaro application.
var favoriteConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_BASE_DOMAIN"]="apps.example.org",
        ["Security:RequireHttps"]="true"
    }).Build();
var favoriteProxy=new FavoriteProxy(favoriteConfig);
Assert(favoriteProxy.BaseConfigured,"favorite wildcard app router configured");
var requestContext=new Microsoft.AspNetCore.Http.DefaultHttpContext();
requestContext.Request.Scheme="https";
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.org");
requestContext.Request.Headers["Origin"]="https://portal.example.org";
requestContext.Request.Headers["Sec-Fetch-Site"]="same-origin";
Assert(FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start accepts exact HTTPS browser origin");
requestContext.Request.Headers["Origin"]="https://untrusted.example.org";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start blocks cross-origin POST");
requestContext.Request.Headers["Origin"]="https://portal.example.org";
requestContext.Request.Headers["Sec-Fetch-Site"]="cross-site";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start blocks cross-site metadata");
requestContext.Request.Headers["Sec-Fetch-Site"]="same-origin";
requestContext.Request.Scheme="http";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start rejects HTTP");
requestContext.Request.Scheme="https";
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("wiki.apps.example.org");
Assert(favoriteProxy.IsFavoriteHost(requestContext),
    "approved or unknown app alias stays on isolated app router");
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.org");
Assert(!favoriteProxy.IsFavoriteHost(requestContext),
    "main Plenaro origin cannot be intercepted as favorite");
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("tickets.example.org");
Assert(!favoriteProxy.IsFavoriteHost(requestContext),
    "Znuny native origin remains on existing proxy");

// A token-free probe must positively identify that the complete
// wildcard/Host/HTTPS routing chain reached the FAVORITE proxy (not Plenaro).
var favoriteProbe=new Microsoft.AspNetCore.Http.DefaultHttpContext();
favoriteProbe.Request.Method="GET";
favoriteProbe.Request.Scheme="https";
favoriteProbe.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "wiki.apps.example.org");
favoriteProbe.Request.Path="/_plenaro/favorite-health";
using var favoriteProbeBody=new MemoryStream();
favoriteProbe.Response.Body=favoriteProbeBody;
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==200 &&
    Encoding.UTF8.GetString(favoriteProbeBody.ToArray())==
        "plenaro-favorite-proxy-ready",
    "favorite wildcard health confirms correct proxy routing without token");
// Confluence may embed additional same-origin frames after a successful login.
// The browser evaluates CSP frame-ancestors against *every* ancestor, not
// only the outer Plenaro document. Preserve the exact, restricted allowlist.
var favoriteCsp=favoriteProbe.Response.Headers["Content-Security-Policy"].ToString();
var favoriteAncestors=favoriteCsp.Split(';',StringSplitOptions.TrimEntries)
    .Single(x=>x.StartsWith("frame-ancestors ",StringComparison.Ordinal));
Assert(favoriteAncestors=="frame-ancestors 'self' https://portal.example.org" &&
    favoriteCsp.Contains("frame-src 'self'",StringComparison.Ordinal) &&
    !favoriteCsp.Contains("frame-ancestors *",StringComparison.Ordinal) &&
    !favoriteProbe.Response.Headers.ContainsKey("X-Frame-Options"),
    "favorite host permits nested same-origin frames and Plenaro only");
favoriteProbe.Request.Method="POST";
favoriteProbeBody.SetLength(0);
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==405,
    "favorite wildcard health is read-only");
favoriteProbe.Request.Method="GET";
favoriteProbe.Request.Scheme="http";
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==404,
    "favorite wildcard health is unavailable via plaintext");


 // The new single-level wildcard aliases coexist with the original
 // slug.base-domain URLs. Classic bookmark IDs, ACLs and sessions stay intact.
 var templateOnly=new ConfigurationBuilder().AddInMemoryCollection(
     new Dictionary<string,string?>{
         ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
         ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
         ["PLENARO_FAVORITES_HOST_TEMPLATE"]="{slug}-plenaro.example.org",
         ["Security:RequireHttps"]="true"
     }).Build();
 var templatedProxy=new FavoriteProxy(templateOnly);
 Assert(templatedProxy.BaseConfigured,
     "single-level template can run without legacy base domain");
 Assert(FavoriteHostNames.PreferredOrigin("wiki",null,
     "{slug}-plenaro.example.org")?.AbsoluteUri=="https://wiki-plenaro.example.org/",
     "new favorites start on one-level TLS wildcard-compatible host");
 Assert(FavoriteHostNames.PreferredOrigin("wiki","apps.example.org",
     null)?.AbsoluteUri=="https://wiki.apps.example.org/",
     "legacy favorites keep original generated URLs without template");
 Assert(FavoriteHostNames.TryGetSlug("wiki-plenaro.example.org",null,
     "{slug}-plenaro.example.org",out var newSlug) && newSlug=="wiki",
     "template alias resolves original favorite slug");
 Assert(FavoriteHostNames.TryGetSlug("wiki.apps.example.org",
     "apps.example.org","{slug}-plenaro.example.org",out var oldSlug) &&
     oldSlug=="wiki","legacy aliases remain routable when template enabled");
 Assert(FavoriteHostGuard.IsUnassignedAlias("wiki-plenaro.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"new aliases isolated from Plenaro login");
 Assert(FavoriteHostGuard.IsUnassignedAlias("a.b-plenaro.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"malformed deep aliases are still isolated");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("portal.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"primary Plenaro host never intercepted");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("tickets.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"Znuny host never intercepted");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("unrelated.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"unrelated hosts remain unchanged");
 foreach(var invalid in new[]{"https://{slug}-plenaro.example.org",
     "*.{slug}.example.org","{slug}.example.org/{slug}",
     "{slug}-plenaro.example.org:443"})
 {
     var rejected=false;
     try{FavoriteHostNames.PreferredOrigin("wiki",null,invalid);}
     catch(InvalidOperationException){rejected=true;}
     Assert(rejected,"invalid hostname templates are rejected");
 }
 var templatedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 templatedRequest.Request.Method="GET";
 templatedRequest.Request.Scheme="https";
 templatedRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "wiki-plenaro.example.org");
 templatedRequest.Request.Path="/_plenaro/favorite-health";
 using(var body=new MemoryStream())
 {
     templatedRequest.Response.Body=body;
     Assert(templatedProxy.IsFavoriteHost(templatedRequest),
         "template alias is handled before identity/static files");
     await templatedProxy.ServeAsync(templatedRequest,CancellationToken.None);
     Assert(templatedRequest.Response.StatusCode==200 &&
         Encoding.UTF8.GetString(body.ToArray())=="plenaro-favorite-proxy-ready",
         "templated host exposes diagnostic without app credentials");
 }
 var legacyAndNew=new ConfigurationBuilder().AddInMemoryCollection(
     new Dictionary<string,string?>{
         ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
         ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
         ["PLENARO_FAVORITES_BASE_DOMAIN"]="apps.example.org",
         ["PLENARO_FAVORITES_HOST_TEMPLATE"]="{slug}-plenaro.example.org",
         ["Security:RequireHttps"]="true"
     }).Build();
 var bothProxy=new FavoriteProxy(legacyAndNew);
 var legacyRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 legacyRequest.Request.Method="GET";
 legacyRequest.Request.Scheme="https";
 legacyRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "wiki.apps.example.org");
 legacyRequest.Request.Path="/_plenaro/favorite-health";
 using(var body=new MemoryStream())
 {
     legacyRequest.Response.Body=body;
     Assert(bothProxy.IsFavoriteHost(legacyRequest),
         "legacy alias still enters favorite proxy after migration");
     await bothProxy.ServeAsync(legacyRequest,CancellationToken.None);
     Assert(legacyRequest.Response.StatusCode==200 &&
         Encoding.UTF8.GetString(body.ToArray())=="plenaro-favorite-proxy-ready",
         "legacy favorite host stays accessible when template configured");
 }
 var malformedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 malformedRequest.Request.Method="GET";
 malformedRequest.Request.Scheme="https";
 malformedRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "a.b-plenaro.example.org");
 malformedRequest.Request.Path="/_plenaro/favorite-health";
 Assert(templatedProxy.IsFavoriteHost(malformedRequest),
     "malformed alias is intercepted, never falls through");
 await templatedProxy.ServeAsync(malformedRequest,CancellationToken.None);
 Assert(malformedRequest.Response.StatusCode==404,
     "malformed aliases return 404 instead of Plenaro UI");


// One *explicit* DNS name for every app path, with legacy aliases preserved.
// A shared browser origin is weaker isolation than per-app origins: path cookie
// scoping is a defense-in-depth measure, not a cross-app security boundary.
var pathOnlyConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_PATH_ORIGIN"]="https://apps-plenaro.example.org",
        ["Security:RequireHttps"]="true"
    }).Build();
// Path-host compatibility mode: Confluence plugins generate /rest and /s
// despite corrected HTML context metadata. Fix URLs in a selected iframe's
// browser before requests are sent, never by rewriting external JS bundles,
// never by exposing /rest or /s directly on the shared host root.
Assert(FavoritePathRouting.BrowserShimEnabled("wiki, docs","wiki") &&
    !FavoritePathRouting.BrowserShimEnabled("wiki, docs","cloud") &&
    !FavoritePathRouting.BrowserShimEnabled(null,"wiki"),
    "browser compatibility shim enabled only for explicit safe slugs");
var invalidShimSlug=false;
try{FavoritePathRouting.BrowserShimEnabled("wiki,../rest","wiki");}
catch(InvalidOperationException){invalidShimSlug=true;}
Assert(invalidShimSlug,"invalid browser shim slug is rejected");
var injectShim=typeof(FavoriteProxy).GetMethod("InjectBrowserPathShim",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var fixtureHtml="<html><head><script>var matcher=/\"/g;"+
    "var contextPath='';</script></head><body></body></html>";
var shimmed=(string)injectShim.Invoke(null,new object[]{
    fixtureHtml,"/wiki"})!;
Assert(shimmed.Contains("data-plenaro-browser-shim",StringComparison.Ordinal)&&
    shimmed.Contains("const base = \"/wiki\";",StringComparison.Ordinal)&&
    shimmed.Contains("const routes = [\"/rest/\", \"/s/\"]",StringComparison.Ordinal)&&
    shimmed.Contains("XMLHttpRequest.prototype.open",StringComparison.Ordinal)&&
    shimmed.Contains("window.fetch = function",StringComparison.Ordinal)&&
    shimmed.Contains("class PlenaroScopedRequest extends NativeRequest",StringComparison.Ordinal)&&
    shimmed.Contains("input = rewrite(input);",StringComparison.Ordinal)&&
    shimmed.Contains("patchAssetUrlProperty(HTMLScriptElement.prototype, \"src\")",StringComparison.Ordinal)&&
    shimmed.Contains("patchAssetUrlProperty(HTMLLinkElement.prototype, \"href\")",StringComparison.Ordinal)&&
    shimmed.Contains("patchAssetUrlProperty(HTMLImageElement.prototype, \"src\")",StringComparison.Ordinal)&&
    shimmed.Contains("Node.prototype.appendChild",StringComparison.Ordinal)&&
    shimmed.Contains("<script>var matcher=/\"/g;var contextPath='';</script>",
        StringComparison.Ordinal)&&
    shimmed.IndexOf("data-plenaro-browser-shim",StringComparison.Ordinal)<
        shimmed.IndexOf("var matcher",StringComparison.Ordinal),
    "browser path shim precedes app scripts; original inline JS is untouched");
Assert((string?)injectShim.Invoke(null,new object[]{shimmed,"/wiki"})==shimmed,
    "browser shim injection is idempotent");
Assert((string?)injectShim.Invoke(null,new object[]{fixtureHtml,""})==fixtureHtml,
    "dedicated-origin HTML is not modified");
var invalidPrefixRejected=false;
try{injectShim.Invoke(null,new object[]{fixtureHtml,"/../../rest"});}
catch(System.Reflection.TargetInvocationException e) when(
    e.InnerException is InvalidOperationException){invalidPrefixRejected=true;}
Assert(invalidPrefixRejected,"untrusted browser shim prefix is rejected");

// A sanitized real Confluence login HAR shows successful POST
// /wiki/rest/tsv/1.0/authenticate -> {"next":"/"} (200), followed by
// browser navigation to shared host root "/" (404). Only the successful
// plugin endpoint JSON navigation field may receive the /wiki/ prefix.
var loginRewrite=typeof(FavoriteProxy).GetMethod("RewriteConfluenceLoginNext",
    BindingFlags.NonPublic|BindingFlags.Static)!;
string LoginNext(string json,string prefix)=>
    (string)loginRewrite.Invoke(null,new object[]{json,prefix})!;
const string nextRoot = """{"next":"/"}""";
const string nextWiki = """{"next":"/wiki/"}""";
const string nextSubPath = """{"next":"/display/SPACE?a=1","ok":true}""";
const string nextSubPathRewritten = """{"next":"/wiki/display/SPACE?a=1","ok":true}""";
Assert(LoginNext(nextRoot,"/wiki")==nextWiki &&
    LoginNext(nextSubPath,"/wiki")==nextSubPathRewritten &&
    LoginNext("""{"next":"/wiki/display/SPACE"}""","/wiki")
        == """{"next":"/wiki/display/SPACE"}""" &&
    LoginNext("""{"next":"//outside.invalid/"}""","/wiki")
        == """{"next":"//outside.invalid/"}""" &&
    LoginNext("""{"next":"https://outside.invalid/"}""","/wiki")
        == """{"next":"https://outside.invalid/"}""" &&
    LoginNext(nextRoot,"")==nextRoot &&
    LoginNext("{broken","/wiki")=="{broken",
    "Confluence login JSON 'next' rewrite is narrow, prefix-safe and idempotent");
var loginRewriteGate=typeof(FavoriteProxy).GetMethod(
    "ShouldRewriteConfluenceLoginNext",
    BindingFlags.NonPublic|BindingFlags.Static)!;
bool ShouldLoginRewrite(string method,System.Net.HttpStatusCode status,
    string path,string prefix,string slug,string media,string slugs)=>
    (bool)loginRewriteGate.Invoke(null,new object?[]{
        method,status,path,prefix,slug,
        new System.Net.Http.Headers.MediaTypeHeaderValue(media),slugs})!;
Assert(ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","/wiki","wiki",
        "application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.Unauthorized,
        "/rest/tsv/1.0/authenticate","/wiki","wiki",
        "application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","","wiki","application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","/cloud","cloud",
        "application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","/wiki","wiki",
        "application/json","") &&
    !ShouldLoginRewrite("GET",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","/wiki","wiki",
        "application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/other/1.0/authenticate","/wiki","wiki",
        "application/json","wiki") &&
    !ShouldLoginRewrite("POST",System.Net.HttpStatusCode.OK,
        "/rest/tsv/1.0/authenticate","/wiki","wiki",
        "text/html","wiki"),
    "Confluence login rewrite gated by exact endpoint, success, mime and opted-in slug");

// Confluence plugins use origin-root /rest and /s paths. Keep their session
// on a dedicated host with cookie Path=/ while all unrelated favorites keep
// the existing shared /<slug>/ host and their /<slug> cookie scope.
var mixedOriginConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_PATH_ORIGIN"]="https://apps-plenaro.example.org",
        ["PLENARO_FAVORITES_HOST_TEMPLATE"]="{slug}-plenaro.example.org",
        ["PLENARO_FAVORITES_DEDICATED_SLUGS"]="wiki, confluence",
        ["Security:RequireHttps"]="true"
    }).Build();
var mixedOriginProxy=new FavoriteProxy(mixedOriginConfig);
var originMethod=typeof(FavoriteProxy).GetMethod("ProxyOrigin",
    BindingFlags.NonPublic|BindingFlags.Instance)!;
Uri? MixedOrigin(string slug)=>originMethod.Invoke(mixedOriginProxy,
    new object[]{slug}) as Uri;
Assert(MixedOrigin("wiki")?.AbsoluteUri=="https://wiki-plenaro.example.org/" &&
    MixedOrigin("cloud")?.AbsoluteUri=="https://apps-plenaro.example.org/cloud/" &&
    MixedOrigin("confluence")?.AbsoluteUri=="https://confluence-plenaro.example.org/",
    "selected Confluence favorites get dedicated origin, other slugs stay on path host");
Assert(FavoriteHostNames.PreferDedicated("wiki, confluence","wiki") &&
    !FavoriteHostNames.PreferDedicated("wiki, confluence","cloud") &&
    !FavoriteHostNames.PreferDedicated(null,"wiki"),
    "dedicated origin preference is explicit, scoped and off by default");
var isolatedProbe=new Microsoft.AspNetCore.Http.DefaultHttpContext();
isolatedProbe.Request.Scheme="https";
isolatedProbe.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "wiki-plenaro.example.org");
Assert(mixedOriginProxy.IsFavoriteHost(isolatedProbe),
    "dedicated alias is intercepted before primary Plenaro routes");
isolatedProbe.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "apps-plenaro.example.org");
Assert(mixedOriginProxy.IsFavoriteHost(isolatedProbe),
    "shared path-host continues serving unrelated favorites");
var noAliasConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_PATH_ORIGIN"]="https://apps-plenaro.example.org",
        ["PLENARO_FAVORITES_DEDICATED_SLUGS"]="wiki",
        ["Security:RequireHttps"]="true"
    }).Build();
Assert(originMethod.Invoke(new FavoriteProxy(noAliasConfig),new object[]{"wiki"}) is null,
    "selected dedicated host fails closed without configured domain/template");
var invalidSlugRejected=false;
try{FavoriteHostNames.PreferDedicated("wiki,../admin","wiki");}
catch(InvalidOperationException){invalidSlugRejected=true;}
Assert(invalidSlugRejected,
    "malformed allowlist fails closed rather than accepting partial configuration");

var sharedProxy=new FavoriteProxy(pathOnlyConfig);
Assert(sharedProxy.BaseConfigured,"path-host router is opt-in without subdomain wildcard");
Assert(FavoritePathRouting.SharedOrigin("https://apps-plenaro.example.org",
    "https://portal.example.org","https://tickets.example.org")?.Host==
    "apps-plenaro.example.org","shared path host is distinct from login");
Assert(FavoritePathRouting.TrySplitPath("/wiki/api/search",out var pathSlug,
    out var upstreamRest) && pathSlug=="wiki" && upstreamRest=="/api/search",
    "path router strips only approved first segment");
Assert(FavoritePathRouting.TrySplitPath("/wiki",out var rootSlug,
    out var rootRest) && rootSlug=="wiki" && rootRest=="/",
    "app root without trailing slash is recognized");
Assert(!FavoritePathRouting.TrySplitPath("/api/v1/users",out _,out _) &&
    !FavoritePathRouting.TrySplitPath("/_plenaro/favorite-bootstrap",out _,out _) &&
    !FavoritePathRouting.TrySplitPath("/../../etc",out _,out _),
    "reserved, malformed and traversing roots cannot become a favorite");
Assert(FavoritePathRouting.CookiePath("wiki")=="/wiki" &&
    FavoritePathRouting.AppPath("wiki","/index.php")=="/wiki/index.php",
    "per-app cookie path and navigation paths are stable");
foreach(var invalid in new[]{
    "http://apps-plenaro.example.org","https://portal.example.org",
    "https://tickets.example.org","https://apps-plenaro.example.org/x",
    "https://apps-plenaro.example.org:8443"})
{
    var rejected=false;
    try{FavoritePathRouting.SharedOrigin(invalid,
        "https://portal.example.org","https://tickets.example.org");}
    catch(InvalidOperationException){rejected=true;}
    Assert(rejected,"invalid or shared primary login origin fails closed");
}
var sharedProbe=new Microsoft.AspNetCore.Http.DefaultHttpContext();
sharedProbe.Request.Method="GET";
sharedProbe.Request.Scheme="https";
sharedProbe.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "apps-plenaro.example.org");
sharedProbe.Request.Path="/_plenaro/favorite-health";
Assert(sharedProxy.IsFavoriteHost(sharedProbe),
    "path host intercepted before Plenaro static/identity middleware");
using(var body=new MemoryStream())
{
    sharedProbe.Response.Body=body;
    await sharedProxy.ServeAsync(sharedProbe,CancellationToken.None);
    Assert(sharedProbe.Response.StatusCode==200 &&
        Encoding.UTF8.GetString(body.ToArray())=="plenaro-favorite-proxy-ready",
        "single path host passes tokenless DNS/Nginx/TLS health probe");
}
sharedProbe.Request.Path="/";
await sharedProxy.ServeAsync(sharedProbe,CancellationToken.None);
Assert(sharedProbe.Response.StatusCode==404,
    "shared host root never exposes Plenaro login");
// In the sanitized Confluence login trace, the successful login plugin
// navigates to GET / with Referer /wiki/login.action and iframe destination.
// Root remains 404 generally; only this exact same-origin navigation may
// redirect to /wiki/, which enforces its original session/ACL normally.
var recoveryConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_PATH_ORIGIN"]="https://apps-plenaro.example.org",
        ["PLENARO_FAVORITES_BROWSER_SHIM_SLUGS"]="wiki",
        ["Security:RequireHttps"]="true"
    }).Build();
var recoveryProxy=new FavoriteProxy(recoveryConfig);
var rootNavigation=new Microsoft.AspNetCore.Http.DefaultHttpContext();
rootNavigation.Request.Method="GET";
rootNavigation.Request.Scheme="https";
rootNavigation.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "apps-plenaro.example.org");
rootNavigation.Request.Path="/";
rootNavigation.Request.Headers["Sec-Fetch-Dest"]="iframe";
rootNavigation.Request.Headers["Sec-Fetch-Site"]="same-origin";
rootNavigation.Request.Headers["Referer"]=
    "https://apps-plenaro.example.org/wiki/login.action?os_destination=%2F";
Assert(FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "Confluence same-origin login iframe navigation recognized exactly");
using(var body=new MemoryStream()){
    rootNavigation.Response.Body=body;
    await recoveryProxy.ServeAsync(rootNavigation,CancellationToken.None);
    Assert(rootNavigation.Response.StatusCode==303 &&
        rootNavigation.Response.Headers.Location=="/wiki/" &&
        rootNavigation.Response.Headers["X-Plenaro-Login-Reroute"]=="wiki-root" &&
        body.Length==0,
        "login-root fallback only redirects to ACL protected /wiki/ without content");
}
rootNavigation.Request.Headers["Referer"]=
    "https://apps-plenaro.example.org/cloud/login.action";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback never chooses another app from Referer");
rootNavigation.Request.Headers["Referer"]=
    "https://other.example.org/wiki/login.action";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback denies cross-origin spoofed Referer");
rootNavigation.Request.Headers["Referer"]=
    "https://apps-plenaro.example.org/wiki/login.action";
rootNavigation.Request.Headers["Sec-Fetch-Site"]="cross-site";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback requires same-origin browser navigation");
rootNavigation.Request.Headers["Sec-Fetch-Site"]="same-origin";
rootNavigation.Request.Headers["Sec-Fetch-Dest"]="empty";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback refuses AJAX requests");
rootNavigation.Request.Headers["Sec-Fetch-Dest"]="iframe";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"cloud"),
    "login fallback is opt-in only for wiki");
rootNavigation.Request.Method="POST";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback refuses root form POST");
rootNavigation.Request.Method="GET";
rootNavigation.Request.Path="/api/v1/users";
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback refuses API root");
rootNavigation.Request.Path="/";
rootNavigation.Request.QueryString=new Microsoft.AspNetCore.Http.QueryString("?query=1");
Assert(!FavoritePathRouting.ConfluenceLoginRootNavigation(
    rootNavigation.Request,"wiki"),
    "login fallback refuses arbitrary root query");

sharedProbe.Request.Path="/api/v1/users";
await sharedProxy.ServeAsync(sharedProbe,CancellationToken.None);
Assert(sharedProbe.Response.StatusCode==404,
    "shared host does not expose Plenaro APIs");
sharedProbe.Request.Path="/wiki/css/site.css";
await sharedProxy.ServeAsync(sharedProbe,CancellationToken.None);
Assert(sharedProbe.Response.StatusCode==401,
    "path resource requires independent valid favorite cookie");
// Only /<slug>/ routes create the app frame policy. The public healthcheck
// exits before that policy is applied, so test on an actual favorite path.
var sharedCsp=sharedProbe.Response.Headers["Content-Security-Policy"].ToString();
var sharedAncestors=sharedCsp.Split(';',StringSplitOptions.TrimEntries)
    .Single(x=>x.StartsWith("frame-ancestors ",StringComparison.Ordinal));
Assert(sharedAncestors=="frame-ancestors 'self' https://portal.example.org" &&
    sharedCsp.Contains("frame-src 'self'",StringComparison.Ordinal) &&
    !sharedProbe.Response.Headers.ContainsKey("X-Frame-Options"),
    "shared /wiki/ host permits own nested login frames and Plenaro only");
sharedProbe.Request.Path="/cloud/_plenaro/favorite-bootstrap";
await sharedProxy.ServeAsync(sharedProbe,CancellationToken.None);
Assert(sharedProbe.Response.StatusCode==401,
    "another favorite cannot bootstrap without one-time session token");
var rewriteMethod=typeof(FavoriteProxy).GetMethod("Rewrite",
    BindingFlags.NonPublic|BindingFlags.Static)!;
var rewrite=(string)rewriteMethod.Invoke(null,new object[]{
    "<a href=\"/wiki-page\">Wiki</a><script src=\"/bundle.js\"></script>"+
    "<form action=\"/login\"></form><img src=\"//cdn.example.org/pic\">"+
    "https://wiki.example.org/help",
    new Uri("https://wiki.example.org/"),
    new Uri("https://apps-plenaro.example.org/wiki/"),"/wiki"})!;
Assert(rewrite.Contains("href=\"/wiki/wiki-page\"") &&
    rewrite.Contains("src=\"/wiki/bundle.js\"") &&
    rewrite.Contains("action=\"/wiki/login\"") &&
    rewrite.Contains("src=\"//cdn.example.org/pic\"") &&
    rewrite.Contains("https://apps-plenaro.example.org/wiki/help"),
    "path-prefixed resource rewriting preserves protocol-relative external URLs");
var plainRewrite=(string)rewriteMethod.Invoke(null,new object[]{
    "<a href=\"/docs\">docs</a> https://wiki.example.org/help",
    new Uri("https://wiki.example.org/"),
    new Uri("https://wiki-plenaro.example.org/"),""})!;
Assert(plainRewrite.Contains("href=\"/docs\"") &&
    plainRewrite.Contains("https://wiki-plenaro.example.org/help"),
    "existing subdomain proxy response stays unchanged");

// Regression from a sanitized Confluence contextbatch/batch.js HAR:
// the old global rewriter produced ".replace(/"/wiki/g,...)" and corrupted
// JS regex flags, so AMD define/AJS never initialized despite HTTP 200.
var rewriteMimeMethod=typeof(FavoriteProxy).GetMethod("RewriteMime",
    BindingFlags.NonPublic|BindingFlags.Static)!;
bool WillRewrite(string mime)=>(bool)rewriteMimeMethod.Invoke(null,new object[]{
    new System.Net.Http.Headers.MediaTypeHeaderValue(mime)})!;
Assert(!WillRewrite("text/javascript") &&
    !WillRewrite("application/javascript") &&
    !WillRewrite("application/x-javascript") &&
    !WillRewrite("text/ecmascript") &&
    !WillRewrite("application/ecmascript") &&
    !WillRewrite("application/json") &&
    !WillRewrite("application/problem+json") &&
    !WillRewrite("text/plain") &&
    WillRewrite("text/html") &&
    WillRewrite("text/css") &&
    WillRewrite("application/xhtml+xml") &&
    WillRewrite("image/svg+xml"),
    "executable JS and API JSON are streamed unchanged; HTML/CSS still rewritten");

// Inline scripts must also be excluded from HTML rewriting, while external
// script src, CSS, images and Confluence metadata remain path-prefixed.
var htmlSafeMethod=typeof(FavoriteProxy).GetMethod(
    "RewriteHtmlPreservingScripts",BindingFlags.NonPublic|BindingFlags.Static)!;
var inlineJs="<script>var k=function(e){return e.replace(/\"/g,\"&quot;\")"+
    ".replace(/'/g,\"&#39;\")};fetch('/api/js');</script>";
var mixedHtml="<meta name=\"ajs-context-path\" content=\"\">"+
    inlineJs+"<script src=\"/assets/batch.js\"></script>"+
    "<link href=\"/assets/site.css\" rel=\"stylesheet\">"+
    "<img src=\"/logos/brand.svg\">";
var protectedHtml=(string)htmlSafeMethod.Invoke(null,new object[]{
    mixedHtml,new Uri("https://old.example.org/"),
    new Uri("https://apps-plenaro.example.org/wiki/"),"/wiki"})!;
Assert(protectedHtml.Contains(inlineJs,StringComparison.Ordinal) &&
    protectedHtml.Contains("src=\"/wiki/assets/batch.js\"") &&
    protectedHtml.Contains("href=\"/wiki/assets/site.css\"") &&
    protectedHtml.Contains("src=\"/wiki/logos/brand.svg\"") &&
    protectedHtml.Contains("name=\"ajs-context-path\" content=\"/wiki\"") &&
    !protectedHtml.Contains(".replace(/\"/wiki/g") &&
    !protectedHtml.Contains(".replace(/'/wiki/g"),
    "inline JS regex literals preserved, external JS/CSS/logo URLs still prefixed");

// Shared path-host assets: rewrite root-relative HTML/CSS/JS URLs while
// leaving already-prefixed routes and external protocol-relative URLs alone.
var htmlResourcesMethod=typeof(FavoriteProxy).GetMethod("RewriteHtmlResources",
    BindingFlags.NonPublic|BindingFlags.Static)!;
var htmlAssets="<link href=/assets/site.css><script src=/js/app.js></script>"+
    "<img srcset=\"/one.png 1x, /two.png 2x, //cdn.example.org/three.png 3x, /wiki/keep.png 4x\">";
var htmlAbsolute=(string)rewriteMethod.Invoke(null,new object[]{
    htmlAssets,new Uri("https://old.example.org/"),
    new Uri("https://apps-plenaro.example.org/wiki/"),"/wiki"})!;
var htmlFixed=(string)htmlResourcesMethod.Invoke(null,
    new object[]{htmlAbsolute,"/wiki"})!;
Assert(htmlFixed.Contains("href=/wiki/assets/site.css") &&
    htmlFixed.Contains("src=/wiki/js/app.js") &&
    htmlFixed.Contains("/wiki/one.png 1x, /wiki/two.png 2x") &&
    htmlFixed.Contains("//cdn.example.org/three.png 3x") &&
    htmlFixed.Contains("/wiki/keep.png 4x") &&
    !htmlFixed.Contains("/wiki/wiki/keep.png"),
    "unquoted HTML assets and every srcset candidate stay within the app");
var cssText="@import \"/theme.css\"; .a{background:url(/img/bg.svg)}"+
    ".b{mask:url('/wiki/keep.svg')}";
var cssFixed=(string)rewriteMethod.Invoke(null,new object[]{
    cssText,new Uri("https://old.example.org/"),
    new Uri("https://apps-plenaro.example.org/wiki/"),"/wiki"})!;
Assert(cssFixed.Contains("\"/wiki/theme.css\"") &&
    cssFixed.Contains("url(/wiki/img/bg.svg)") &&
    cssFixed.Contains("url('/wiki/keep.svg')") &&
    !cssFixed.Contains("/wiki/wiki/keep.svg"),
    "CSS imports, fonts and unquoted URL() roots are prefixed exactly once");
var jsText="fetch(`/api/search`);import('/chunks/view.mjs');"+
    "const already='/wiki/assets/site.css';";
var jsFixed=(string)rewriteMethod.Invoke(null,new object[]{
    jsText,new Uri("https://old.example.org/"),
    new Uri("https://apps-plenaro.example.org/wiki/"),"/wiki"})!;
Assert(jsFixed.Contains("fetch(`/wiki/api/search`)") &&
    jsFixed.Contains("import('/wiki/chunks/view.mjs')") &&
    jsFixed.Contains("'/wiki/assets/site.css'") &&
    !jsFixed.Contains("/wiki/wiki/assets"),
    "JS template literals, dynamic chunks and existing app paths stay valid");
var correctMimeMethod=typeof(FavoriteProxy).GetMethod("CorrectAssetMediaType",
    BindingFlags.NonPublic|BindingFlags.Static)!;
string? CorrectMime(string path,string? mime)=>
    correctMimeMethod.Invoke(null,new object?[]{
        path,mime is null?null:
            new System.Net.Http.Headers.MediaTypeHeaderValue(mime)}) as string;
Assert(CorrectMime("/assets/site.css","application/octet-stream")=="text/css" &&
    CorrectMime("/js/app.mjs","text/plain")=="text/javascript" &&
    CorrectMime("/js/app.js",null)=="text/javascript" &&
    CorrectMime("/fonts/cache.wasm","application/octet-stream")=="application/wasm" &&
    CorrectMime("/img/photo.png","application/octet-stream") is null &&
    CorrectMime("/css/site.css","text/css") is null,
    "X-Content-Type-Options nosniff can serve CSS/JS with generic upstream MIME");
var slashRoot=new Microsoft.AspNetCore.Http.DefaultHttpContext();
slashRoot.Request.Method="GET";
slashRoot.Request.Scheme="https";
slashRoot.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "apps-plenaro.example.org");
slashRoot.Request.Path="/wiki";
slashRoot.Request.QueryString=new Microsoft.AspNetCore.Http.QueryString("?page=2");
await sharedProxy.ServeAsync(slashRoot,CancellationToken.None);
Assert(slashRoot.Response.StatusCode==307 &&
    slashRoot.Response.Headers.Location.ToString()=="/wiki/?page=2",
    "shared app root canonicalized to /wiki/ so relative CSS/JS resolve within app");

// Wiki frameworks such as Confluence build dynamic REST/web-resource URLs
// from ajs-context-path rather than from the rewritten static HTML links.
// The upstream root context must be replaced with the public /<slug> path.
var contextMethod=typeof(FavoriteProxy).GetMethod("RewriteHtmlAppContext",
    BindingFlags.NonPublic|BindingFlags.Static)!;
string RewriteContext(string html,string path)=>
    (string)contextMethod.Invoke(null,new object[]{html,path})!;
var metaEmpty="<meta name=\"ajs-context-path\" content=\"\">"+
    "<meta name='confluence-context-path' content='/'>"+
    "<meta name=\"unrelated\" content=\"\">"+
    "<meta name=\"ajs-context-path\" content=\"/wiki\">"+
    "<meta name=\"ajs-context-path\" content=\"/wiki/confluence\">";
var metaFixed=RewriteContext(metaEmpty,"/wiki");
Assert(metaFixed.Contains("name=\"ajs-context-path\" content=\"/wiki\"") &&
    metaFixed.Contains("name='confluence-context-path' content=\"/wiki\"") &&
    metaFixed.Contains("name=\"unrelated\" content=\"\"") &&
    !metaFixed.Contains("/wiki/wiki"),
    "Confluence path metadata becomes /wiki without modifying other meta values");
var previousContext=RewriteContext(
    "<meta name=\"ajs-context-path\" content=\"/confluence\">","/wiki");
Assert(previousContext.Contains("content=\"/wiki/confluence\"") &&
    RewriteContext(previousContext,"/wiki")==previousContext,
    "non-root application context is preserved and rewriting is idempotent");
Assert(RewriteContext("<meta name=\"ajs-context-path\" content=\"\">","")==
    "<meta name=\"ajs-context-path\" content=\"\">",
    "dedicated-host favorites do not alter upstream context metadata");
var limitMethod=typeof(FavoriteProxy).GetMethod("RewriteLimit",
    BindingFlags.NonPublic|BindingFlags.Static)!;
int RewriteBound(string media)=> (int)limitMethod.Invoke(null,
    new object[]{new System.Net.Http.Headers.MediaTypeHeaderValue(media)})!;
Assert(RewriteBound("text/javascript")>=20*1024*1024 &&
    RewriteBound("application/javascript")>=20*1024*1024 &&
    RewriteBound("text/css")>=20*1024*1024 &&
    RewriteBound("text/html")==6*1024*1024 &&
    RewriteBound("application/json")==6*1024*1024 &&
    RewriteBound("image/svg+xml")==6*1024*1024,
    "large JS/CSS bundles are accepted while HTML/API/other text limits remain strict");

// Znuny article bodies are nested iframes: every ancestor must satisfy
// frame-ancestors. Parent Plenaro plus the proxy's own origin are permitted,
// without allowing unrelated websites or disabling framing protection.
var nativeCspMethod=typeof(ZnunyNativeProxy).GetMethod(
    "BrowserContentSecurityPolicy",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var nativeCsp=(string)nativeCspMethod.Invoke(null,new object[]{
    new Uri("https://portal.example.org")
})!;
var frameAncestors=nativeCsp.Split(';',StringSplitOptions.TrimEntries)
    .Single(x=>x.StartsWith("frame-ancestors ",StringComparison.Ordinal));
Assert(frameAncestors=="frame-ancestors 'self' https://portal.example.org",
    "Znuny nested article iframes may embed under proxy self and Plenaro");
Assert(!frameAncestors.Contains('*') &&
    !frameAncestors.Contains("example.net",StringComparison.Ordinal) &&
    nativeCsp.Contains("frame-src 'self'",StringComparison.Ordinal),
    "Znuny nested frame fix retains restricted framing and child frame policy");

// A global service-account sync can contain far more tickets than the
// authenticated agent can read. The personal session's TicketSearch result
// is the only positive allowlist; a ticket from a foreign queue is excluded.
var personalSearchCalls=0;
var personalSearch=new ZnunyReader(new FakeFactory(request=>{
    var path=request.RequestUri?.AbsolutePath??"";
    if(path.EndsWith("/Session",StringComparison.Ordinal)){
        Assert(request.Method==HttpMethod.Post,
            "permission session uses normal personal Znuny login");
        using var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync()
            .GetAwaiter().GetResult());
        Assert(body.RootElement.GetProperty("UserLogin").GetString()=="ci-agent",
            "queue permissions are checked as the signed-in agent");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("{\"SessionID\":\"personal-only\"}",
                Encoding.UTF8,"application/json")
        };
    }
    if(path.EndsWith("/Ticket",StringComparison.Ordinal)){
        personalSearchCalls++;
        var query=System.Web.HttpUtility.ParseQueryString(
            request.RequestUri!.Query);
        Assert(query["SessionID"]=="personal-only" &&
            query["TicketCreateTimeNewerDate"] is not null,
            "TicketSearch uses personal session and explicit date window");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("{\"TicketID\":[\"77\",\"79\"]}",
                Encoding.UTF8,"application/json")
        };
    }
    if(path.EndsWith("/Ticket/77",StringComparison.Ordinal)){
        Assert(request.RequestUri!.Query.Contains("SessionID=personal-only"),
            "ticket detail permission verifies with personal TicketGet");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(
                "{\"Ticket\":{\"TicketID\":\"77\",\"TicketNumber\":\"2026#77\",\"Title\":\"Authorized\",\"Queue\":\"IT\",\"State\":\"open\",\"StateType\":\"open\"}}",
                Encoding.UTF8,"application/json")
        };
    }
    throw new Exception("Unexpected mock Znuny URL");
}));
var personalSettings=new ServerSettings(ApiUrl:
    "https://znuny.example.org/otrs/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST",
    SearchLimit:100);
var personalCounter=new ZnunyReader.Counter(5);
var personalSession=await personalSearch.SessionAsync(personalSettings,
    "ci-agent","ci-password",personalCounter,CancellationToken.None);
var personalAuthorized=await personalSearch.SearchWindowAsync(
    personalSettings,personalSession,"created",
    new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero),
    new DateTimeOffset(2026,11,9,0,0,0,TimeSpan.Zero),
    personalCounter,CancellationToken.None);
Assert(personalSearchCalls==1,
    "single unsaturated per-agent search produces authoritative ticket IDs");
var restricted=ZnunyTicketVisibility.Intersect(
    new[]{"42","77","78","79","80"},personalAuthorized.ToHashSet());
Assert(restricted.SequenceEqual(new[]{"77","79"}) && !restricted.Contains("42"),
    "central cache rows from foreign queues excluded by per-agent search");
var checkedTicket=await personalSearch.GetAsync(personalSettings,personalSession,
    "77",personalCounter,CancellationToken.None);
Assert(checkedTicket.Id=="77" && checkedTicket.Queue=="IT",
    "direct ticket reads revalidate permissions under agent identity");

var settings=new ServerSettings(ApiUrl:"https://znuny.test/otrs/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST",
    WebUrl:"https://znuny.test/otrs/index.pl");

static void Assert(bool condition,string description)
{
    if(!condition)throw new Exception("FAILED: "+description);
    Console.WriteLine("PASS: "+description);
}
static bool Ack(string json)
{
    using var doc=JsonDocument.Parse(json);
    return ZnunyReader.ConfirmedUpdate(doc.RootElement,"77");
}
foreach(var good in new[]{
    "{\"Success\":1}","{\"Success\":true}","{\"Success\":\"1\"}",
    "{\"TicketID\":77}","{\"TicketID\":\"77\"}",
    "{\"Data\":{\"TicketID\":77}}","{\"ArticleID\":812}"
})Assert(Ack(good),"Znuny success ACK "+good);
foreach(var bad in new[]{
    "{}","{\"Success\":0}","{\"Success\":false,\"TicketID\":77}",
    "{\"TicketID\":\"999\"}","{\"ArticleID\":0}",
    "{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"},\"Success\":1}"
})Assert(!Ack(bad),"reject ambiguous/mismatch ACK "+bad);

async Task<(bool Confirmed,JsonElement? Sent)> SendWrite(string response,int status,
    Dictionary<string,object> changes)
{
    var requests=0;
    JsonElement? sent=null;
    var factory=new FakeFactory(request=>{
        requests++;
        Assert(request.Method==HttpMethod.Post,"TicketUpdate HTTP POST");
        Assert(request.RequestUri?.AbsolutePath.EndsWith("/Ticket/77/Update")==true,
            "configured legacy TicketUpdate route respected");
        using var doc=JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        sent=doc.RootElement.Clone();
        return new HttpResponseMessage((HttpStatusCode)status){
            Content=new StringContent(response,Encoding.UTF8,"application/json")
        };
    });
    var reader=new ZnunyReader(factory);
    var result=await reader.UpdateAsync(settings,"session", "77",changes,
        new ZnunyReader.Counter(2),CancellationToken.None);
    Assert(requests==1,"one remote TicketUpdate, never retried");
    return(result,sent);
}

var reply=new Dictionary<string,object>{
    ["Article"]=new Dictionary<string,object>{
        ["CommunicationChannel"]="Email",["SenderType"]="agent",
        ["IsVisibleForCustomer"]=1,["ArticleSend"]=1,
        ["AppendSignatureToBody"]=1,["To"]="kunde@example.org",
        ["Subject"]="Re: Test",["Body"]="Hallo Kunde",
        ["ContentType"]="text/html; charset=utf-8",["MimeType"]="text/html",
        ["Charset"]="utf-8"
    }
};
var normal=await SendWrite("{\"Success\":1}",200,reply);
Assert(normal.Confirmed,"reply Success=1 accepted");
var actual=normal.Sent!.Value;
Assert(actual.GetProperty("SessionID").GetString()=="session" &&
    actual.GetProperty("TicketID").GetString()=="77" &&
    actual.GetProperty("Article").GetProperty("ArticleSend").GetInt32()==1 &&
    actual.GetProperty("Article").GetProperty("To").GetString()=="kunde@example.org",
    "reply uses Classic outbound-email payload");
var close=new Dictionary<string,object>(reply){
    ["Ticket"]=new Dictionary<string,object>{["State"]="closed successful"}
};
var closeResult=await SendWrite("{\"Success\":1}",200,close);
Assert(closeResult.Confirmed &&
    closeResult.Sent!.Value.GetProperty("Ticket").GetProperty("State")
        .GetString()=="closed successful",
    "answer + close in one TicketUpdate");
var booking=new Dictionary<string,object>{
    ["Article"]=new Dictionary<string,object>{
        ["Subject"]="TaskTool Zeitbuchung",["Body"]="Prüfung",
        ["CommunicationChannel"]="Internal",["SenderType"]="agent",
        ["TimeUnit"]=30,["IsVisibleForCustomer"]=0,
        ["ContentType"]="text/plain; charset=utf-8",
        ["MimeType"]="text/plain",["Charset"]="utf-8"
    },
    ["DynamicField"]=new[]{
        new Dictionary<string,string>{{"Name","KostenstelleID"},{"Value","100"}},
        new Dictionary<string,string>{{"Name","AuftragsID"},{"Value","200"}}
    }
};
var bookingResult=await SendWrite("{\"Success\":1}",200,booking);
Assert(bookingResult.Confirmed &&
    bookingResult.Sent!.Value.GetProperty("Article").GetProperty("TimeUnit").GetInt32()==30 &&
    bookingResult.Sent!.Value.GetProperty("DynamicField").GetArrayLength()==2,
    "time booking confirmed with exact 15-min-rounded TimeUnit and dynamic fields");
var assigned=await SendWrite("{\"TicketID\":77}",200,
    new Dictionary<string,object>{["Ticket"]=new Dictionary<string,object>{
        ["OwnerID"]=145,["ResponsibleID"]=200
    }});
Assert(assigned.Confirmed,"assignment accepts legacy ID ACK");
var unconfirmed=await SendWrite("{}",200,reply);
Assert(!unconfirmed.Confirmed,"empty 200 does NOT imply successful write");

async Task ExpectFailure(string body,int status,string code,bool rejected)
{
    var reader=new ZnunyReader(new FakeFactory(_=>
        new HttpResponseMessage((HttpStatusCode)status){
            Content=new StringContent(body,Encoding.UTF8,"application/json")
        }));
    try{
        await reader.UpdateAsync(settings,"session","77",reply,
            new ZnunyReader.Counter(2),CancellationToken.None);
        throw new Exception("Expected remote failure "+code);
    }catch(ZnunyReader.RemoteFailure ex){
        Assert(ex.Code==code && ex.DefinitivelyRejected==rejected,
            "remote "+status+" is "+code+" and rejected="+rejected);
        if(code=="remote-rejected" && body.Contains("ErrorCode",StringComparison.Ordinal))
            Assert(ex.RemoteCode=="TicketUpdate.InvalidParameter",
                "safe diagnostic exposes Znuny machine code only");
        if(code=="remote-rejected" && !body.Contains("ErrorCode",StringComparison.Ordinal))
            Assert(ex.RemoteCode is null,"no invented Znuny error code");
    }
}
await ExpectFailure("{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"}}",
    200,"remote-rejected",true);
await ExpectFailure("{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"}}",
    400,"remote-rejected",true);
await ExpectFailure("{}",404,"route-not-found",true);
await ExpectFailure("{}",401,"authentication",true);
await ExpectFailure("{}",500,"server-error",false);
await ExpectFailure("{}",502,"server-error",false);
// Standard modern Znuny mapping uses PATCH /Ticket/{ID}; it is selected
// by the administrator, never attempted as a fallback to an ambiguous POST.
var patchCalls=0;
var patched=new ZnunyReader(new FakeFactory(request=>{
    patchCalls++;
    Assert(request.Method==HttpMethod.Patch &&
        request.RequestUri?.AbsolutePath.EndsWith("/Ticket/77")==true,
        "modern Znuny PATCH TicketUpdate route, not a second POST");
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent("{\"Success\":1}",Encoding.UTF8,"application/json")
    };
}));
var modern=settings with {TicketUpdateRoute="/Ticket/{TicketID}",TicketUpdateMethod="PATCH"};
Assert(await patched.UpdateAsync(modern,"session","77",reply,
        new ZnunyReader.Counter(2),CancellationToken.None) && patchCalls==1,
    "configured PATCH contract accepted with exactly one write");
await ExpectFailure("{\"Success\":0}",200,"remote-rejected",true);
// A 200 response to SessionCreate may contain an API error rather than a
// SessionID. Never attempt TicketGet or a write after such a response.
var loginCalls=0;
var badLogin=new ZnunyReader(new FakeFactory(request=>{
    loginCalls++;
    Assert(request.RequestUri?.AbsolutePath.EndsWith("/Session")==true,
        "personal login stops before TicketGet when session invalid");
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(
            """{"Error":{"ErrorCode":"SessionCreate.AuthFail"}}""",
            Encoding.UTF8,"application/json")
    };
}));
try{
    await badLogin.SessionAsync(settings,"bad-user","bad-password",
        new ZnunyReader.Counter(2),CancellationToken.None);
    throw new Exception("SessionCreate missing SessionID accepted unexpectedly");
}catch(ZnunyReader.RemoteFailure e){
    Assert(e.Code=="remote-rejected" &&
        e.RemoteCode=="SessionCreate.AuthFail" && loginCalls==1,
        "personal SessionCreate errors are recognized without attempting writes");
}

// A stock Znuny connector may lack the *optional* DynamicField/Options
// endpoint; admin-configured keys must still allow a time booking to proceed.
var optionCalls=0;
var optionReader=new ZnunyReader(new FakeFactory(request=>{
    var path=request.RequestUri?.AbsolutePath??"";
    if(path.EndsWith("/Ticket/77"))
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(
                """{"Ticket":{"TicketID":"77"}}""",
                Encoding.UTF8,"application/json")
        };
    optionCalls++;
    Assert(path.Contains("/DynamicField/") && path.EndsWith("/Options"),
        "optional DynamicField endpoint queried by server");
    return new HttpResponseMessage(HttpStatusCode.NotFound){
        Content=new StringContent("{}",Encoding.UTF8,"application/json")
    };
}));
var fallbackSettings=settings with{
    CostCenterOptions="100=IT;200=Service",OrderOptions="AB=Auftrag"
};
var fieldResult=await optionReader.GetBookingFieldsAsync(fallbackSettings,
    "session","77",new ZnunyReader.Counter(4),CancellationToken.None);
Assert(optionCalls==2 && fieldResult.CostCenterOptions.Count==2 &&
    fieldResult.OrderOptions.Single().Key=="AB",
    "404 on optional DynamicField route falls back to admin-controlled booking keys");

// Native iframe proxy tests use ONLY synthetic example hosts, never
// customer infrastructure. Verify the exact standard agent login markup.
var proxyType=typeof(ZnunyNativeProxy);
var findLogin=proxyType.GetMethod("FindLoginForm",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var upstream=new Uri("https://ticketsystem.example.test");
var page=new Uri("https://ticketsystem.example.test/otrs/index.pl");
const string formHtml="""
    <html><form method="post" action="/otrs/index.pl">
      <input type="hidden" name="ChallengeToken" value="synthetic-challenge">
      <input type="hidden" name="Action" value="Login">
      <input type="hidden" name="RequestedURL" value="">
      <input type="text" name="User" value="">
      <input type="password" name="Password">
    </form></html>
    """;
var parsed=findLogin.Invoke(null,new object[]{
    formHtml,page,upstream,"agent.example","test-password"
})!;
var target=(Uri)parsed.GetType().GetProperty("Action")!.GetValue(parsed)!;
var fields=(IEnumerable<KeyValuePair<string,string>>)parsed.GetType()
    .GetProperty("Fields")!.GetValue(parsed)!;
Assert(target==page && fields.Any(f=>f.Key=="ChallengeToken"&&
    f.Value=="synthetic-challenge")&&
    fields.Any(f=>f.Key=="User"&&f.Value=="agent.example")&&
    fields.Any(f=>f.Key=="Password"&&f.Value=="test-password"),
    "native agent login retains CSRF hidden fields and fills user credentials");
try{
    findLogin.Invoke(null,new object[]{
        formHtml.Replace("action=\"/otrs/index.pl\"",
            "action=\"https://other.example.test/steal\""),
        page,upstream,"agent.example","test-password"
    });
    throw new Exception("Cross-origin login action accepted");
}catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException){
    Assert(true,"native login blocks credential delivery to a different host");
}
var rewriter=proxyType.GetMethod("Rewrite",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var rewritten=(string)rewriter.Invoke(null,new object[]{
    "https://ticketsystem.example.test/otrs/index.pl "+
    "https:\\/\\/ticketsystem.example.test/otrs/index.pl",
    upstream,new Uri("https://tickets.example.test")
})!;
Assert(!rewritten.Contains("ticketsystem.example.test")&&
    rewritten.Contains("https://tickets.example.test"),
    "native proxy rewrites original absolute and escaped JS URLs");
var config=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["Security:RequireHttps"]="true",
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.test",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.test"
    }).Build();
var proxy=new ZnunyNativeProxy(config);
var origins=proxyType.GetMethod("Origins",
    BindingFlags.Instance|BindingFlags.NonPublic)!;
Assert((bool)origins.Invoke(proxy,new object?[]{null,null})!,
    "native proxy accepts two distinct same-site HTTPS origins");
var badConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["Security:RequireHttps"]="true",
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.test",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.other.test"
    }).Build();
Assert(!(bool)origins.Invoke(new ZnunyNativeProxy(badConfig),
    new object?[]{null,null})!,
    "native proxy rejects cross-site HTTPS origins");

// The native proxy must not fail closed merely because a legacy agent
// installation uses a custom/multistep HTML login. The regular browser can
// complete that login through the same already-authorized proxy session.
var nativeLogin=proxyType.GetMethod("LoginAsync",
    BindingFlags.Static|BindingFlags.NonPublic)!;
async Task<string> RunNativeLogin(Func<HttpRequestMessage,HttpResponseMessage> respond)
{
    using var http=new HttpClient(new MockHandler(respond));
    var task=(Task)nativeLogin.Invoke(null,new object[]{
        http,page,upstream,"agent.example","example-password",CancellationToken.None
    })!;
    await task;
    return task.GetType().GetProperty("Result")!.GetValue(task)!.ToString()!;
}
// Some internal gateways use nonstandard Content-Type headers for normal
// agent HTML. The source body, not just its MIME label, determines whether
// the initial agent document is usable. All hosts here are fictional.
foreach(var mime in new[]{
    "text/plain","application/octet-stream","application/xhtml+xml"
}){
    var successful=await RunNativeLogin(request=>{
        if(request.Method==HttpMethod.Post){
            var redirect=new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location=new Uri(
                "/otrs/index.pl?Action=AgentDashboard",UriKind.Relative);
            return redirect;
        }
        if(request.RequestUri?.Query.Contains("AgentDashboard")==true)
            return new HttpResponseMessage(HttpStatusCode.OK){
                Content=new StringContent(
                    "<!doctype html><html><body>Dashboard</body></html>",
                    Encoding.UTF8,"text/html")
            };
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(formHtml,Encoding.UTF8,mime)
        };
    });
    Assert(successful=="Automatic",
        "native auto-login accepts real HTML labeled "+mime);
}
var missingContentType=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post)return new HttpResponseMessage(
        HttpStatusCode.NoContent);
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new ByteArrayContent(Encoding.UTF8.GetBytes(formHtml))
    };
});
Assert(missingContentType=="Automatic",
    "native auto-login accepts HTML with missing Content-Type");

foreach(var (mime,body,expected) in new[]{
    ("application/json","{\"error\":\"not a login page\"}","page-json"),
    ("text/plain","This is not HTML","page-not-html"),
    ("text/html","","page-empty")
}){
    try{
        await RunNativeLogin(_=>new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(body,Encoding.UTF8,mime)
        });
        throw new Exception("Non-HTML document incorrectly accepted");
    }catch(InvalidOperationException ex) when(ex.Message==expected){
        Assert(true,"native login safely rejects non-HTML page: "+expected);
    }
}

var successfulPosts=0;
var autoLogin=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post){
        successfulPosts++;
        Assert(request.Headers.TryGetValues("Origin",out var origins)&&
            origins.Single()=="https://ticketsystem.example.test",
            "native login includes upstream-only Origin for CSRF validation");
        var post=new HttpResponseMessage(HttpStatusCode.Found);
        post.Headers.Location=new Uri("/otrs/index.pl?Action=AgentDashboard",UriKind.Relative);
        return post;
    }
    if(request.RequestUri?.Query.Contains("AgentDashboard")==true)
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("<html><main>Agent dashboard</main></html>",
                Encoding.UTF8,"text/html")
        };
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
    };
});
Assert(autoLogin=="Automatic"&&successfulPosts==1,
    "standard native login POST follows dashboard redirect exactly once");

var rejectedPosts=0;
var interactiveLogin=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post)rejectedPosts++;
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
    };
});
Assert(interactiveLogin=="Interactive"&&rejectedPosts==1,
    "login form returned after POST allows normal isolated browser login");

var customLogin=await RunNativeLogin(_=>
    new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent("<html><main>Custom login</main></html>",
            Encoding.UTF8,"text/html")
    });
Assert(customLogin=="Interactive",
    "unrecognized custom login markup no longer yields unhelpful HTTP 500");

try{
    await RunNativeLogin(request=>{
        if(request.Method==HttpMethod.Post){
            var blocked=new HttpResponseMessage(HttpStatusCode.Found);
            blocked.Headers.Location=new Uri("https://other.example.test/login");
            return blocked;
        }
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
        };
    });
    throw new Exception("External authentication redirect incorrectly accepted");
}catch(InvalidOperationException ex) when(ex.Message=="unsafe-redirect"){
    Assert(true,"native auto-login never follows cross-origin authentication redirects");
}

// The outer HTTPS gateway may send an internal Host value to Plenaro.
// Browser Origin is still the public URL. All hostnames here are fictitious.
var expectedPublicOrigin=new Uri("https://portal.example.test");
var proxiedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext().Request;
proxiedRequest.Scheme="https";
proxiedRequest.Host=new Microsoft.AspNetCore.Http.HostString("gateway.example.test");
proxiedRequest.Headers["Origin"]="https://portal.example.test";
proxiedRequest.Headers["Sec-Fetch-Site"]="same-origin";
Assert(ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap accepts matching browser Origin through normalized Host");
proxiedRequest.Headers.Remove("Origin");
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap does not blindly trust normalized internal Host");
proxiedRequest.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.test");
Assert(ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap preserves direct same-host clients without Origin");
proxiedRequest.Headers["Origin"]="https://other.example.test";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects a cross-origin POST even when Host matches");
proxiedRequest.Headers["Origin"]="null";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects opaque/null browser Origin");
proxiedRequest.Headers["Origin"]="https://portal.example.test";
proxiedRequest.Headers["Sec-Fetch-Site"]="cross-site";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects cross-site fetch metadata");
proxiedRequest.Headers["Sec-Fetch-Site"]="same-origin";
proxiedRequest.Scheme="http";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap requires verified HTTPS");

// Regression: a real HTML agent page can arrive as plain text or binary
// and must render as HTML for a navigation. Assets/downloads must not be
// promoted. Only synthetic markup and fictional hostnames are tested.
var sniff=proxyType.GetMethod("LooksLikeHtmlDocument",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var nav=proxyType.GetMethod("IsDocumentNavigation",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var mimeCheck=proxyType.GetMethod("PotentialHtmlMime",
    BindingFlags.Static|BindingFlags.NonPublic)!;
bool Html(string body)=>(bool)sniff.Invoke(null,new object[]{body})!;
bool Mime(string? contentType)=>(bool)mimeCheck.Invoke(
    null,new object?[]{contentType})!;
bool Navigation(string method,string? accept,string? destination,
    string path="/otrs/index.pl",string query="",string? mode=null) {
    var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
    http.Request.Method=method;
    http.Request.Path=path;
    http.Request.QueryString=new Microsoft.AspNetCore.Http.QueryString(query);
    if(accept is not null)http.Request.Headers["Accept"]=accept;
    if(destination is not null)http.Request.Headers["Sec-Fetch-Dest"]=destination;
    if(mode is not null)http.Request.Headers["Sec-Fetch-Mode"]=mode;
    return (bool)nav.Invoke(null,new object[]{
        http.Request,new Uri("https://ticketsystem.example.test"+path+query),
        new Uri("https://ticketsystem.example.test/otrs/index.pl")
    })!;
}
Assert(Html("<!DOCTYPE html><html><head><title>Agent</title></head><body></body></html>"),
    "a complete agent HTML document is recognized by its markup");
Assert(!Html("{\"error\":\"no-html\"}")&&!Html("binary")&&!Html(""),
    "non-HTML and JSON cannot trigger the HTML MIME override");
foreach(var mime in new string?[]{
    null,"text/plain","text/xml","application/xml",
    "application/octet-stream","application/x-octet-stream","binary/octet-stream"
})Assert(Mime(mime),"potential upstream CGI HTML MIME supported");
foreach(var mime in new[]{"application/javascript","application/json",
    "text/css","image/png","application/pdf","font/woff2"})
    Assert(!Mime(mime),"asset/attachment MIME remains unchanged: "+mime);
Assert(Navigation("GET","text/html,application/xhtml+xml",null,
    "/otrs/index.pl","?Action=AgentDashboard"),
    "HTML navigation with a CGI query must be recognized");
Assert(Navigation("GET",null,"iframe","/otrs/index.pl","?Action=AgentTicketZoom"),
    "iframe document navigation recognized with CGI query");
Assert(!Navigation("GET","*/*",null,"/otrs/js/ticket.js"),
    "AJAX and JavaScript resource requests are never retyped as HTML");
Assert(Navigation("POST","text/html","document",
    "/otrs/index.pl","?Action=AgentTicketCompose"),
    "reply form POST navigation can render full HTML document");
Assert(Navigation("POST","text/html",null,
    "/otrs/index.pl","?Action=AgentTicketCompose",mode:"navigate"),
    "browser navigate-mode POST accepts HTML MIME fallback");
Assert(!Navigation("POST","text/html",null,
    "/otrs/index.pl","?Action=AgentTicketCompose"),
    "AJAX POST is not misclassified as a document navigation");
Assert(!Navigation("POST","application/json",null,
    "/otrs/index.pl","?Action=AgentTicketCompose",mode:"cors"),
    "AJAX JSON POST never forces HTML MIME");

// Verify the actual opaque-content reader, not only its MIME predicates.
var opaqueReader=proxyType.GetMethod("ReadOpaqueNavigationAsync",
    BindingFlags.Static|BindingFlags.NonPublic)!;
async Task<(byte[]? Html,byte[] Streamed)> ProbeOpaque(byte[] source)
{
    using var content=new ByteArrayContent(source);
    using var streamed=new MemoryStream();
    var task=(Task<byte[]?>)opaqueReader.Invoke(null,new object[]{
        content,streamed,CancellationToken.None
    })!;
    var result=await task;
    return(result,streamed.ToArray());
}
var syntheticHtml=Encoding.UTF8.GetBytes(
    "<!DOCTYPE html><html><body><form method='post'>"+
    "<input name='User'></form></body></html>");
var htmlResult=await ProbeOpaque(syntheticHtml);
Assert(htmlResult.Html is not null &&
    htmlResult.Html.SequenceEqual(syntheticHtml) &&
    htmlResult.Streamed.Length==0,
    "opaque CGI HTML is buffered for safe URL rewriting");
var syntheticBinary=new byte[6*1024*1024+8192];
syntheticBinary[0]=0x00;
syntheticBinary[^1]=0xff;
var binaryResult=await ProbeOpaque(syntheticBinary);
Assert(binaryResult.Html is null &&
    binaryResult.Streamed.SequenceEqual(syntheticBinary),
    "large non-HTML binary downloads stream unchanged without HTML size cap");

Console.WriteLine("Znuny mock integration tests completed successfully. No real ticket modified.");

sealed class FakeFactory(Func<HttpRequestMessage,HttpResponseMessage> answer):IHttpClientFactory
{
    public HttpClient CreateClient(string name)=>new(new MockHandler(answer));
}
sealed class MockHandler(Func<HttpRequestMessage,HttpResponseMessage> answer):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken token)=>Task.FromResult(answer(request));
}
