using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;
using Plenaro.Api.Znuny;

namespace Plenaro.Api.Favorites;

/// <summary>
/// HTML application bridge: each approved app has its own browser origin,
/// and each Plenaro user/favorite gets an independent upstream cookie jar.
/// No arbitrary browser-specified destination, no original credentials/cookies.
/// </summary>
public sealed partial class FavoriteProxy(IConfiguration configuration)
{
    private const string CookieName="Plenaro.FavoriteProxy";
    private const string BootstrapPath="/_plenaro/favorite-bootstrap";
    // Token-free route that verifies the full DNS -> outer NGINX ->
    // Plenaro NGINX -> ASP.NET Host/header forwarding chain.
    private const string DiagnosticPath="/_plenaro/favorite-health";
    private const int MaxTextBytes=6*1024*1024;
    private static readonly TimeSpan MaxAge=TimeSpan.FromHours(2);
    private static readonly TimeSpan MaxIdle=TimeSpan.FromMinutes(20);
    private readonly ConcurrentDictionary<string,ProxySession> _sessions=new();
    private readonly ConcurrentDictionary<string,PendingSession> _pending=new();
    private readonly ConcurrentDictionary<(Guid,Guid),string> _users=new();

    private sealed record PendingSession(string SessionId,string Slug,
        string Target,DateTimeOffset Expires);

    private sealed class ProxySession(
        Guid user,Guid favorite,Guid app,string slug,Uri upstream,
        Uri parent,HttpClient client,bool autoLogin,string username)
    {
        public Guid User {get;}=user;
        public Guid Favorite {get;}=favorite;
        public Guid App {get;}=app;
        public string Slug {get;}=slug;
        public Uri Upstream {get;}=upstream;
        public Uri Parent {get;}=parent;
        public HttpClient Client {get;}=client;
        public bool AutoLogin {get;}=autoLogin;
        public string Username {get;}=username;
        // A harmless, random per-bootstrap attempt ID. Never the auth cookie.
        public string AutoFillNonce {get;}=
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        public DateTimeOffset Created {get;}=DateTimeOffset.UtcNow;
        private long _lastUsed=DateTimeOffset.UtcNow.Ticks;
        public bool Expired=>DateTimeOffset.UtcNow-Created>MaxAge ||
            DateTimeOffset.UtcNow-new DateTimeOffset(
                Interlocked.Read(ref _lastUsed),TimeSpan.Zero)>MaxIdle;
        public void Touch()=>Interlocked.Exchange(ref _lastUsed,DateTimeOffset.UtcNow.Ticks);
        public void Dispose()=>Client.Dispose();
    }

    public bool BaseConfigured=>
        configuration.GetValue<bool>("Security:RequireHttps") &&
        Uri.TryCreate(configuration["PLENARO_PUBLIC_ORIGIN"],UriKind.Absolute,out var parent) &&
        parent.Scheme==Uri.UriSchemeHttps &&
        (FavoritePathRouting.SharedOrigin(
            configuration["PLENARO_FAVORITES_PATH_ORIGIN"],
            configuration["PLENARO_PUBLIC_ORIGIN"],
            configuration["PLENARO_ZNUNY_PROXY_ORIGIN"]) is not null ||
         FavoriteHostNames.PreferredOrigin("wiki",
            configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
            configuration["PLENARO_FAVORITES_HOST_TEMPLATE"]) is not null);

    // The caller uses this before app cookies/auth/UI/static middleware.
    // An unknown alias still belongs to this isolated router and yields 404.
    public bool IsFavoriteHost(HttpContext ctx)=>
        FavoritePathRouting.IsSharedHost(ctx.Request.Host.Host,
            configuration["PLENARO_FAVORITES_PATH_ORIGIN"],
            configuration["PLENARO_PUBLIC_ORIGIN"],
            configuration["PLENARO_ZNUNY_PROXY_ORIGIN"]) ||
        FavoriteHostGuard.IsUnassignedAlias(ctx.Request.Host.Host,
            configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
            configuration["PLENARO_PUBLIC_ORIGIN"],
            configuration["PLENARO_ZNUNY_PROXY_ORIGIN"],
            configuration["PLENARO_FAVORITES_HOST_TEMPLATE"]);

