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
    private readonly SemaphoreSlim _startLock=new(1,1);

    private sealed record Pending(string SessionId,string Redirect,DateTimeOffset Expires);

    private sealed class ProxySession(Guid user,Uri upstream,Uri entry,
        string signature,HttpClient client)
    {
        public Guid User {get;}=user;
        public Uri Upstream {get;}=upstream;
        public Uri Entry {get;}=entry;
        public string Signature {get;}=signature;
        public HttpClient Client {get;}=client;
        public DateTimeOffset Created {get;}=DateTimeOffset.UtcNow;
        private long _lastUsed=DateTime.UtcNow.Ticks;
        public DateTimeOffset LastUsed=>new(Interlocked.Read(ref _lastUsed),TimeSpan.Zero);
        public void Touch()=>Interlocked.Exchange(ref _lastUsed,DateTime.UtcNow.Ticks);
        public bool Expired=>DateTimeOffset.UtcNow-Created>MaxAge ||
            DateTimeOffset.UtcNow-LastUsed>MaxIdle;
        public void Dispose()=>Client.Dispose();
    }

    private sealed class ProxyFailure(string reason):Exception(reason)
    {
        public string Reason=>Message;
    }

    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/v1/znuny/native")
            .RequireAuthorization().RequireRateLimiting("znuny-browser-start");

        group.MapGet("/status",(ZnunyNativeProxy service)=>{
            var enabled=service.Origins(out _,out _);
            return Results.Ok(new{enabled,mode=enabled?"native":"legacy",
                reason=enabled?"": "Für die direkte HTML-Ansicht sind zwei "+
                    "HTTPS-Hostnamen in der lokalen .env erforderlich."});
        });

        group.MapPost("/start",async(
            StartRequest input,HttpContext ctx,IAntiforgery anti,
            UserManager<ApplicationUser> users,SecretVault vault,
            ZnunyNativeProxy proxy,CancellationToken ct)=>{
            try{await anti.ValidateRequestAsync(ctx);}
            catch(AntiforgeryValidationException){return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});}
            if(!proxy.Origins(out var parent,out var external))
                return Results.Conflict(new{error="Native HTML-Ansicht ist nicht konfiguriert."});
            if(!ctx.Request.IsHttps ||
                !string.Equals(ctx.Request.Host.Host,parent.Host,
                    StringComparison.OrdinalIgnoreCase) ||
                ctx.Request.Host.Port.GetValueOrDefault(443)!=parent.Port)
                return Results.BadRequest(new{error="Ungültige Anwendungsadresse."});

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
                return Results.Ok(new{url=result});
            }catch(ProxyFailure e){
                return Results.Json(new{error=e.Reason},statusCode:503);
            }
        });
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
        return Origins(out _,out var external) &&
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
            "frame-ancestors "+parent.GetLeftPart(UriPartial.Authority);
        ctx.Response.Headers["X-Frame-Options"]=""; // never forward upstream DENY/SAMEORIGIN
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
        // Each request must belong to the correct isolated user session.
        session.Touch();
        if(ctx.Request.Path.StartsWithSegments("/api/v1",StringComparison.OrdinalIgnoreCase)||
            ctx.Request.Path.StartsWithSegments("/health",StringComparison.OrdinalIgnoreCase)){
            ctx.Response.StatusCode=404;return;
        }
        await ProxyRequestAsync(ctx,session,external,ct);
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

    private async Task<string> StartAsync(Guid user,StartRequest input,
        SecretVault vault,Uri external,CancellationToken ct)
    {
        await using var db=new NpgsqlConnection(
            configuration.GetConnectionString("Plenaro"));
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
        var username=reader.GetString(0);
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

        var settings=await ZnunyEndpoints.LoadServerAsync(db,ct);
        if(ZnunyEndpoints.ValidateServer(settings) is not null ||
            !Uri.TryCreate(settings.WebUrl,UriKind.Absolute,out var entry)||
            entry.Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
            throw new ProxyFailure("Ticketsystem-Webadresse ungültig.");
        var password=await vault.GetForIntegrationAsync("user",user,
            "znuny.password",ct);
        if(string.IsNullOrEmpty(password))throw new ProxyFailure(
            "Persönliches Ticketsystem-Passwort fehlt.");

        var signature=CredentialSignature(username,password,entry);
        await _startLock.WaitAsync(ct);
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
                try{
                    await LoginAsync(client,entry,origin,username,password,ct);
                }catch(Exception e) when(e is HttpRequestException or
                    TaskCanceledException or InvalidOperationException or
                    ArgumentException){
                    client.Dispose();
                    throw new ProxyFailure("Native Anmeldung am Ticketsystem fehlgeschlagen. "+
                        "Bitte Zugangsdaten, TLS-Zertifikat und Webadresse prüfen.");
                }
                session=new ProxySession(user,origin,entry,signature,client);
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
            return external.GetLeftPart(UriPartial.Authority)+BootstrapPath+
                "?token="+Uri.EscapeDataString(token);
        }finally{_startLock.Release();}
    }

    private void RemoveSession(string key,ProxySession session)
    {
        if(_sessions.TryRemove(new KeyValuePair<string,ProxySession>(key,session))){
            _users.TryRemove(new KeyValuePair<Guid,string>(session.User,key));
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
