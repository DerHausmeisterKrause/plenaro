using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Server-side WebView for the native Znuny HTML application.
///
/// A real Chromium browser lives in an INTERNAL Docker container. The user's
/// browser only receives PNG frames and sends authenticated input to Plenaro.
/// The user never contacts Znuny, never receives a Znuny cookie/password, and
/// never sees the internal WebDriver session ID. The Selenium service must
/// NOT publish its ports to the Docker host.
/// </summary>
public sealed partial class ZnunyRemoteBrowser(IHttpClientFactory clients)
{
    private const int Width=1280;
    private const int Height=800;
    private const int MaxSessions=4;
    private static readonly TimeSpan MaxIdle=TimeSpan.FromMinutes(12);
    private static readonly Regex SafeSession=new(
        "^[a-zA-Z0-9_-]{10,160}$",RegexOptions.Compiled);
    private static readonly byte[] SignatureKey=RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<Guid,Browser> _sessions=new();
    private readonly SemaphoreSlim _startGate=new(1,1);
    private HttpClient Client=>clients.CreateClient("Plenaro.WebDriver");

    public sealed record StartInput(string? TicketId,bool Reset=false);
    public sealed record ViewportInput(int Width,int Height);
    public sealed record BrowserInput(string Kind,int X=0,int Y=0,int DeltaY=0,
        string? Key=null,string? Text=null,bool Ctrl=false,bool Shift=false,
        bool Alt=false);

    private sealed class Browser(string sessionId,Uri entry,string signature)
    {
        public string SessionId {get;}=sessionId;
        public Uri Entry {get;}=entry;
        public string Signature {get;}=signature;
        public DateTimeOffset LastUsed {get;set;}=DateTimeOffset.UtcNow;
        public SemaphoreSlim Gate {get;}=new(1,1);
        public bool JpegSupported {get;set;}=true;
        public int ViewWidth {get;set;}=Width;
        public int ViewHeight {get;set;}=Height;
    }