    public static bool IsTrustedStartOrigin(HttpRequest request,IConfiguration config)
    {
        if(!request.IsHttps ||
           !Uri.TryCreate(config["PLENARO_PUBLIC_ORIGIN"],UriKind.Absolute,out var expected))
            return false;
        if(request.Headers.TryGetValue("Sec-Fetch-Site",out var site) &&
            site.ToString() is not ("same-origin" or "none"))return false;
        if(request.Headers.TryGetValue("Origin",out var values)){
            if(values.Count!=1||
               !Uri.TryCreate(values.ToString(),UriKind.Absolute,out var actual))
                return false;
            return actual.Scheme==Uri.UriSchemeHttps &&
                actual.AbsolutePath=="/" && actual.Query.Length==0 &&
                actual.Fragment.Length==0 &&
                actual.GetLeftPart(UriPartial.Authority).Equals(
                    expected.GetLeftPart(UriPartial.Authority),
                    StringComparison.OrdinalIgnoreCase);
        }
        return request.Host.Host.Equals(expected.Host,StringComparison.OrdinalIgnoreCase)
            && request.Host.Port.GetValueOrDefault(443)==expected.Port;
    }

    private static bool ValidPath(string path)=>FavoriteUrlPolicy.ValidPath(path);

    private static bool SameUpstream(Uri root,Uri url)=>
        url.Scheme==Uri.UriSchemeHttps &&
        url.Host.Equals(root.Host,StringComparison.OrdinalIgnoreCase) &&
        url.Port==root.Port && url.UserInfo.Length==0;

    private Uri? ProxyOrigin(string slug)
    {
        if(!BaseConfigured)return null;
        if(!FavoriteHostNames.ValidSlug(slug))return null;
        var shared=FavoritePathRouting.SharedOrigin(
            configuration["PLENARO_FAVORITES_PATH_ORIGIN"],
            configuration["PLENARO_PUBLIC_ORIGIN"],
            configuration["PLENARO_ZNUNY_PROXY_ORIGIN"]);
        var alias=FavoriteHostNames.PreferredOrigin(slug,
            configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
            configuration["PLENARO_FAVORITES_HOST_TEMPLATE"]);
        // The hostname template alone does NOT guarantee that wildcard DNS
        // and TLS are actually deployed for every new personal favorite.
        // Previously selecting dedicated hosts automatically broke ALL
        // personal favorites when the generated site-... hostname was NXDOMAIN.
        // Preserve the working shared path host by default. Operators must
        // explicitly opt into personal dedicated origins after provisioning
        // wildcard DNS, TLS and NGINX for that hostname pattern.
        var personal=slug.StartsWith("site-",StringComparison.Ordinal) &&
            Guid.TryParseExact(slug.AsSpan(5),"N",out _);
        var dedicated=FavoriteHostNames.PreferDedicated(
            configuration["PLENARO_FAVORITES_DEDICATED_SLUGS"],slug) ||
            (personal && alias is not null &&
             configuration.GetValue<bool>(
                "PLENARO_FAVORITES_PERSONAL_DEDICATED"));
        if(shared is not null&&!dedicated)
            return FavoritePathRouting.AppOrigin(shared,slug);
        // A selected dedicated app must not silently fall back to /<slug>/.
        // That would reintroduce invalid root REST/Webpack URLs at login.
        if(alias is null)return null;
        if(shared is not null &&
            alias.Host.Equals(shared.Host,StringComparison.OrdinalIgnoreCase))
            return null;
        // A changed template must never turn an app alias into the primary
        // application or native Znuny login origin.
        foreach(var key in new[]{"PLENARO_PUBLIC_ORIGIN","PLENARO_ZNUNY_PROXY_ORIGIN"})
            if(Uri.TryCreate(configuration[key],UriKind.Absolute,out var reserved) &&
                alias.Host.Equals(reserved.Host,StringComparison.OrdinalIgnoreCase))
                return null;
        return alias;
    }

