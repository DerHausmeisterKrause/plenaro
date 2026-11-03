using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Per-user, server-side session reverse proxy for the native Znuny HTML UI.
/// A dedicated HTTPS origin (different from the Plenaro login origin) runs
/// legacy Znuny JavaScript without ever receiving Plenaro's auth cookies.
/// The remote service's original cookies remain inside CookieContainer.
/// </summary>
public sealed partial class ZnunyNativeProxy(IConfiguration configuration)
{
    private const string SessionCookie="Plenaro.ZnunyProxy";
    private const string BootstrapPath="/_plenaro/znuny-bootstrap";
    private const int MaxSessions=200;
    private static readonly TimeSpan MaxAge=TimeSpan.FromHours(2);
    private static readonly TimeSpan MaxIdle=TimeSpan.FromMinutes(20);
    private static readonly TimeSpan BootstrapTtl=TimeSpan.FromSeconds(45);
    private static readonly byte[] SignatureKey=RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string,ProxySession> _sessions=new();
    private readonly ConcurrentDictionary<Guid,string> _users=new();
    private readonly ConcurrentDictionary<string,Pending> _pending=new();
    private readonly ConcurrentDictionary<Guid,SemaphoreSlim> _startLocks=new();

    private sealed record Pending(string SessionId,string Redirect,DateTimeOffset Expires);

    private sealed class ProxySession(Guid user,Uri upstream,Uri entry,
        string signature,HttpClient client,LoginState loginState)
    {
        public Guid User {get;}=user;
        public Uri Upstream {get;}=upstream;
        public Uri Entry {get;}=entry;
        public string Signature {get;}=signature;
        public HttpClient Client {get;}=client;
        public LoginState InitialLogin {get;}=loginState;
        public DateTimeOffset Created {get;}=DateTimeOffset.UtcNow;
        private long _lastUsed=DateTime.UtcNow.Ticks;
        private long _approvedAt=0;
        public SemaphoreSlim ApprovalGate {get;}=new(1,1);
        public bool RecentlyApproved=>
            DateTime.UtcNow.Ticks-Interlocked.Read(ref _approvedAt)<
                TimeSpan.FromSeconds(15).Ticks;
        public void MarkApproved()=>Interlocked.Exchange(ref _approvedAt,
            DateTime.UtcNow.Ticks);
        public DateTimeOffset LastUsed=>new(Interlocked.Read(ref _lastUsed),TimeSpan.Zero);
        public void Touch()=>Interlocked.Exchange(ref _lastUsed,DateTime.UtcNow.Ticks);
        public bool Expired=>DateTimeOffset.UtcNow-Created>MaxAge ||
            DateTimeOffset.UtcNow-LastUsed>MaxIdle;
        public void Dispose()=>Client.Dispose();
    }

    private sealed class ProxyFailure(string reason,string code="native-error")
        :Exception(reason)
    {
        public string Reason=>Message;
        public string Code=>code;
    }

    private sealed record NativeStartResult(string Url,bool RequiresInteractiveLogin);

    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/v1/znuny/native")
            .RequireAuthorization();

        group.MapGet("/status",(ZnunyNativeProxy service)=>{
            var enabled=service.Origins(out _,out _);
            return Results.Ok(new{enabled,mode=enabled?"native":"setup",
                reason=enabled?"": "Für die direkte HTML-Ansicht sind zwei "+
                    "HTTPS-Hostnamen in der lokalen .env erforderlich."});
        });

        group.MapPost("/start",async(
            StartRequest input,HttpContext ctx,IAntiforgery anti,
            UserManager<ApplicationUser> users,SecretVault vault,
            ZnunyNativeProxy proxy,ILogger<ZnunyNativeProxy> logger,
            CancellationToken ct)=>{
            try{await anti.ValidateRequestAsync(ctx);}
            catch(AntiforgeryValidationException){return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});}
            if(!proxy.Origins(out var parent,out var external))
                return Results.Conflict(new{error="Native HTML-Ansicht ist nicht konfiguriert."});
            // A trusted front-door reverse proxy can normalize Host to its
            // internal upstream name. Browser POSTs still carry the original
            // public Origin. Verify that exact HTTPS origin instead of
            // incorrectly rejecting an otherwise authenticated + CSRF-checked
            // browser request. Never trust arbitrary X-Forwarded-Host headers.
            if(!ctx.Request.IsHttps)
                return Results.BadRequest(new{
                    error="Die Plenaro-Verbindung wird nicht als HTTPS erkannt.",
                    code="application-https-required"
                });
            if(!AllowedStartOrigin(ctx.Request,parent))
                return Results.BadRequest(new{
                    error="Die öffentliche Plenaro-Adresse stimmt nicht mit "+
                        "der konfigurierten Anwendungsadresse überein.",
                    code="application-origin-mismatch"
                });

