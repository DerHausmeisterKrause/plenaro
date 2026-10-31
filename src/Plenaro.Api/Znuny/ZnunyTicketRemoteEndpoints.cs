using System.Net.Mail;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Interactive per-user Znuny GenericInterface. Every remote call is guarded
/// by a fresh, admin-approved agent binding and cached-ticket access check.
/// Read with the central technical account (if present); writes ALWAYS use the
/// user's own Znuny credentials, so the audit trail is not a shared service user.
/// Remote write failures are never retried automatically.
/// </summary>
public static class ZnunyTicketRemoteEndpoints
{
    private const string UserPasswordKey="znuny.password";
    public sealed record ReplyRequest(string Body);
    public sealed record AssignmentRequest(int? OwnerId,int? ResponsibleId);

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/tickets/{ticketId}/articles",async (string ticketId,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
            ZnunyReader reader,CancellationToken ct)=>{
            var context=await Context(http,users,config,vault,ticketId,false,ct);
            if(context.Error is {} failure)return failure;
            try{
                var cred=await ReadCredentials(context,vault,ct);
                if(cred is null)return MissingCredentials();
                var counter=new ZnunyReader.Counter(4);
                var session=await reader.SessionAsync(context.Server!,cred.Value.Username,
                    cred.Value.Password,counter,ct);
                var detail=await reader.GetArticlesAsync(context.Server!,session,
                    ticketId,counter,ct);
                return Results.Ok(new {ticketId=detail.TicketId,
                    ticketNumber=detail.TicketNumber,title=detail.Title,
                    articles=detail.Articles});
            }catch(ZnunyReader.RemoteFailure e){return RemoteReadError(e.Code);}
        }).RequireRateLimiting("znuny-test");

        group.MapGet("/agents",async(HttpContext http,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,ZnunyReader reader,CancellationToken ct)=>{
            var context=await Context(http,users,config,vault,null,false,ct);
            if(context.Error is {} failure)return failure;
            try{
                var cred=await ReadCredentials(context,vault,ct);
                if(cred is null)return MissingCredentials();
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,cred.Value.Username,
                    cred.Value.Password,counter,ct);
                return Results.Ok(await reader.GetAgentsAsync(context.Server!,session,counter,ct));
            }catch(ZnunyReader.RemoteFailure e){return RemoteReadError(e.Code);}
        }).RequireRateLimiting("znuny-test");

        group.MapPut("/tickets/{ticketId}/assignment",async(string ticketId,
            AssignmentRequest input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
            ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            if(input.OwnerId is not >0 && input.ResponsibleId is not >0 ||
                input.OwnerId is <=0 || input.ResponsibleId is <=0)
                return Results.BadRequest(new {error="Mindestens eine gültige Agenten-ID angeben."});
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} failure)return failure;
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            var fields=new Dictionary<string,object>();
            if(input.OwnerId is {} owner)fields["OwnerID"]=owner;
            if(input.ResponsibleId is {} responsible)fields["ResponsibleID"]=responsible;
            try{
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                var result=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Ticket"]=fields},counter,ct);
                if(!result)return UncertainWrite();
            }catch(ZnunyReader.RemoteFailure){return UncertainWrite();}
            // Remote server ACK first; only then update local cache. If the cache
            // update fails, don't resend the remote command (sync will reconcile).
            try{
                await using var db=await Open(config,ct);
                await using var cmd=new NpgsqlCommand("""
                    UPDATE znuny_cached_tickets SET
                        owner_agent_id=COALESCE(@owner,owner_agent_id),
                        responsible_agent_id=COALESCE(@responsible,responsible_agent_id),
                        changed_at=now(),synced_at=now()
                    WHERE ticket_id=@ticket
                    """,db);
                cmd.Parameters.AddWithValue("ticket",ticketId);
                cmd.Parameters.Add("owner",NpgsqlTypes.NpgsqlDbType.Integer).Value=
                    input.OwnerId is {} own?(object)own:DBNull.Value;
                cmd.Parameters.Add("responsible",NpgsqlTypes.NpgsqlDbType.Integer).Value=
                    input.ResponsibleId is {} resp?(object)resp:DBNull.Value;
                await cmd.ExecuteNonQueryAsync(ct);
                return Results.Ok(new {confirmed=true,cacheUpdated=true});
            }catch(PostgresException){
                return Results.Ok(new {confirmed=true,cacheUpdated=false});
            }
        }).RequireRateLimiting("znuny-test");

        group.MapPost("/tickets/{ticketId}/assign-to-me",async(string ticketId,
            HttpContext http,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} failure)return failure;
            if(context.AgentId is not >0)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            try{
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                var confirmed=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Ticket"]=new Dictionary<string,object>{
                        ["OwnerID"]=context.AgentId.Value,["ResponsibleID"]=context.AgentId.Value
                    }},counter,ct);
                if(!confirmed)return UncertainWrite();
            }catch(ZnunyReader.RemoteFailure){return UncertainWrite();}
            try{
                await using var db=await Open(config,ct);
                await using var cmd=new NpgsqlCommand("""
                    UPDATE znuny_cached_tickets SET owner_agent_id=@agent,
                        responsible_agent_id=@agent,changed_at=now(),synced_at=now()
                    WHERE ticket_id=@ticket
                    """,db);
                cmd.Parameters.AddWithValue("agent",context.AgentId.Value);
                cmd.Parameters.AddWithValue("ticket",ticketId);
                await cmd.ExecuteNonQueryAsync(ct);
                return Results.Ok(new{confirmed=true,cacheUpdated=true});
            }catch(PostgresException){
                return Results.Ok(new{confirmed=true,cacheUpdated=false});
            }
        }).RequireRateLimiting("znuny-test");

        group.MapPost("/tickets/{ticketId}/reply",async(string ticketId,
            ReplyRequest input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
            ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            if(string.IsNullOrWhiteSpace(input.Body)||input.Body.Length>10000)
                return Results.BadRequest(new{error="Antwort muss zwischen 1 und 10.000 Zeichen enthalten."});
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} failure)return failure;
            if(!context.Assigned)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            // Client-supplied To is NOT accepted. Determine recipient from
            // authoritative customer email articles fetched on the server.
            try{
                var counter=new ZnunyReader.Counter(4);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                var detail=await reader.GetArticlesAsync(context.Server!,session,ticketId,counter,ct);
                var source=detail.Articles.LastOrDefault(a=>
                    a.SenderType.Contains("customer",StringComparison.OrdinalIgnoreCase)&&
                    a.Channel.Contains("email",StringComparison.OrdinalIgnoreCase)&&
                    ValidEmail(a.ReplyTo,a.From) is not null);
                if(source is null)
                    return Results.Conflict(new{error="Kein eindeutiger E-Mail-Empfänger aus den Kundenartikeln ermittelbar. Bitte in Znuny antworten."});
                var to=ValidEmail(source.ReplyTo,source.From)!;
                var title=source.Subject.Length>0?source.Subject:detail.Title;
                var subject=title.TrimStart().StartsWith("Re:",StringComparison.OrdinalIgnoreCase)
                    ?title:"Re: "+title;
                // Escape plain text. No browser-provided HTML is ever sent.
                var html=WebUtility.HtmlEncode(input.Body.Trim()).Replace("\r\n","\n")
                    .Replace("\n","<br/>");
                var article=new Dictionary<string,object>{
                    ["CommunicationChannel"]="Email",["SenderType"]="agent",
                    ["IsVisibleForCustomer"]=1,["ArticleSend"]=1,
                    ["AppendSignatureToBody"]=1,["To"]=to,
                    ["Subject"]=subject,["Body"]=html,
                    ["ContentType"]="text/html; charset=utf-8",
                    ["MimeType"]="text/html",["Charset"]="utf-8"
                };
                var confirmed=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Article"]=article},counter,ct);
                if(!confirmed)return UncertainWrite();
                return Results.Ok(new {confirmed=true});
            }catch(ZnunyReader.RemoteFailure){return UncertainWrite();}
        }).RequireRateLimiting("znuny-test");
    }

    private sealed record ContextResult(IResult? Error,Guid? UserId,int? AgentId,
        bool Assigned,ZnunyEndpoints.ServerSettings? Server,
        string? PersonalUsername,string? CentralUsername);
    private static async Task<ContextResult> Context(HttpContext http,
        UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
        string? ticketId,bool requireOpen,CancellationToken ct)
    {
        var user=await users.GetUserAsync(http.User);
        ContextResult Fail(IResult result)=>new(result,null,null,false,null,null,null);
        if(user is null||user.IsDisabled)return Fail(Results.Unauthorized());
        if(!vault.IsConfigured)return Fail(Results.Problem(
            "Secret-Vault ist nicht konfiguriert.",statusCode:503));
        if(ticketId is {Length:>80} || ticketId is not null &&
            (ticketId.Length==0||!ticketId.All(char.IsAsciiDigit)))
            return Fail(Results.BadRequest(new{error="Ungültige Ticket-ID."}));
        await using var db=await Open(config,ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT b.agent_id,
                COALESCE(p.username,''),
                c.service_username,
                CASE WHEN @ticket IS NULL THEN true
                     ELSE (t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id
                         OR ((t.owner_agent_id IS NULL OR
                               t.owner_agent_id IN(0,c.candidate_default_agent_id))
                         AND (t.responsible_agent_id IS NULL OR
                               t.responsible_agent_id IN(0,c.candidate_default_agent_id)))) END,
                COALESCE(t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id,false),
                t.ticket_id
            FROM znuny_approved_agent_bindings b
            LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
            CROSS JOIN znuny_sync_control c
            LEFT JOIN znuny_cached_tickets t ON t.ticket_id=@ticket
            WHERE b.user_id=@user
            """,db);
        cmd.Parameters.AddWithValue("user",user.Id);
        cmd.Parameters.Add("ticket",NpgsqlTypes.NpgsqlDbType.Varchar).Value=
            ticketId is null?DBNull.Value:ticketId;
        await using var r=await cmd.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return Fail(Results.Forbid());
        if(ticketId is not null&&
            (r.IsDBNull(5)||!r.GetBoolean(3)))return Fail(Results.NotFound());
        var assigned=r.GetBoolean(4);
        if(requireOpen&&ticketId is not null){
            // Closed status checked separately using the authoritative cache
            // before allowing writes. This is rechecked every operation.
        }
        var agent=r.GetInt32(0);
        var personal=r.GetString(1);
        var central=r.GetString(2);
        await r.DisposeAsync();
        if(requireOpen&&ticketId is not null){
            await using var closed=new NpgsqlCommand(
                "SELECT lower(state_type)='closed' FROM znuny_cached_tickets WHERE ticket_id=@ticket",db);
            closed.Parameters.AddWithValue("ticket",ticketId);
            if(await closed.ExecuteScalarAsync(ct) is true)
                return Fail(Results.Conflict(new{error="Geschlossenes Ticket kann nicht bearbeitet werden."}));
        }
        var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
        if(ZnunyEndpoints.ValidateServer(server) is not null||
            new Uri(server.ApiUrl).Host.Equals("SERVER",StringComparison.OrdinalIgnoreCase))
            return Fail(Results.Conflict(new{error="Znuny-Server ist noch nicht eingerichtet."}));
        return new(null,user.Id,agent,assigned,server,personal,central);
    }

    private static async Task<(string Username,string Password)?> ReadCredentials(
        ContextResult context,SecretVault vault,CancellationToken ct)
    {
        if(!string.IsNullOrWhiteSpace(context.CentralUsername)){
            var password=await vault.GetForIntegrationAsync("global",null,
                ZnunyCentralSync.ServiceSecret,ct);
            if(!string.IsNullOrEmpty(password))
                return(context.CentralUsername,password);
        }
        return await WriteCredentials(context,vault,ct);
    }
    private static async Task<(string Username,string Password)?> WriteCredentials(
        ContextResult context,SecretVault vault,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(context.PersonalUsername)||
            context.UserId is null)return null;
        var password=await vault.GetForIntegrationAsync("user",context.UserId.Value,
            UserPasswordKey,ct);
        return string.IsNullOrEmpty(password)?null:(context.PersonalUsername,password);
    }
    private static string? ValidEmail(string? replyTo,string? from)
    {
        foreach(var raw in new[]{replyTo,from}){
            if(string.IsNullOrWhiteSpace(raw))continue;
            try{
                var address=new MailAddress(raw);
                if(address.Address.Length<=254 && address.Address.Contains('@'))
                    return address.Address;
            }catch(FormatException){}
        }
        return null;
    }
    private static IResult MissingCredentials()=>Results.Conflict(new{
        error="Znuny-Anmeldedaten fehlen: zentralen Serviceaccount oder persönliche Anmeldung konfigurieren."});
    private static IResult MissingUserCredentials()=>Results.Conflict(new{
        error="Zum Ändern von Znuny-Tickets bitte den persönlichen Znuny-Benutzernamen und das Passwort unter Einstellungen speichern."});
    private static IResult RemoteReadError(string code)=>Results.Problem(
        "Znuny konnte die Daten nicht liefern ("+code+").",statusCode:502);
    private static IResult UncertainWrite()=>Results.Problem(
        "Znuny hat die Änderung nicht eindeutig bestätigt. Bitte das Ticket direkt in Znuny prüfen, bevor du die Aktion wiederholst.",
        statusCode:502);
    private static IResult BadCsrf()=>Results.BadRequest(new{error="Ungültiger Sicherheitstoken."});
    private static async Task<bool> Csrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static async Task<NpgsqlConnection> Open(IConfiguration config,CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }
}