    public async Task<string> StartAsync(Guid user,FavoritesEndpoints.FavoriteInfo favorite,
        string upstreamUrl,SecretVault vault,CancellationToken ct,
        bool requireWikiAutoLogin=false)
    {
        var external=ProxyOrigin(favorite.Slug) ??
            throw new InvalidOperationException("Favorite proxy is not configured.");
        if(!ValidPath(favorite.Path)||
            !Uri.TryCreate(upstreamUrl,UriKind.Absolute,out var upstream)||
            !SameUpstream(upstream,new Uri(upstream.GetLeftPart(UriPartial.Authority)))||
            upstream.AbsolutePath!="/"||upstream.Query.Length>0||upstream.Fragment.Length>0||
            !FavoriteUrlPolicy.AllowedHost(upstream))
            throw new InvalidOperationException("Invalid upstream.");

        Debug(user,favorite.Id,"info","session-start");
        foreach(var pair in _sessions)
            if(pair.Value.Expired)Remove(pair.Key,pair.Value);
        foreach(var pair in _pending)
            if(pair.Value.Expires<DateTimeOffset.UtcNow)
                _pending.TryRemove(pair.Key,out _);
        if(_sessions.Count>=200)throw new InvalidOperationException("Session limit reached.");
        var root=new Uri(upstream.GetLeftPart(UriPartial.Authority));
        // When a private wildcard alias is configured, the browser runs the
        // WPF-compatible DOM login once. Do NOT perform a second speculative
        // server-side HTML login attempt in the same personal session.
        var preferred=FavoriteHostNames.PreferredOrigin(favorite.Slug,
            configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
            configuration["PLENARO_FAVORITES_HOST_TEMPLATE"]);
        var domAutoLogin=FavoriteDomAutoLogin.Eligible(
            favorite.Slug,favorite.AutoLogin,IsPathHost(external.Host),
            favorite.Username,external.Host,preferred?.Host);
        Debug(user,favorite.Id,"info",
            !favorite.AutoLogin?"login-disabled":
            domAutoLogin?"login-dedicated-dom":"login-server-or-browser");
        if(favorite.AutoLogin && IsPathHost(external.Host) && !domAutoLogin)
            Debug(user,favorite.Id,"info","shared-origin-autofill-disabled");
        // Personal favorites also support internal HTTPS services without
        // an operator-managed origin list, as in the Desktop application.
        // The shared secure connector still blocks loopback, link-local,
        // cloud metadata and invalid TCP peers on every connection.
        // Never follow cross-host redirects or bypass TLS verification.
        var handler=ZnunyConnectionTester.SecureHandler();
        handler.UseCookies=true;
        handler.CookieContainer=new CookieContainer(256,64,8192);
        handler.AutomaticDecompression=DecompressionMethods.GZip|
            DecompressionMethods.Deflate|DecompressionMethods.Brotli;
        var client=new HttpClient(handler,disposeHandler:true){
            Timeout=TimeSpan.FromSeconds(35)
        };
        try{
            if(requireWikiAutoLogin){
                Debug(user,favorite.Id,"info","wiki-login-check");
                // Central wiki must establish the private upstream login
                // BEFORE exposing the iframe. No silent manual fallback.
                if(favorite.Slug!="wiki"||!favorite.AutoLogin||!vault.IsConfigured)
                    throw new InvalidOperationException("Wiki-Autologin fehlt.");
                var secret=await vault.GetForIntegrationAsync("user",user,
                    "wiki.password",ct);
                if(string.IsNullOrEmpty(secret)||
                   string.IsNullOrWhiteSpace(favorite.Username)||
                   !await TryConfluenceWikiLoginAsync(client,root,
                       favorite.Username,secret,ct))
                    throw new InvalidOperationException("Wiki-Autologin wurde nicht bestätigt.");
            }else if(favorite.AutoLogin && vault.IsConfigured && !domAutoLogin){
                var password=await vault.GetForIntegrationAsync("user",user,
                    FavoritesEndpoints.PasswordName(favorite.Id),ct);
                if(!string.IsNullOrEmpty(password) &&
                   !string.IsNullOrWhiteSpace(favorite.Username)){
                    // On the existing shared apps-plenaro /site-... host,
                    // browser-side password injection would leak credentials
                    // across same-origin favorites. Use a PRIVATE headless
                    // browser instead and import only approved upstream
                    // session cookies into THIS user's isolated proxy jar.
                    // If the optional worker is absent, retain the original
                    // conventional server-side HTML login behavior.
                    var browserAttempted=false;
                    var entry=new Uri(root,favorite.Path);
                    if(favorite.Slug.StartsWith("site-",StringComparison.Ordinal)
                       && AtossEntry(entry)){
                        // ATOSS is ZK, not an HTML form. Reuse the existing
                        // server-side cookie jar and its same-origin HTTPS
                        // route. No headless worker, new container or host.
                        Debug(user,favorite.Id,"info","atoss-zk-login-attempt");
                        var atoss=await TryAtossZkLoginAsync(
                            client,root,entry,favorite.Username,password,ct);
                        // Never fall back to another credential submission
                        // after entering the ATOSS ZK login flow.
                        browserAttempted=true;
                        Debug(user,favorite.Id,
                            atoss.Authenticated?"info":"warning",
                            atoss.Code,status:atoss.Status);
                    }
                    if(!browserAttempted &&
                        favorite.Slug.StartsWith("site-",StringComparison.Ordinal)
                        && IsPathHost(external.Host)){
                        Debug(user,favorite.Id,"info","headless-login-attempt");
                        var browser=await FavoriteHeadlessLogin.TryDetailedAsync(
                            handler.CookieContainer,root,entry,
                            favorite.Username,password,ct);
                        browserAttempted=browser.Attempted;
                        Debug(user,favorite.Id,
                            browser.Authenticated?"info":"warning",browser.Code);
                    }
                    if(!browserAttempted){
                        Debug(user,favorite.Id,"info","html-login-attempt");
                        var htmlLogin=await TryLoginAsync(client,root,
                            new Uri(root,favorite.Path),
                            favorite.Username,password,ct);
                        Debug(user,favorite.Id,
                            htmlLogin.Code is "html-login-post-sent-unconfirmed"?
                                "info":"warning",
                            htmlLogin.Code,status:htmlLogin.Status);
                    }
                }else Debug(user,favorite.Id,"warning","login-credentials-missing");
            }
        }catch(Exception e) when(!requireWikiAutoLogin&&!ct.IsCancellationRequested &&
            e is HttpRequestException or InvalidOperationException or TaskCanceledException){
            // Legacy favorites retain their existing interactive fallback.
            Debug(user,favorite.Id,"warning","login-fallback-error");
        }catch{
            Debug(user,favorite.Id,"error","login-start-failed");
            client.Dispose();
            throw;
        }
        var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session=new ProxySession(user,favorite.Id,favorite.AppId,
            favorite.Slug,root,new Uri(configuration["PLENARO_PUBLIC_ORIGIN"]!),
            client,favorite.AutoLogin,favorite.Username);
        if(_users.TryGetValue((user,favorite.Id),out var previous) &&
            _sessions.TryGetValue(previous,out var old))
            Remove(previous,old);
        _sessions[key]=session;
        _users[(user,favorite.Id)]=key;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var shared=IsPathHost(external.Host);
        var landing=shared
            ? FavoritePathRouting.AppPath(favorite.Slug,favorite.Path)
            : favorite.Path;
        _pending[token]=new(key,favorite.Slug,landing,
            DateTimeOffset.UtcNow.AddSeconds(45));
        var bootstrap=shared
            ? new Uri(external,"_plenaro/favorite-bootstrap")
            : new Uri(external,BootstrapPath);
        Debug(user,favorite.Id,"info","session-ready");
        return bootstrap.AbsoluteUri+"?token="+Uri.EscapeDataString(token);
    }