            var user=await users.GetUserAsync(ctx.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(input.TicketId is { } ticket &&
                (ticket.Length is <1 or >40||!ticket.All(char.IsAsciiDigit)))
                return Results.BadRequest(new{error="Ungültige Ticket-ID."});
            if(!vault.IsConfigured)return Results.Json(new{
                error="Verschlüsselter Zugangsdaten-Vault ist nicht verfügbar."},
                statusCode:503);
            try{
                var result=await proxy.StartAsync(user.Id,input,vault,external,ct);
                ctx.Response.Headers.CacheControl="no-store";
                return Results.Ok(new{
                    url=result.Url,
                    requiresInteractiveLogin=result.RequiresInteractiveLogin
                });
            }catch(ProxyFailure e){
                return Results.Json(new{error=e.Reason,code=e.Code},statusCode:503);
            }catch(Exception e) when(!ct.IsCancellationRequested){
                // Never serialize the original exception: HTTP/DB exceptions
                // can contain internal hosts, queries or configuration.
                logger.LogWarning(
                    "Native ticket startup failed ({ExceptionType}); details suppressed",
                    e.GetType().Name);
                return Results.Json(new{
                    error="Interner Fehler beim Aufbau der Ticketsystem-Sitzung.",
                    code="server-error"
                },statusCode:503);
            }
        }).RequireRateLimiting("znuny-native-start");
    }

    // Distinguish the URL visible in the user's browser from the internal
    // Host header of a correctly configured reverse proxy. A browser fetch
    // POST sends Origin; only the exact configured public origin is allowed.
    // Legacy clients without Origin retain the stricter Host-only behavior.
    public static bool AllowedStartOrigin(HttpRequest request,Uri expected)
    {
        if(!request.IsHttps)return false;
        if(request.Headers.TryGetValue("Sec-Fetch-Site",out var fetchSite)&&
            fetchSite.ToString() is not ("same-origin" or "none"))
            return false;
        if(request.Headers.TryGetValue("Origin",out var values)){
            if(values.Count!=1 ||
                !Uri.TryCreate(values.ToString(),UriKind.Absolute,out var origin))
                return false;
            return origin.Scheme==Uri.UriSchemeHttps &&
                origin.GetLeftPart(UriPartial.Authority).Equals(
                    expected.GetLeftPart(UriPartial.Authority),
                    StringComparison.OrdinalIgnoreCase) &&
                origin.AbsolutePath=="/" &&
                origin.Query.Length==0 && origin.Fragment.Length==0 &&
                origin.UserInfo.Length==0;
        }
        return request.Host.Host.Equals(expected.Host,
                StringComparison.OrdinalIgnoreCase) &&
            request.Host.Port.GetValueOrDefault(443)==expected.Port;
    }

    public sealed record StartRequest(string? TicketId,bool Reset=false);

    private bool Origins(out Uri parent,out Uri external)
    {
        parent=null!;external=null!;
        if(!configuration.GetValue<bool>("Security:RequireHttps"))return false;
        if(!ValidOrigin(configuration["PLENARO_PUBLIC_ORIGIN"],out parent)||
            !ValidOrigin(configuration["PLENARO_ZNUNY_PROXY_ORIGIN"],out external))
            return false;
        if(parent.Host.Equals(external.Host,StringComparison.OrdinalIgnoreCase))
            return false;
        // The two origins must be same-site, otherwise browsers block
        // third-party iframe cookies. Prevent a completely unrelated iframe
        // origin from receiving a trusted application bootstrap.
        var a=parent.Host.Split('.');
        var b=external.Host.Split('.');
        return a.Length>=3 && b.Length>=3 &&
            a[^1].Equals(b[^1],StringComparison.OrdinalIgnoreCase) &&
            a[^2].Equals(b[^2],StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidOrigin(string? value,out Uri origin)
    {
        origin=null!;
        if(!Uri.TryCreate(value,UriKind.Absolute,out var found)||
            found.Scheme!=Uri.UriSchemeHttps || found.Port!=443 ||
            found.AbsolutePath!="/" || found.Query.Length!=0 ||
            found.Fragment.Length!=0 || found.UserInfo.Length!=0 ||
            found.HostNameType!=UriHostNameType.Dns ||
            found.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase))
            return false;
        origin=found;return true;
    }

    public bool IsProxyHost(HttpContext ctx)
    {
        // Intercept even if the parent origin is misconfigured: never serve
        // the normal Plenaro authentication UI on the isolated alias.
        return ValidOrigin(configuration["PLENARO_ZNUNY_PROXY_ORIGIN"],out var external) &&
            ctx.Request.Host.Host.Equals(external.Host,
                StringComparison.OrdinalIgnoreCase) &&
            ctx.Request.Host.Port.GetValueOrDefault(443)==443;
    }

    public async Task ServeAsync(HttpContext ctx,CancellationToken ct)
    {
        if(!Origins(out var parent,out var external)){
            ctx.Response.StatusCode=404;return;
        }
        if(!ctx.Request.IsHttps){
            ctx.Response.StatusCode=426;return;
        }
        ctx.Response.Headers["X-Content-Type-Options"]="nosniff";
        ctx.Response.Headers["Referrer-Policy"]="no-referrer";
        ctx.Response.Headers["Content-Security-Policy"]=
            BrowserContentSecurityPolicy(parent);
        // Upstream X-Frame-Options is deliberately NOT forwarded. The
        // administrator explicitly allows embedding from the configured parent.
        if(ctx.Request.Path==BootstrapPath){
            await BootstrapAsync(ctx,ct);return;
        }
        if(!ctx.Request.Cookies.TryGetValue(SessionCookie,out var key)||
            !_sessions.TryGetValue(key,out var session)||
            session.Expired||
            !_users.TryGetValue(session.User,out var ownerKey)||
            ownerKey!=key){
            ctx.Response.StatusCode=401;
            await ctx.Response.WriteAsync("Ticketsystem-Sitzung abgelaufen. Bitte über Plenaro neu öffnen.",ct);
            return;
        }
        // Recheck disabled users / revoked agent approvals while a
        // proxy tab remains open; revoking Plenaro access stops the proxy.
        if(!await StillApprovedAsync(session,ctx.RequestServices,ct)){
            RevokeUser(session.User);
            ctx.Response.StatusCode=403;return;
        }
        session.Touch();
        if(ctx.Request.Path.StartsWithSegments("/api/v1",StringComparison.OrdinalIgnoreCase)||
            ctx.Request.Path.StartsWithSegments("/health",StringComparison.OrdinalIgnoreCase)){
            ctx.Response.StatusCode=404;return;
        }
        await ProxyRequestAsync(ctx,session,external,ct);
    }

    // Znuny embeds article bodies and email previews in nested iframes.
    // CSP frame-ancestors is evaluated against EVERY ancestor: the outer
    // Plenaro page AND the native Znuny proxy page. Allow exactly those
    // two HTTPS origins; never use a wildcard or disable frame protection.
    internal static string BrowserContentSecurityPolicy(Uri parent)=>
        "default-src 'self' data: blob:; "+
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; "+
        "style-src 'self' 'unsafe-inline'; "+
        "img-src 'self' data: blob:; font-src 'self' data:; "+
        "connect-src 'self'; frame-src 'self'; worker-src 'self' blob:; "+
        "form-action 'self'; base-uri 'self'; object-src 'none'; "+
        "frame-ancestors 'self' "+parent.GetLeftPart(UriPartial.Authority);

    private async Task<bool> StillApprovedAsync(ProxySession session,
        IServiceProvider services,CancellationToken ct)
    {
        if(session.RecentlyApproved)return true;
        await session.ApprovalGate.WaitAsync(ct);
        try{
            if(session.RecentlyApproved)return true;
            var users=services.GetRequiredService<UserManager<ApplicationUser>>();
            var user=await users.FindByIdAsync(session.User.ToString());
            if(user is null||user.IsDisabled)return false;
            await using var db=new NpgsqlConnection(
                configuration.GetConnectionString("Plenaro"));
            await db.OpenAsync(ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM znuny_approved_agent_bindings
                    WHERE user_id=@user AND agent_id>0)
                """,db);
            cmd.Parameters.AddWithValue("user",session.User);
            if(await cmd.ExecuteScalarAsync(ct) is not true)return false;
            session.MarkApproved();
            return true;
        }finally{session.ApprovalGate.Release();}
    }

    private async Task BootstrapAsync(HttpContext ctx,CancellationToken ct)
    {
        if(!HttpMethods.IsGet(ctx.Request.Method)||
            !ctx.Request.Query.TryGetValue("token",out var raw)||
            raw.Count!=1||
            !_pending.TryRemove(raw.ToString(),out var pending)||
            pending.Expires<DateTimeOffset.UtcNow||
            !_sessions.TryGetValue(pending.SessionId,out var session)||
            session.Expired||
            !_users.TryGetValue(session.User,out var key)||
            key!=pending.SessionId){
            ctx.Response.StatusCode=401;return;
        }
        ctx.Response.Cookies.Append(SessionCookie,pending.SessionId,new CookieOptions{
            HttpOnly=true,Secure=true,SameSite=SameSiteMode.Strict,
            IsEssential=true,Path="/",MaxAge=MaxAge
        });
        ctx.Response.Headers.CacheControl="no-store";
        ctx.Response.Redirect(pending.Redirect);
        await Task.CompletedTask;
    }

    private static string NewToken()=>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string CredentialSignature(string username,string password,Uri url)=>
        Convert.ToHexString(HMACSHA256.HashData(SignatureKey,
            Encoding.UTF8.GetBytes(username+"\0"+password+"\0"+url.AbsoluteUri)));

    private async Task<NativeStartResult> StartAsync(Guid user,StartRequest input,
        SecretVault vault,Uri external,CancellationToken ct)
    {
        string username;
        ZnunyEndpoints.ServerSettings settings;
        // Keep DB connections brief: native upstream login can take seconds
        // and must not exhaust PostgreSQL's connection pool for other users.
        await using(var db=new NpgsqlConnection(
            configuration.GetConnectionString("Plenaro"))){
        await db.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT p.username,b.agent_id
            FROM znuny_approved_agent_bindings b
            INNER JOIN znuny_user_settings p ON p.user_id=b.user_id
            WHERE b.user_id=@user
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))throw new ProxyFailure(
            "Keine freigegebene Ticketsystem-Agentenzuordnung.");
        username=reader.GetString(0);
        var agentId=reader.GetInt32(1);
        await reader.DisposeAsync();
        if(string.IsNullOrWhiteSpace(username)||agentId<=0)throw new ProxyFailure(
            "Persönliche Ticketsystem-Anmeldung ist nicht konfiguriert.");

        if(input.TicketId is { } ticket){
            await using var permit=new NpgsqlCommand("""
                SELECT COALESCE(t.owner_agent_id=@agent OR
                    t.responsible_agent_id=@agent OR
                    (p.candidate_user_id IS NOT NULL AND
                    (t.owner_agent_id IS NULL OR
                        t.owner_agent_id IN (0,p.candidate_user_id)) AND
                    (t.responsible_agent_id IS NULL OR
                        t.responsible_agent_id IN (0,p.candidate_user_id))),false)
                FROM znuny_cached_tickets t
                CROSS JOIN znuny_user_settings p
                WHERE t.ticket_id=@ticket AND p.user_id=@user
                """,db);
            permit.Parameters.AddWithValue("user",user);
            permit.Parameters.AddWithValue("agent",agentId);
            permit.Parameters.AddWithValue("ticket",ticket);
            if(await permit.ExecuteScalarAsync(ct) is not true)
                throw new ProxyFailure("Für dieses Ticket fehlt die Freigabe.");
        }

        settings=await ZnunyEndpoints.LoadServerAsync(db,ct);
        }
        if(ZnunyEndpoints.ValidateServer(settings) is not null ||
            !Uri.TryCreate(settings.WebUrl,UriKind.Absolute,out var entry)||
            entry.Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
            throw new ProxyFailure("Ticketsystem-Webadresse ungültig.");
        var password=await vault.GetForIntegrationAsync("user",user,
            "znuny.password",ct);
        if(string.IsNullOrEmpty(password))throw new ProxyFailure(
            "Persönliches Ticketsystem-Passwort fehlt.");

        var signature=CredentialSignature(username,password,entry);
        // Independent user logins must not block each other.
        var gate=_startLocks.GetOrAdd(user,_=>new SemaphoreSlim(1,1));
        await gate.WaitAsync(ct);
        try{
            // A maximum protects memory and HTTP connections in a shared server.
            foreach(var pair in _sessions){
                if(pair.Value.Expired)RemoveSession(pair.Key,pair.Value);
            }
            foreach(var pair in _pending){
                if(pair.Value.Expires<DateTimeOffset.UtcNow)
                    _pending.TryRemove(pair.Key,out _);
            }
            ProxySession? session=null;
            string? existingKey=null;
            if(_users.TryGetValue(user,out var previous) &&
                _sessions.TryGetValue(previous,out var existing)){
                if(!input.Reset&&!existing.Expired&&existing.Signature==signature){
                    session=existing;existingKey=previous;
                }else RemoveSession(previous,existing);
            }
            if(session is null){
                if(_sessions.Count>=MaxSessions)throw new ProxyFailure(
                    "Zu viele gleichzeitige Ticketsystem-Sitzungen.");
                var origin=new Uri(entry.GetLeftPart(UriPartial.Authority));
                var client=CreateSessionClient();
                LoginState loginState;
                try{
                    loginState=await LoginAsync(
                        client,entry,origin,username,password,ct);
                }catch(NativeLoginException e){
                    client.Dispose();
                    var message=e.Code switch{
                        "page-not-html"=>
                            "Die Ticketsystem-Webadresse liefert keine HTML-Anmeldeseite. "+
                            "Bitte in den Einstellungen die Agenten-Webadresse prüfen.",
                        "unsafe-redirect" or "unsafe-form-target"=>
                            "Das Ticketsystem leitet bei der Anmeldung auf eine andere "+
                            "Webadresse um. Dies wird aus Sicherheitsgründen blockiert.",
                        "redirect-loop"=>
                            "Das Ticketsystem leitet die Anmeldung zu oft weiter.",
                        "unsupported-login-method"=>
                            "Das Ticketsystem verwendet ein nicht unterstütztes Loginformular.",
                        _=>"Die Ticketsystem-Anmeldeseite hat die Anfrage abgewiesen."
                    };
                    throw new ProxyFailure(message,e.Code);
                }catch(HttpRequestException e){
                    client.Dispose();
                    var tls=e.HttpRequestError.ToString()=="SecureConnectionError";
                    throw new ProxyFailure(tls?
                        "Die HTTPS-Zertifikatsprüfung zum Ticketsystem ist fehlgeschlagen. "+
                        "Bitte die interne Root-CA im Plenaro-Server prüfen.":
                        "Der Plenaro-Server kann die Ticketsystem-Webadresse nicht "+
                        "erreichen. Bitte DNS, Netzwerk und HTTPS-Verbindung prüfen.",
                        tls?"upstream-tls":"upstream-network");
                }catch(OperationCanceledException) when(!ct.IsCancellationRequested){
                    client.Dispose();
                    throw new ProxyFailure(
                        "Der Ticketsystem-Server antwortet beim Anmelden nicht rechtzeitig.",
                        "upstream-timeout");
                }catch(Exception e) when(e is InvalidOperationException or
                    ArgumentException or IOException){
                    client.Dispose();
                    throw new ProxyFailure(
                        "Das Loginformular oder die Antwort des Ticketsystems konnte "+
                        "nicht verarbeitet werden.","login-protocol");
                }
                session=new ProxySession(
                    user,origin,entry,signature,client,loginState);
                existingKey=NewToken();
                _sessions[existingKey]=session;
                _users[user]=existingKey;
            }
            var path=input.TicketId is { } id?
                new UriBuilder(entry){Query="Action=AgentTicketZoom;TicketID="+
                    Uri.EscapeDataString(id),Fragment=""}.Uri.PathAndQuery:
                entry.PathAndQuery;
            var token=NewToken();
            _pending[token]=new Pending(existingKey!,path,
                DateTimeOffset.UtcNow+BootstrapTtl);
            return new NativeStartResult(
                external.GetLeftPart(UriPartial.Authority)+BootstrapPath+
                "?token="+Uri.EscapeDataString(token),
                session.InitialLogin==LoginState.Interactive);
        }finally{gate.Release();}
    }

    private void RemoveSession(string key,ProxySession session)
    {
        if(((ICollection<KeyValuePair<string,ProxySession>>)_sessions)
            .Remove(new KeyValuePair<string,ProxySession>(key,session))){
            ((ICollection<KeyValuePair<Guid,string>>)_users)
                .Remove(new KeyValuePair<Guid,string>(session.User,key));
            session.Dispose();
        }
    }

    public void RevokeUser(Guid user)
    {
        if(_users.TryRemove(user,out var token)&&
            _sessions.TryRemove(token,out var session))
            session.Dispose();
    }
}
