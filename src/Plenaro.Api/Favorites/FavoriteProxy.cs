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
public sealed class FavoriteProxy(IConfiguration configuration)
{
    private const string CookieName="Plenaro.FavoriteProxy";
    private const string BootstrapPath="/_plenaro/favorite-bootstrap";
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
        Uri parent,HttpClient client)
    {
        public Guid User {get;}=user;
        public Guid Favorite {get;}=favorite;
        public Guid App {get;}=app;
        public string Slug {get;}=slug;
        public Uri Upstream {get;}=upstream;
        public Uri Parent {get;}=parent;
        public HttpClient Client {get;}=client;
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
        !string.IsNullOrWhiteSpace(BaseDomain) &&
        Uri.CheckHostName(BaseDomain)==UriHostNameType.Dns &&
        BaseDomain.Contains('.');

    private string BaseDomain=>
        (configuration["PLENARO_FAVORITES_BASE_DOMAIN"]??"").Trim().TrimEnd('.').ToLowerInvariant();

    // The caller uses this before app cookies/auth/UI/static middleware.
    // An unknown alias still belongs to this isolated router and yields 404.
    public bool IsFavoriteHost(HttpContext ctx)=>
        FavoriteHostGuard.IsUnassignedAlias(ctx.Request.Host.Host,
            configuration["PLENARO_FAVORITES_BASE_DOMAIN"],
            configuration["PLENARO_PUBLIC_ORIGIN"],
            configuration["PLENARO_ZNUNY_PROXY_ORIGIN"]);

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

    private static bool ValidPath(string path)=>path.StartsWith('/') &&
        !path.StartsWith("//")&&!path.Contains('\\')&&!path.Contains('#') &&
        !path.Contains("://")&&!path.Any(char.IsControl);

    private static bool SameUpstream(Uri root,Uri url)=>
        url.Scheme==Uri.UriSchemeHttps &&
        url.Host.Equals(root.Host,StringComparison.OrdinalIgnoreCase) &&
        url.Port==root.Port && url.UserInfo.Length==0;

    private Uri? ProxyOrigin(string slug)
    {
        if(!BaseConfigured||!Regex.IsMatch(slug,"^[a-z][a-z0-9-]{1,39}$"))
            return null;
        return new Uri("https://"+slug+"."+BaseDomain);
    }

    public async Task<string> StartAsync(Guid user,FavoritesEndpoints.FavoriteInfo favorite,
        string upstreamUrl,SecretVault vault,CancellationToken ct)
    {
        var external=ProxyOrigin(favorite.Slug) ??
            throw new InvalidOperationException("Favorite proxy is not configured.");
        if(!ValidPath(favorite.Path)||
            !Uri.TryCreate(upstreamUrl,UriKind.Absolute,out var upstream)||
            !SameUpstream(upstream,new Uri(upstream.GetLeftPart(UriPartial.Authority)))||
            upstream.AbsolutePath!="/"||upstream.Query.Length>0)
            throw new InvalidOperationException("Invalid upstream.");

        foreach(var pair in _sessions)
            if(pair.Value.Expired)Remove(pair.Key,pair.Value);
        foreach(var pair in _pending)
            if(pair.Value.Expires<DateTimeOffset.UtcNow)
                _pending.TryRemove(pair.Key,out _);
        if(_sessions.Count>=200)throw new InvalidOperationException("Session limit reached.");
        var root=new Uri(upstream.GetLeftPart(UriPartial.Authority));
        var handler=ZnunyConnectionTester.SecureHandler();
        handler.UseCookies=true;
        handler.CookieContainer=new CookieContainer(256,64,8192);
        handler.AutomaticDecompression=DecompressionMethods.GZip|
            DecompressionMethods.Deflate|DecompressionMethods.Brotli;
        var client=new HttpClient(handler,disposeHandler:true){
            Timeout=TimeSpan.FromSeconds(35)
        };
        try{
            if(favorite.AutoLogin && vault.IsConfigured){
                var password=await vault.GetForIntegrationAsync("user",user,
                    FavoritesEndpoints.PasswordName(favorite.Id),ct);
                if(!string.IsNullOrEmpty(password) &&
                   !string.IsNullOrWhiteSpace(favorite.Username))
                    await TryLoginAsync(client,root,favorite.Username,password,ct);
            }
        }catch(Exception e) when(e is HttpRequestException or
            InvalidOperationException or TaskCanceledException){
            // A custom login form may be unsupported: the iframe still
            // permits a regular interactive login in the same private session.
        }
        var key=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var session=new ProxySession(user,favorite.Id,favorite.AppId,
            favorite.Slug,root,new Uri(configuration["PLENARO_PUBLIC_ORIGIN"]!),client);
        if(_users.TryGetValue((user,favorite.Id),out var previous) &&
            _sessions.TryGetValue(previous,out var old))
            Remove(previous,old);
        _sessions[key]=session;
        _users[(user,favorite.Id)]=key;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _pending[token]=new(key,favorite.Slug,favorite.Path,
            DateTimeOffset.UtcNow.AddSeconds(45));
        return external.GetLeftPart(UriPartial.Authority)+BootstrapPath+
            "?token="+Uri.EscapeDataString(token);
    }

    public async Task ServeAsync(HttpContext ctx,CancellationToken ct)
    {
        if(!BaseConfigured||!ctx.Request.IsHttps){
            ctx.Response.StatusCode=404;return;
        }
        var host=ctx.Request.Host.Host.ToLowerInvariant();
        var suffix="."+BaseDomain;
        if(!host.EndsWith(suffix,StringComparison.Ordinal)||
            host[..^suffix.Length] is not { } slug||
            !Regex.IsMatch(slug,"^[a-z][a-z0-9-]{1,39}$")){
            ctx.Response.StatusCode=404;return;
        }
        var external=ProxyOrigin(slug);
        if(external is null){ctx.Response.StatusCode=404;return;}
        ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
        ctx.Response.Headers["Referrer-Policy"]="no-referrer";
        ctx.Response.Headers["Content-Security-Policy"]=
            "default-src 'self' data: blob:; "+
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; "+
            "style-src 'self' 'unsafe-inline'; "+
            "img-src 'self' data: blob:; font-src 'self' data:; "+
            "connect-src 'self'; frame-src 'self'; worker-src 'self' blob:; "+
            "form-action 'self'; base-uri 'self'; object-src 'none'; "+
            "frame-ancestors "+configuration["PLENARO_PUBLIC_ORIGIN"];
        ctx.Response.Headers.CacheControl="private,no-store";
        if(ctx.Request.Path==BootstrapPath){
            await BootstrapAsync(ctx,slug,ct);return;
        }
        if(!ctx.Request.Cookies.TryGetValue(CookieName,out var key)||
            !_sessions.TryGetValue(key,out var session)||
            session.Expired||session.Slug!=slug||
            !_users.TryGetValue((session.User,session.Favorite),out var active)||
            active!=key){
            ctx.Response.StatusCode=401;return;
        }
        // Reject when the account, favorite or organizational approval
        // was revoked, even when the existing proxy cookie is still valid.
        if(!await StillApprovedAsync(ctx,session,ct)){
            Remove(key,session);
            ctx.Response.StatusCode=403;return;
        }
        if(ctx.Request.Path.StartsWithSegments("/api",StringComparison.OrdinalIgnoreCase)||
            ctx.Request.Path.StartsWithSegments("/health",StringComparison.OrdinalIgnoreCase)||
            ctx.Request.Path.StartsWithSegments("/_plenaro",StringComparison.OrdinalIgnoreCase)){
            ctx.Response.StatusCode=404;return;
        }
        session.Touch();
        await ForwardAsync(ctx,session,external,ct);
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
            SELECT EXISTS(SELECT 1 FROM user_favorites f
                JOIN favorite_apps a ON a.id=f.app_id
                WHERE f.id=@favorite AND f.user_id=@user AND
                  f.app_id=@app AND f.enabled AND a.enabled AND
                  a.slug=@slug AND a.upstream_url=@upstream AND
                  (a.available_to_all OR EXISTS(
                    SELECT 1 FROM favorite_app_users access
                    WHERE access.app_id=a.id AND access.user_id=@user)))
            """,db);
        cmd.Parameters.AddWithValue("favorite",session.Favorite);
        cmd.Parameters.AddWithValue("user",session.User);
        cmd.Parameters.AddWithValue("app",session.App);
        cmd.Parameters.AddWithValue("slug",session.Slug);
        cmd.Parameters.AddWithValue("upstream",session.Upstream.AbsoluteUri);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private async Task BootstrapAsync(HttpContext ctx,string slug,CancellationToken ct)
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
        ctx.Response.Cookies.Append(CookieName,pending.SessionId,new CookieOptions{
            HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,
            IsEssential=true,Path="/",MaxAge=MaxAge
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