    private bool IsPathHost(string host)=>FavoritePathRouting.IsSharedHost(host,
        configuration["PLENARO_FAVORITES_PATH_ORIGIN"],
        configuration["PLENARO_PUBLIC_ORIGIN"],
        configuration["PLENARO_ZNUNY_PROXY_ORIGIN"]);

    public async Task ServeAsync(HttpContext ctx,CancellationToken ct)
    {
        if(!BaseConfigured||!ctx.Request.IsHttps){
            ctx.Response.StatusCode=404;return;
        }
        var host=ctx.Request.Host.Host.ToLowerInvariant();
        var pathMode=IsPathHost(host);
        string slug;
        string appPath;
        // The shared host has only a health probe and explicit /slug/ routes.
        // It must never show the primary Plenaro login/API on unknown paths.
        if(pathMode){
            if(ctx.Request.Path==DiagnosticPath){
                await FavoriteHealthAsync(ctx,ct);return;
            }
            if(!FavoriteHostNames.PreferDedicated(
                configuration["PLENARO_FAVORITES_DEDICATED_SLUGS"],"wiki") &&
               FavoritePathRouting.ConfluenceLoginRootNavigation(
                   ctx.Request,
                   configuration["PLENARO_FAVORITES_BROWSER_SHIM_SLUGS"])){
                // Never serve arbitrary content at the shared host root.
                // Only the proven login iframe navigation is redirected to
                // /wiki/, where the normal scoped session cookie is required.
                ctx.Response.StatusCode=StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location="/wiki/";
                ctx.Response.Headers.CacheControl="no-store";
                ctx.Response.Headers["X-Plenaro-Login-Reroute"]="wiki-root";
                return;
            }
            if(FavoritePathRouting.TryRecoverPersonalIframeNavigation(
                ctx.Request,out var recovered)){
                ctx.Response.StatusCode=StatusCodes.Status307TemporaryRedirect;
                ctx.Response.Headers.Location=recovered;
                ctx.Response.Headers.CacheControl="no-store";
                ctx.Response.Headers["X-Plenaro-Favorite-Reroute"]="personal-iframe";
                return;
            }
            if(!FavoritePathRouting.TrySplitPath(ctx.Request.Path.Value,
                out slug,out appPath)){
                ctx.Response.StatusCode=404;return;
            }
        }else{
            if(!FavoriteHostNames.TryGetSlug(host,
                configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
                configuration["PLENARO_FAVORITES_HOST_TEMPLATE"],out slug)){
                ctx.Response.StatusCode=404;return;
            }
            appPath=ctx.Request.Path.Value??"/";
        }
        // Preserve the actual legacy alias, but prefix resource links when
        // routing multiple favorites under the shared host.
        var external=new Uri("https://"+host+
            (pathMode?FavoritePathRouting.CookiePath(slug)+"/":"/"));
        if(ProxyOrigin(slug) is null){ctx.Response.StatusCode=404;return;}
        ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
        ctx.Response.Headers["Referrer-Policy"]="no-referrer";
        // Prevent legacy document.domain from collapsing dedicated hosts
        // into one browser security boundary.
        ctx.Response.Headers["Origin-Agent-Cluster"]="?1";
        ctx.Response.Headers["Content-Security-Policy"]=
            "default-src 'self' data: blob:; "+
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; "+
            "style-src 'self' 'unsafe-inline'; "+
            // Image/font CDNs may differ from the app's HTTPS origin.
            // No external script or CSS origins are permitted here.
            // The frame itself uses referrerPolicy=no-referrer.
            "img-src 'self' data: blob: https:; font-src 'self' data: https:; "+
            "connect-src 'self'; frame-src 'self'; worker-src 'self' blob:; "+
            "form-action 'self'; base-uri 'self'; object-src 'none'; "+
            // Confluence can load nested same-origin frames after login.
            // Every ancestor is checked: the proxy frame itself and Plenaro.
            // Allow only these two origins, never arbitrary parent websites.
            "frame-ancestors 'self' "+configuration["PLENARO_PUBLIC_ORIGIN"];
        ctx.Response.Headers.CacheControl="private,no-store";
        if(!pathMode&&ctx.Request.Path==DiagnosticPath){
            await FavoriteHealthAsync(ctx,ct);return;
        }
        // /wiki without its trailing slash makes relative links resolve at
        // the SHARED origin root (/assets.css) instead of /wiki/assets.css.
        // Canonicalize only safe navigations, preserving query parameters.
        if(pathMode && ctx.Request.Path.Value=="/"+slug &&
            (HttpMethods.IsGet(ctx.Request.Method)||
             HttpMethods.IsHead(ctx.Request.Method))){
            ctx.Response.StatusCode=StatusCodes.Status307TemporaryRedirect;
            ctx.Response.Headers.Location="/"+slug+"/"+ctx.Request.QueryString;
            return;
        }
        if(appPath==BootstrapPath){
            await BootstrapAsync(ctx,slug,pathMode,ct);return;
        }
        if(!ctx.Request.Cookies.TryGetValue(CookieName,out var key)||
            !_sessions.TryGetValue(key,out var session)||
            session.Expired||session.Slug!=slug||
            !_users.TryGetValue((session.User,session.Favorite),out var active)||
            active!=key){
            await ProxyNavigationErrorAsync(ctx,401,"proxy-session-expired",
                "Die Favoriten-Sitzung ist nicht mehr gültig. "+
                "Klicke in Plenaro auf „Neu verbinden“.",ct);
            return;
        }
        // Reject when the account, favorite or organizational approval
        // was revoked, even when the existing proxy cookie is still valid.
        if(!await StillApprovedAsync(ctx,session,ct)){
            Debug(session.User,session.Favorite,"warning","session-revoked");
            Remove(key,session);
            await ProxyNavigationErrorAsync(ctx,403,"proxy-session-revoked",
                "Die Favoriten-Verbindung wurde geändert oder deaktiviert. "+
                "Bitte die Webseite neu öffnen.",ct);
            return;
        }
        // The app proxy host never enters the normal Plenaro route pipeline.
        // /api and /health belong to the approved upstream app here, while
        // reserved bootstrap paths must never be forwarded.
        if(appPath.StartsWith("/_plenaro",StringComparison.OrdinalIgnoreCase)){
            ctx.Response.StatusCode=404;return;
        }
        session.Touch();
        await ForwardAsync(ctx,session,external,appPath,
            pathMode?FavoritePathRouting.CookiePath(slug):"",ct);
    }