    private sealed class DriverFailure(string code):Exception(code)
    {
        public string Code=>Message;
    }

    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/v1/znuny/browser")
            .RequireAuthorization().RequireRateLimiting("znuny-browser");
        group.MapPost("/start",async(StartInput input,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,ZnunyRemoteBrowser browser,
            CancellationToken ct)=>{
            if(!await Csrf(http,anti))return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(input.TicketId is { } ticket &&
                (ticket.Length is <1 or >40||!ticket.All(char.IsAsciiDigit)))
                return Results.BadRequest(new{error="Ungültige Ticket-ID."});
            if(!vault.IsConfigured)return Results.Problem(
                "Secret-Vault ist nicht eingerichtet.",statusCode:503);
            await using var db=new NpgsqlConnection(
                config.GetConnectionString("Plenaro"));
            await db.OpenAsync(ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT p.username,b.agent_id
                FROM znuny_approved_agent_bindings b
                INNER JOIN znuny_user_settings p ON p.user_id=b.user_id
                WHERE b.user_id=@user
                """,db);
            cmd.Parameters.AddWithValue("user",user.Id);
            await using var binding=await cmd.ExecuteReaderAsync(ct);
            if(!await binding.ReadAsync(ct))return Results.Forbid();
            var username=binding.GetString(0);
            var agentId=binding.GetInt32(1);
            await binding.DisposeAsync();
            if(string.IsNullOrWhiteSpace(username)||agentId<=0)
                return Results.Conflict(new{error=
                    "Persönliche Znuny-Zugangsdaten und Agentenzuordnung fehlen."});

            // A direct ticket deep link is allowed only when Plenaro's server
            // has already approved this user for that cached ticket.
            if(input.TicketId is { } id){
                await using var permit=new NpgsqlCommand("""
                    SELECT COALESCE(
                        t.owner_agent_id=@agent OR t.responsible_agent_id=@agent
                        OR (p.candidate_user_id IS NOT NULL
                            AND (t.owner_agent_id IS NULL
                                 OR t.owner_agent_id IN (0,p.candidate_user_id))
                            AND (t.responsible_agent_id IS NULL
                                 OR t.responsible_agent_id IN (0,p.candidate_user_id))),
                        false)
                    FROM znuny_cached_tickets t
                    CROSS JOIN znuny_user_settings p
                    WHERE t.ticket_id=@ticket AND p.user_id=@user
                    """,db);
                permit.Parameters.AddWithValue("ticket",id);
                permit.Parameters.AddWithValue("agent",agentId);
                permit.Parameters.AddWithValue("user",user.Id);
                if(await permit.ExecuteScalarAsync(ct) is not true)
                    return Results.NotFound();
            }

            var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
            if(ZnunyEndpoints.ValidateServer(server) is not null ||
                !Uri.TryCreate(server.WebUrl,UriKind.Absolute,out var entry) ||
                entry.Scheme!=Uri.UriSchemeHttps ||
                entry.Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new{
                    error="Znuny-Webadresse ist nicht korrekt eingerichtet."});
            var password=await vault.GetForIntegrationAsync("user",user.Id,
                "znuny.password",ct);
            if(string.IsNullOrEmpty(password))
                return Results.Conflict(new{
                    error="Persönliches Znuny-Passwort fehlt in den Einstellungen."});
            try{
                await browser.StartAsync(user.Id,entry,username,password,
                    input.TicketId,input.Reset,ct);
                return Results.Ok(new{ready=true,width=Width,height=Height,
                    message="Interner Chromium-WebView gestartet."});
            }catch(DriverFailure e){
                return DriverError(e);
            }
        }).RequireRateLimiting("znuny-browser-start");

        group.MapGet("/frame",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,
            ZnunyRemoteBrowser browser,CancellationToken ct)=>{
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(!await ApprovedAsync(user.Id,config,ct)){
                await browser.StopAsync(user.Id,ct);
                return Results.Forbid();
            }
            try{
                var bytes=await browser.FrameAsync(user.Id,ct);
                http.Response.Headers.CacheControl="no-store,private";
                http.Response.Headers["X-Content-Type-Options"]="nosniff";
                var mime=bytes.Length>2&&bytes[0]==0xff&&bytes[1]==0xd8
                    ?"image/jpeg":"image/png";
                return Results.File(bytes,mime,enableRangeProcessing:false);
            }catch(DriverFailure e){return DriverError(e);}
        }).RequireRateLimiting("znuny-browser-frame");


        // Live CDP screencast via authenticated SSE. Browser credentials and
        // the internal Selenium websocket never reach the end user's device.
        group.MapGet("/stream",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,
            ZnunyRemoteBrowser browser,CancellationToken ct)=>{
            // Cross-site EventSource requests must never hold a browser session.
            if(http.Request.Headers.TryGetValue("Sec-Fetch-Site",out var site)&&
                site.ToString() is not ("same-origin" or "none"))
            {http.Response.StatusCode=403;return;}
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled){http.Response.StatusCode=401;return;}
            if(!await ApprovedAsync(user.Id,config,ct)){
                await browser.StopAsync(user.Id,ct);
                http.Response.StatusCode=403;return;
            }
            http.Response.ContentType="text/event-stream";
            http.Response.Headers.CacheControl="no-store, private";
            http.Response.Headers["X-Accel-Buffering"]="no";
            http.Response.Headers["X-Content-Type-Options"]="nosniff";
            try{await browser.StreamAsync(user.Id,http.Response,config,ct);}
            catch(OperationCanceledException) when(ct.IsCancellationRequested){}
            catch(Exception){ // Never surface Selenium details or internal URLs.
                if(!ct.IsCancellationRequested)
                    await http.Response.WriteAsync("event: unavailable\ndata: retry\n\n",ct);
            }
        }).RequireRateLimiting("znuny-browser-start");

        group.MapPost("/viewport",async(ViewportInput input,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,ZnunyRemoteBrowser browser,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(!await ApprovedAsync(user.Id,config,ct)){
                await browser.StopAsync(user.Id,ct);
                return Results.Forbid();
            }
            try{
                await browser.ResizeAsync(user.Id,input,ct);
                return Results.NoContent();
            }catch(DriverFailure e){return DriverError(e);}
        });

        group.MapPost("/input",async(BrowserInput input,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,ZnunyRemoteBrowser browser,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(!await ApprovedAsync(user.Id,config,ct)){
                await browser.StopAsync(user.Id,ct);
                return Results.Forbid();
            }
            try{
                await browser.InputAsync(user.Id,input,ct);
                return Results.NoContent();
            }catch(DriverFailure e){return DriverError(e);}
        });

        group.MapPost("/stop",async(HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,ZnunyRemoteBrowser browser,
            CancellationToken ct)=>{
            if(!await Csrf(http,anti))return Results.BadRequest(
                new{error="Ungültiger Sicherheitstoken."});
            var user=await users.GetUserAsync(http.User);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await browser.StopAsync(user.Id,ct);
            return Results.NoContent();
        });
    }

    private static async Task<bool> ApprovedAsync(Guid user,
        IConfiguration config,CancellationToken ct)
    {
        await using var db=new NpgsqlConnection(
            config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1 FROM znuny_approved_agent_bindings
                WHERE user_id=@user AND agent_id>0)
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static IResult DriverError(DriverFailure error)
    {
        var detail=error.Code switch{
            "unavailable"=>
                "Der interne Chromium-Container ist nicht erreichbar. "+
                "Bitte den Docker-Service 'znuny-browser' prüfen.",
            "busy"=>
                "Alle internen Browser-Sitzungen sind belegt. "+
                "Bitte später erneut öffnen.",
            "not-started"=>
                "Die Browser-Sitzung ist beendet. Bitte Ticketsystem neu laden.",
            "out-of-scope"=>
                "Die Webseite hat den freigegebenen Znuny-Server verlassen. "+
                "Die Navigation wurde gestoppt.",
            "invalid-input"=>
                "Ungültige Eingabe für die Browser-Sitzung.",
            "timeout"=>
                "Der interne Browser hat nicht rechtzeitig geantwortet.",
            _=>
                "Die interne Browser-Sitzung ist fehlgeschlagen. "+
                "Bitte Ticketsystem erneut öffnen."
        };
        return Results.Json(new{error=detail,code=error.Code},
            statusCode:error.Code is "not-started" or "invalid-input"?409:
                error.Code=="busy"?429:503);
    }

    private static async Task<bool> Csrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }

    private static string CredentialSignature(string user,string password,Uri uri)=>
        Convert.ToHexString(HMACSHA256.HashData(SignatureKey,
            Encoding.UTF8.GetBytes(user+"\0"+password+"\0"+uri.AbsoluteUri)));

    private async Task StartAsync(Guid user,Uri entry,string username,
        string password,string? ticket,bool reset,CancellationToken ct)
    {
        var signature=CredentialSignature(username,password,entry);
        await _startGate.WaitAsync(ct);
        try{
            foreach(var pair in _sessions.ToArray()){
                if(DateTimeOffset.UtcNow-pair.Value.LastUsed>MaxIdle&&
                    _sessions.TryRemove(pair.Key,out var expired)){
                    await CloseAsync(expired,ct);
                }
            }
            if(_sessions.TryGetValue(user,out var existing)){
                await existing.Gate.WaitAsync(ct);
                try{
                    if(!reset&&existing.Signature==signature){
                        if(ticket is not null)
                            await NavigateAsync(existing,TicketUrl(entry,ticket),ct);
                        else await NavigateAsync(existing,entry,ct);
                        existing.LastUsed=DateTimeOffset.UtcNow;
                        return;
                    }
                }finally{existing.Gate.Release();}
                if(_sessions.TryRemove(user,out var stale))await CloseAsync(stale,ct);
            }
            if(_sessions.Count>=MaxSessions)throw new DriverFailure("busy");
            string? sessionId=null;
            try{
                using var created=await CommandAsync(HttpMethod.Post,"session",new{
                    capabilities=new{
                        alwaysMatch=new Dictionary<string,object>{
                            ["browserName"]="chrome",
                            ["goog:chromeOptions"]=new {
                                args=new[]{
                                    "--headless=new","--disable-dev-shm-usage",
                                    "--window-size=1280,800",
                                    "--no-first-run","--no-default-browser-check",
                                    "--disable-popup-blocking"
                                }
                            }
                        }
                    }
                },ct);
                sessionId=created.RootElement.GetProperty("value")
                    .GetProperty("sessionId").GetString();
                if(sessionId is null||!SafeSession.IsMatch(sessionId))
                    throw new DriverFailure("protocol");
                var browser=new Browser(sessionId,entry,signature);
                await CommandAsync(HttpMethod.Post,"session/"+sessionId+"/window/rect",
                    new{width=Width,height=Height,x=0,y=0},ct);
                await NavigateAsync(browser,entry,ct);
                // Autofill/login happens INSIDE server-side Chromium; no
                // Znuny credential is ever returned to the user's browser.
                using var login=await CommandAsync(HttpMethod.Post,
                    "session/"+sessionId+"/execute/sync",
                    new{
                        script=LoginScript,
                        args=new[]{username,password}
                    },ct);
                var submitted=login.RootElement.GetProperty("value")
                    .ValueKind==JsonValueKind.String &&
                    login.RootElement.GetProperty("value").GetString()=="submitted";
                if(ticket is not null){
                    // The initial login form may submit asynchronously; the
                    // next screenshot shows Znuny's real result/login page.
                    await NavigateAsync(browser,TicketUrl(entry,ticket),ct);
                }
                _sessions[user]=browser;
                _=submitted;
            }catch(DriverFailure){
                if(sessionId is not null)await DeleteSessionAsync(sessionId,ct);
                throw;
            }catch(Exception e) when(e is HttpRequestException or
                TaskCanceledException or JsonException or KeyNotFoundException or
                InvalidOperationException){
                if(sessionId is not null)await DeleteSessionAsync(sessionId,ct);
                throw new DriverFailure(e is TaskCanceledException?"timeout":"unavailable");
            }
        }finally{_startGate.Release();}
    }

    private static Uri TicketUrl(Uri entry,string ticket)=>
        new UriBuilder(entry){
            Query="Action=AgentTicketZoom;TicketID="+Uri.EscapeDataString(ticket),
            Fragment=""
        }.Uri;

    private const string LoginScript="""
        const username=document.querySelector(
          'input[name="User"],input[name="UserLogin"],input#User,input#UserLogin');
        const password=document.querySelector(
          'input[name="Password"],input#Password,input[type="password"]');
        const form=password?.closest('form');
        if(!username||!password||!form)return 'no-form';
        for(const [field,value] of [[username,arguments[0]],[password,arguments[1]]]){
          const setter=Object.getOwnPropertyDescriptor(
            HTMLInputElement.prototype,'value').set;
          setter.call(field,value);
          field.dispatchEvent(new Event('input',{bubbles:true}));
          field.dispatchEvent(new Event('change',{bubbles:true}));
        }
        const submit=form.querySelector(
          'button[type="submit"],input[type="submit"],button[name="LoginButton"],#LoginButton');
        if(submit)submit.click();
        else if(form.requestSubmit)form.requestSubmit();
        else form.submit();
        return 'submitted';
        """;

    private async Task NavigateAsync(Browser browser,Uri target,CancellationToken ct)
    {
        if(!Allowed(browser.Entry,target))throw new DriverFailure("out-of-scope");
        using var doc=await CommandAsync(HttpMethod.Post,
            "session/"+browser.SessionId+"/url",new{url=target.AbsoluteUri},ct);
    }

    private static bool Allowed(Uri entry,Uri target)=>target.Scheme=="https" &&
        entry.Host.Equals(target.Host,StringComparison.OrdinalIgnoreCase) &&
        entry.Port==target.Port && target.UserInfo.Length==0;

    private Browser Current(Guid user)
    {
        if(!_sessions.TryGetValue(user,out var browser))
            throw new DriverFailure("not-started");
        if(DateTimeOffset.UtcNow-browser.LastUsed>MaxIdle)
            throw new DriverFailure("not-started");
        browser.LastUsed=DateTimeOffset.UtcNow;
        return browser;
    }

    private async Task ConfirmHostAsync(Browser browser,CancellationToken ct)
    {
        for(var attempt=0;attempt<4;attempt++){
            using var doc=await CommandAsync(HttpMethod.Get,
                "session/"+browser.SessionId+"/url",null,ct);
            var value=doc.RootElement.GetProperty("value").GetString();
            // Chromium can briefly report about:blank while a Znuny form
            // navigates. Do not reset the user's ticket during that hand-off.
            if(value=="about:blank"){
                await Task.Delay(200,ct);
                continue;
            }
            if(Uri.TryCreate(value,UriKind.Absolute,out var current) &&
                Allowed(browser.Entry,current))return;
            await NavigateAsync(browser,browser.Entry,ct);
            throw new DriverFailure("out-of-scope");
        }
        throw new DriverFailure("timeout");
    }

    private async Task<byte[]> FrameAsync(Guid user,CancellationToken ct)
    {
        var browser=Current(user);
        await browser.Gate.WaitAsync(ct);
        try{
            await ConfirmHostAsync(browser,ct);
            // A full 1280x800 lossless PNG on every ~700 ms refresh is costly,
            // especially through external gateways. Chromium can directly
            // capture a quality-limited JPEG via CDP without image libraries.
            // Fall back to standard WebDriver PNG if CDP is not supported.
            if(browser.JpegSupported){
                try{
                    using var jpeg=await CommandAsync(HttpMethod.Post,
                        "session/"+browser.SessionId+"/goog/cdp/execute",
                        new{
                            cmd="Page.captureScreenshot",
                            @params=new{format="jpeg",quality=72,
                                captureBeyondViewport=false}
                        },ct);
                    var encoded=jpeg.RootElement.GetProperty("value")
                        .GetProperty("data").GetString();
                    var data=DecodeFrame(encoded);
                    if(data.Length<3||data[0]!=0xff||data[1]!=0xd8||
                        data[2]!=0xff)
                        throw new DriverFailure("protocol");
                    return data;
                }catch(DriverFailure e) when(e.Code is "webdriver" or "protocol"){
                    browser.JpegSupported=false;
                }catch(KeyNotFoundException){
                    browser.JpegSupported=false;
                }
            }
            using var fallback=await CommandAsync(HttpMethod.Get,
                "session/"+browser.SessionId+"/screenshot",null,ct);
            var bytes=DecodeFrame(fallback.RootElement.GetProperty("value").GetString());
            if(bytes.Length<8||bytes[0]!=137||bytes[1]!=80||
                bytes[2]!=78||bytes[3]!=71)
                throw new DriverFailure("protocol");
            return bytes;
        }finally{browser.Gate.Release();}
    }

    private static byte[] DecodeFrame(string? encoded)
    {
        if(encoded is null||encoded.Length>12*1024*1024)
            throw new DriverFailure("protocol");
        try{
            var bytes=Convert.FromBase64String(encoded);
            if(bytes.Length>9*1024*1024)
                throw new DriverFailure("protocol");
            return bytes;
        }catch(FormatException){throw new DriverFailure("protocol");}
    }

    private async Task ResizeAsync(Guid user,ViewportInput size,CancellationToken ct)
    {
        if(size.Width is <480 or >2400 || size.Height is <320 or >1600)
            throw new DriverFailure("invalid-input");
        var browser=Current(user);
        await browser.Gate.WaitAsync(ct);
        try{
            if(browser.ViewWidth==size.Width && browser.ViewHeight==size.Height)
                return;
            await ConfirmHostAsync(browser,ct);
            using var window=await CommandAsync(HttpMethod.Post,
                "session/"+browser.SessionId+"/window/rect",
                new{width=size.Width,height=size.Height},ct);
            // Exact CSS viewport size, without margins from headless browser chrome.
            using var metrics=await CommandAsync(HttpMethod.Post,
                "session/"+browser.SessionId+"/goog/cdp/execute",
                new{cmd="Emulation.setDeviceMetricsOverride",
                    @params=new{width=size.Width,height=size.Height,
                        deviceScaleFactor=1,mobile=false}},ct);
            browser.ViewWidth=size.Width;
            browser.ViewHeight=size.Height;
        }finally{browser.Gate.Release();}
    }

    private async Task InputAsync(Guid user,BrowserInput input,CancellationToken ct)
    {
        var browser=Current(user);
        if(input.Kind is not ("click" or "scroll" or "key" or "text" or
            "back" or "forward" or "refresh") ||
            input.X<0||input.X>browser.ViewWidth||input.Y<0||input.Y>browser.ViewHeight||
            input.DeltaY is < -1200 or >1200 ||
            input.Text?.Length>4000)
            throw new DriverFailure("invalid-input");
        await browser.Gate.WaitAsync(ct);
        try{
            await ConfirmHostAsync(browser,ct);
            var url="session/"+browser.SessionId+"/";
            switch(input.Kind){
                case "click":
                    using(await CommandAsync(HttpMethod.Post,url+"actions",new{
                        actions=new object[]{new{
                            type="pointer",id="mouse",parameters=new{pointerType="mouse"},
                            actions=new object[]{
                                new{type="pointerMove",duration=0,
                                    origin="viewport",x=input.X,y=input.Y},
                                new{type="pointerDown",button=0},
                                new{type="pointerUp",button=0}
                            }
                        }}
                    },ct)){}
                    break;
                case "scroll":
                    using(await CommandAsync(HttpMethod.Post,url+"actions",new{
                        actions=new object[]{new{
                            type="wheel",id="wheel",
                            actions=new[]{new{type="scroll",x=input.X,y=input.Y,
                                deltaX=0,deltaY=input.DeltaY,origin="viewport"}}
                        }}
                    },ct)){}
                    break;
                case "text":
                    if(string.IsNullOrEmpty(input.Text))return;
                    // InsertText works for input/textarea/contenteditable,
                    // including Unicode and pasted multi-line content.
                    using(await CommandAsync(HttpMethod.Post,url+"execute/sync",
                        new{script=InsertTextScript,args=new[]{input.Text}},ct)){}
                    break;
                case "key":
                    var key=WebDriverKey(input.Key);
                    if(key is null)throw new DriverFailure("invalid-input");
                    var keys=new List<object>();
                    if(input.Ctrl)keys.Add(new{type="keyDown",value="\uE009"});
                    if(input.Shift)keys.Add(new{type="keyDown",value="\uE008"});
                    if(input.Alt)keys.Add(new{type="keyDown",value="\uE00A"});
                    keys.Add(new{type="keyDown",value=key});
                    keys.Add(new{type="keyUp",value=key});
                    if(input.Alt)keys.Add(new{type="keyUp",value="\uE00A"});
                    if(input.Shift)keys.Add(new{type="keyUp",value="\uE008"});
                    if(input.Ctrl)keys.Add(new{type="keyUp",value="\uE009"});
                    using(await CommandAsync(HttpMethod.Post,url+"actions",
                        new{actions=new[]{new{type="key",id="keyboard",actions=keys}}},ct)){}
                    break;
                case "back":
                case "forward":
                case "refresh":
                    using(await CommandAsync(HttpMethod.Post,url+input.Kind,new{},ct)){}
                    break;
            }
            await ConfirmHostAsync(browser,ct);
        }finally{browser.Gate.Release();}
    }

    private const string InsertTextScript="""
        const value=arguments[0], el=document.activeElement;
        if(!el)return;
        if(el.isContentEditable){
          document.execCommand('insertText',false,value);return;
        }
        if(el instanceof HTMLInputElement||el instanceof HTMLTextAreaElement){
          const start=el.selectionStart??el.value.length;
          const end=el.selectionEnd??start;
          el.setRangeText(value,start,end,'end');
          el.dispatchEvent(new Event('input',{bubbles:true}));
          el.dispatchEvent(new Event('change',{bubbles:true}));
        }
        """;

    private static string? WebDriverKey(string? key)=>key switch{
        "Enter"=>"\uE007","Tab"=>"\uE004","Backspace"=>"\uE003",
        "Delete"=>"\uE017","Escape"=>"\uE00C","ArrowUp"=>"\uE013",
        "ArrowDown"=>"\uE015","ArrowLeft"=>"\uE012",
        "ArrowRight"=>"\uE014","Home"=>"\uE011","End"=>"\uE010",
        "PageUp"=>"\uE00E","PageDown"=>"\uE00F",
        " "=>" ","Space"=>" ","F5"=>"\uE035",
        {Length:1} c=>c,
        _=>null
    };

    private async Task StopAsync(Guid user,CancellationToken ct)
    {
        if(_sessions.TryRemove(user,out var browser))
            await CloseAsync(browser,ct);
    }

    private async Task CloseAsync(Browser browser,CancellationToken ct)
    {
        await browser.Gate.WaitAsync(ct);
        try{await DeleteSessionAsync(browser.SessionId,ct);}
        finally{browser.Gate.Release();}
    }

    private async Task DeleteSessionAsync(string sessionId,CancellationToken ct)
    {
        try{
            using var doc=await CommandAsync(HttpMethod.Delete,
                "session/"+sessionId,null,ct);
        }catch(DriverFailure){}
    }

    private async Task<JsonDocument> CommandAsync(HttpMethod method,string path,
        object? payload,CancellationToken ct)
    {
        if(!path.All(c=>char.IsAsciiLetterOrDigit(c)||c is '/' or '-' or '_'))
            throw new DriverFailure("protocol");
        using var request=new HttpRequestMessage(method,path);
        if(payload is not null)
            request.Content=JsonContent.Create(payload);
        try{
            using var response=await Client.SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)
                throw new DriverFailure(response.StatusCode is HttpStatusCode.TooManyRequests or
                    HttpStatusCode.ServiceUnavailable?"busy":"webdriver");
            if(response.Content.Headers.ContentLength>13*1024*1024)
                throw new DriverFailure("protocol");
            await using var content=await response.Content.ReadAsStreamAsync(ct);
            var doc=await JsonDocument.ParseAsync(content,cancellationToken:ct);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object||
                !doc.RootElement.TryGetProperty("value",out var value)){
                doc.Dispose();throw new DriverFailure("protocol");
            }
            if(value.ValueKind==JsonValueKind.Object&&
                value.TryGetProperty("error",out var err) &&
                err.ValueKind==JsonValueKind.String){
                var code=err.GetString();
                doc.Dispose();
                throw new DriverFailure(code is "invalid session id"?
                    "not-started":"webdriver");
            }
            return doc;
        }catch(HttpRequestException){throw new DriverFailure("unavailable");}
        catch(TaskCanceledException) when(!ct.IsCancellationRequested){
            throw new DriverFailure("timeout");
        }catch(JsonException){throw new DriverFailure("protocol");}
    }
}