    private static async Task FavoriteHealthAsync(HttpContext ctx,CancellationToken ct)
    {
        if(!HttpMethods.IsGet(ctx.Request.Method)&&
            !HttpMethods.IsHead(ctx.Request.Method)){
            ctx.Response.StatusCode=405;return;
        }
        ctx.Response.ContentType="text/plain; charset=utf-8";
        if(HttpMethods.IsGet(ctx.Request.Method))
            await ctx.Response.WriteAsync("plenaro-favorite-proxy-ready",ct);
    }

    private async Task<bool> StillApprovedAsync(HttpContext ctx,
        ProxySession session,CancellationToken ct)
    {
        var users=ctx.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var user=await users.FindByIdAsync(session.User.ToString());
        if(user is null||user.IsDisabled)return false;
        await using var db=new NpgsqlConnection(
            configuration.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT
                EXISTS(SELECT 1 FROM user_favorites f
                    JOIN favorite_apps a ON a.id=f.app_id
                    WHERE f.id=@favorite AND f.user_id=@user AND
                      f.app_id=@app AND f.enabled AND a.enabled AND
                      a.slug=@slug AND a.upstream_url=@upstream AND
                      a.slug<>'wiki' AND f.auto_login=@auto AND
                      f.username=@username AND
                      (a.owner_user_id=@user OR
                       (a.owner_user_id IS NULL AND
                        (a.available_to_all OR EXISTS(
                          SELECT 1 FROM favorite_app_users access
                          WHERE access.app_id=a.id AND access.user_id=@user)))))
                OR
                EXISTS(SELECT 1 FROM wiki_user_connections w
                    JOIN favorite_apps a ON a.slug='wiki'
                    WHERE w.id=@favorite AND w.user_id=@user AND
                      length(trim(w.username))>0 AND a.id=@app AND
                      a.slug=@slug AND a.upstream_url=@upstream AND
                      a.enabled AND a.available_to_all)
            """,db);
        cmd.Parameters.AddWithValue("favorite",session.Favorite);
        cmd.Parameters.AddWithValue("user",session.User);
        cmd.Parameters.AddWithValue("app",session.App);
        cmd.Parameters.AddWithValue("slug",session.Slug);
        cmd.Parameters.AddWithValue("upstream",session.Upstream.AbsoluteUri);
        cmd.Parameters.AddWithValue("auto",session.AutoLogin);
        cmd.Parameters.AddWithValue("username",session.Username);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private async Task BootstrapAsync(HttpContext ctx,string slug,
        bool pathMode,CancellationToken ct)
    {
        if(!HttpMethods.IsGet(ctx.Request.Method)||
            !ctx.Request.Query.TryGetValue("token",out var token)||
            token.Count!=1||
            !_pending.TryRemove(token.ToString(),out var pending)||
            pending.Expires<DateTimeOffset.UtcNow ||
            pending.Slug!=slug ||
            !_sessions.TryGetValue(pending.SessionId,out var session)||
            session.Expired||
            !await StillApprovedAsync(ctx,session,ct)){
            ctx.Response.StatusCode=401;return;
        }
        Debug(session.User,session.Favorite,"info","bootstrap-success");
        ctx.Response.Cookies.Append(CookieName,pending.SessionId,new CookieOptions{
            HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,
            IsEssential=true,
            Path=pathMode?FavoritePathRouting.CookiePath(slug):"/",
            MaxAge=MaxAge
        });
        ctx.Response.Redirect(pending.Target);
    }

    private void Remove(string key,ProxySession session)
    {
        if(((ICollection<KeyValuePair<string,ProxySession>>)_sessions)
            .Remove(new KeyValuePair<string,ProxySession>(key,session))){
            ((ICollection<KeyValuePair<(Guid,Guid),string>>)_users)
                .Remove(new KeyValuePair<(Guid,Guid),string>(
                    (session.User,session.Favorite),key));
            session.Dispose();
        }
    }
    public void RevokeUser(Guid user){
        foreach(var pair in _sessions)
            if(pair.Value.User==user)Remove(pair.Key,pair.Value);
    }
    public void RevokeFavorite(Guid user,Guid favorite){
        foreach(var pair in _sessions)
            if(pair.Value.User==user&&pair.Value.Favorite==favorite)
                Remove(pair.Key,pair.Value);
    }
    public void RevokeApp(Guid app){
        foreach(var pair in _sessions)
            if(pair.Value.App==app)Remove(pair.Key,pair.Value);
    }
}
