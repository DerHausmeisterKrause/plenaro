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
    public sealed record ReplyRequest(string Body,bool Close=false);
    public sealed record StateRequest(bool Close);
    public sealed record AssignmentRequest(int? OwnerId,int? ResponsibleId);
    public sealed record BookTimeInput(string CostCenter,string Order,
        string ShortDescription,string? Note);


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
        }).RequireRateLimiting("znuny-ticket-read");

        // Downloads never use a caller-supplied URL or filename. The ticket,
        // article and file IDs identify the attachment inside a freshly
        // authenticated Znuny TicketGet; authorization is rechecked per read.
        group.MapGet("/tickets/{ticketId}/articles/{articleId}/attachments/{fileId}",
            async(string ticketId,string articleId,string fileId,HttpContext http,
                UserManager<ApplicationUser> users,IConfiguration config,
                SecretVault vault,ZnunyReader reader,CancellationToken ct)=>
                await AttachmentResult(ticketId,articleId,fileId,false,http,
                    users,config,vault,reader,ct))
            .RequireRateLimiting("znuny-ticket-read");

        group.MapGet("/tickets/{ticketId}/articles/{articleId}/attachments/{fileId}/inline",
            async(string ticketId,string articleId,string fileId,HttpContext http,
                UserManager<ApplicationUser> users,IConfiguration config,
                SecretVault vault,ZnunyReader reader,CancellationToken ct)=>
                await AttachmentResult(ticketId,articleId,fileId,true,http,
                    users,config,vault,reader,ct))
            .RequireRateLimiting("znuny-ticket-read");

        group.MapGet("/tickets/{ticketId}/booking-fields",async(string ticketId,
            HttpContext http,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,ZnunyReader reader,
            CancellationToken ct)=>{
            var context=await Context(http,users,config,vault,ticketId,false,ct);
            if(context.Error is {} denied)return denied;
            try{
                var credentials=await ReadCredentials(context,vault,ct);
                if(credentials is null)return MissingCredentials();
                var calls=new ZnunyReader.Counter(6);
                var session=await reader.SessionAsync(context.Server!,
                    credentials.Value.Username,credentials.Value.Password,calls,ct);
                return Results.Ok(await reader.GetBookingFieldsAsync(context.Server!,
                    session,ticketId,calls,ct));
            }catch(ZnunyReader.RemoteFailure e){return RemoteReadError(e.Code);}
        }).RequireRateLimiting("znuny-ticket-read");

        // Read-only personal write readiness check: verifies the same personal
        // credentials used by reply/booking, never the central sync account.
        // No TicketUpdate and no outgoing customer message are issued here.
        group.MapGet("/tickets/{ticketId}/write-check",async(string ticketId,
            HttpContext http,UserManager<ApplicationUser> users,IConfiguration config,
            SecretVault vault,ZnunyReader reader,CancellationToken ct)=>{
            var context=await Context(http,users,config,vault,ticketId,false,ct);
            if(context.Error is {} denied)return denied;
            if(!context.Assigned)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            string session;
            var counter=new ZnunyReader.Counter(4);
            try{
                session=await reader.SessionAsync(context.Server!,
                    credentials.Value.Username,credentials.Value.Password,counter,ct);
            }catch(ZnunyReader.RemoteFailure e){
                return RemoteReadError(e,"persönliche Znuny-API-Anmeldung");
            }
            try{
                await reader.GetArticlesAsync(context.Server!,session,ticketId,counter,ct);
            }catch(ZnunyReader.RemoteFailure e){
                return RemoteReadError(e,"TicketGet mit persönlicher Anmeldung");
            }
            return Results.Ok(new{personalLogin=true,ticketGet=true,
                writeRoute=context.Server!.TicketUpdateRoute,
                writeMethod=context.Server.TicketUpdateMethod,
                writeVerified=false,
                message="Persönliche Anmeldung und TicketGet funktionieren. "+
                "Die tatsächliche TicketUpdate-Schreibberechtigung ist ohne "+
                "eine gezielte Testbuchung oder Testantwort nicht verifizierbar."});
        });

        // The booking journal is committed BEFORE sending anything to Znuny.
        // A pending entry remains pending on network error or uncertain ACK.
        // Never retry automatically, including after an app restart.
        group.MapPost("/tickets/{ticketId}/time/book",async(string ticketId,
            BookTimeInput input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,
            SecretVault vault,ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            if(input.CostCenter is null || input.Order is null ||
                input.ShortDescription is null || input.ShortDescription.Length>500 ||
                input.Note?.Length>1000 || input.CostCenter.Length>200 ||
                input.Order.Length>200 ||
                string.IsNullOrWhiteSpace(input.ShortDescription))
                return Results.BadRequest(new{error="Buchungsfelder ungültig."});
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} denied)return denied;
            if(!context.Assigned)return Results.Forbid();
            var creds=await WriteCredentials(context,vault,ct);
            if(creds is null)return MissingUserCredentials();
            string session;
            ZnunyReader.BookingFields fields;
            var stage="persönliche Znuny-API-Anmeldung";
            try{
                var calls=new ZnunyReader.Counter(6);
                session=await reader.SessionAsync(context.Server!,
                    creds.Value.Username,creds.Value.Password,calls,ct);
                stage="TicketGet und DynamicField-Optionen";
                fields=await reader.GetBookingFieldsAsync(context.Server!,
                    session,ticketId,calls,ct);
            }catch(ZnunyReader.RemoteFailure e){return RemoteReadError(e,stage);}
            if(!BookingValueAllowed(input.CostCenter,fields.CostCenter,fields.CostCenterOptions) ||
               !BookingValueAllowed(input.Order,fields.Order,fields.OrderOptions))
                return Results.BadRequest(new{error="Bitte eine gültige Znuny-Kostenstelle bzw. einen gültigen Auftrag auswählen."});

            var id=Guid.NewGuid();
            long seconds;
            int minutes;
            await using(var db=await Open(config,ct)){
                await using var tx=await db.BeginTransactionAsync(ct);
                await using(var guard=new NpgsqlCommand(
                    "SELECT pg_advisory_xact_lock(hashtextextended(@user::text || ':' || @ticket, 2))",db,tx)){
                    guard.Parameters.AddWithValue("user",context.UserId!.Value);
                    guard.Parameters.AddWithValue("ticket",ticketId);
                    await guard.ExecuteNonQueryAsync(ct);
                }
                await using(var pending=new NpgsqlCommand("""
                    SELECT EXISTS(SELECT 1 FROM znuny_ticket_time_bookings
                    WHERE user_id=@user AND ticket_id=@ticket AND status='pending')
                    """,db,tx)){
                    pending.Parameters.AddWithValue("user",context.UserId!.Value);
                    pending.Parameters.AddWithValue("ticket",ticketId);
                    if(await pending.ExecuteScalarAsync(ct) is true)
                        return Results.Conflict(new{error="Eine vorige Znuny-Buchung ist noch ungeklärt. Erst Buchungsstatus prüfen, nicht erneut senden."});
                }
                var time=await ZnunyTicketDetailEndpoints.ReadTime(db,
                    context.UserId!.Value,ticketId,ct);
                if(time.RunningSince is not null)
                    return Results.Conflict(new{error="Timer vor der Übertragung bitte stoppen."});
                seconds=time.UnbookedSeconds;
                if(seconds<=0 || seconds>864000)
                    return Results.Conflict(new{error="Keine gültige ungebuchte Zeit vorhanden."});
                minutes=checked((int)(((seconds+899)/900)*15));
                await using var insert=new NpgsqlCommand("""
                    INSERT INTO znuny_ticket_time_bookings
                      (id,user_id,ticket_id,source_seconds,booked_minutes,
                       cost_center,order_value,short_description,note,status)
                    VALUES(@id,@user,@ticket,@seconds,@minutes,
                           @cost,@order,@description,@note,'pending')
                    """,db,tx);
                insert.Parameters.AddWithValue("id",id);
                insert.Parameters.AddWithValue("user",context.UserId.Value);
                insert.Parameters.AddWithValue("ticket",ticketId);
                insert.Parameters.AddWithValue("seconds",seconds);
                insert.Parameters.AddWithValue("minutes",minutes);
                insert.Parameters.AddWithValue("cost",input.CostCenter);
                insert.Parameters.AddWithValue("order",input.Order);
                insert.Parameters.AddWithValue("description",input.ShortDescription.Trim());
                insert.Parameters.AddWithValue("note",input.Note?.Trim()??"");
                await insert.ExecuteNonQueryAsync(ct);
                await tx.CommitAsync(ct);
            }

            var marker="TaskTool-Booking-ID: "+id.ToString("D");
            var body=input.ShortDescription.Trim()+
                (string.IsNullOrWhiteSpace(input.Note)?"":"\n\nNotiz:\n"+input.Note.Trim())+
                "\n\n"+marker;
            var article=new Dictionary<string,object>{
                ["Subject"]="TaskTool Zeitbuchung",["Body"]=body,
                ["ContentType"]="text/plain; charset=utf-8",["MimeType"]="text/plain",
                ["Charset"]="utf-8",["SenderType"]="agent",
                ["CommunicationChannel"]="Internal",["IsVisibleForCustomer"]=0,
                ["TimeUnit"]=minutes
            };
            var dynamicFields=new List<Dictionary<string,string>>();
            if(!string.IsNullOrWhiteSpace(context.Server!.CostCenterFieldName) &&
                !string.IsNullOrWhiteSpace(input.CostCenter))
                dynamicFields.Add(new(){
                    ["Name"]=context.Server.CostCenterFieldName,["Value"]=input.CostCenter
                });
            if(!string.IsNullOrWhiteSpace(context.Server.OrderFieldName) &&
                !string.IsNullOrWhiteSpace(input.Order))
                dynamicFields.Add(new(){
                    ["Name"]=context.Server.OrderFieldName,["Value"]=input.Order
                });
            var payload=new Dictionary<string,object>{["Article"]=article};
            if(dynamicFields.Count>0)payload["DynamicField"]=dynamicFields;
            try{
                var calls=new ZnunyReader.Counter(2);
                var ack=await reader.UpdateAsync(context.Server,session,ticketId,payload,calls,ct);
                if(!ack)return UncertainBooking(id);
                await using var db=await Open(config,ct);
                await using var cmd=new NpgsqlCommand("""
                    UPDATE znuny_ticket_time_bookings
                    SET status='confirmed',confirmed_at=now()
                    WHERE id=@id AND user_id=@user AND ticket_id=@ticket AND status='pending'
                    """,db);
                cmd.Parameters.AddWithValue("id",id);
                cmd.Parameters.AddWithValue("user",context.UserId.Value);
                cmd.Parameters.AddWithValue("ticket",ticketId);
                if(await cmd.ExecuteNonQueryAsync(ct)!=1)return UncertainBooking(id);
                return Results.Ok(new{
                    confirmed=true,id,sourceSeconds=seconds,bookedMinutes=minutes
                });
            }catch(ZnunyReader.RemoteFailure e) when(e.DefinitivelyRejected){
                // Znuny returned an explicit rejection (4xx or ErrorCode).
                // It did not accept a booking: retain the audit record but
                // unblock a corrected submission. Never do this on timeout/5xx.
                try{
                    await using var db=await Open(config,ct);
                    await using var rejected=new NpgsqlCommand("""
                        UPDATE znuny_ticket_time_bookings
                        SET status='rejected',rejection_code=@code
                        WHERE id=@id AND status='pending'
                        """,db);
                    rejected.Parameters.AddWithValue("id",id);
                    rejected.Parameters.AddWithValue("code",
                        (object?)e.RemoteCode??e.Code);
                    await rejected.ExecuteNonQueryAsync(ct);
                    return RemoteWriteRejected(e);
                }catch(NpgsqlException){return UncertainBooking(id);}
            }catch(Exception e) when(e is ZnunyReader.RemoteFailure or NpgsqlException){
                // A network failure/5xx/unknown ACK may mean the remote write
                // already happened; keep pending to prevent double booking.
                return UncertainBooking(id);
            }
        }).RequireRateLimiting("znuny-ticket-write");

        group.MapPost("/tickets/{ticketId}/time/bookings/{bookingId:guid}/reconcile",
            async(string ticketId,Guid bookingId,HttpContext http,
                IAntiforgery anti,UserManager<ApplicationUser> users,
                IConfiguration config,SecretVault vault,ZnunyReader reader,
                CancellationToken ct)=>{
                if(!await Csrf(http,anti))return BadCsrf();
                var context=await Context(http,users,config,vault,ticketId,false,ct);
                if(context.Error is {} denied)return denied;
                await using var db=await Open(config,ct);
                await using var pending=new NpgsqlCommand("""
                    SELECT status FROM znuny_ticket_time_bookings
                    WHERE id=@id AND user_id=@user AND ticket_id=@ticket
                    """,db);
                pending.Parameters.AddWithValue("id",bookingId);
                pending.Parameters.AddWithValue("user",context.UserId!.Value);
                pending.Parameters.AddWithValue("ticket",ticketId);
                var status=await pending.ExecuteScalarAsync(ct) as string;
                if(status is null)return Results.NotFound();
                if(status=="confirmed")return Results.Ok(new{confirmed=true});
                if(status=="rejected")return Results.Ok(new{
                    confirmed=false,pending=false,
                    message="Znuny hat diese Buchung ausdrücklich abgelehnt. Die Zeit wurde nicht übertragen. Bitte die Eingaben korrigieren."
                });
                var cred=await ReadCredentials(context,vault,ct);
                if(cred is null)return MissingCredentials();
                try{
                    var calls=new ZnunyReader.Counter(4);
                    var session=await reader.SessionAsync(context.Server!,
                        cred.Value.Username,cred.Value.Password,calls,ct);
                    var detail=await reader.GetArticlesAsync(context.Server!,session,
                        ticketId,calls,ct);
                    var marker="TaskTool-Booking-ID: "+bookingId.ToString("D");
                    var found=detail.Articles.FirstOrDefault(a=>a.Body.Contains(marker,
                        StringComparison.Ordinal));
                    if(found is null)
                        return Results.Ok(new{confirmed=false,pending=true,
                            message="Buchungs-ID im geladenen Znuny-Verlauf nicht gefunden. Nicht erneut buchen; Artikel-Limit und Znuny prüfen."});
                    await using var cmd=new NpgsqlCommand("""
                        UPDATE znuny_ticket_time_bookings
                        SET status='confirmed',article_id=@article,confirmed_at=now()
                        WHERE id=@id AND user_id=@user AND ticket_id=@ticket AND status='pending'
                        """,db);
                    cmd.Parameters.AddWithValue("id",bookingId);
                    cmd.Parameters.AddWithValue("user",context.UserId.Value);
                    cmd.Parameters.AddWithValue("ticket",ticketId);
                    cmd.Parameters.AddWithValue("article",found.Id);
                    await cmd.ExecuteNonQueryAsync(ct);
                    return Results.Ok(new{confirmed=true});
                }catch(ZnunyReader.RemoteFailure e){return RemoteReadError(e.Code);}
            }).RequireRateLimiting("znuny-ticket-read");

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
        }).RequireRateLimiting("znuny-ticket-read");

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
            var writeAttempted=false;
            try{
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                writeAttempted=true;
                var result=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Ticket"]=fields},counter,ct);
                if(!result)return UncertainWrite();
            }catch(ZnunyReader.RemoteFailure e) when(!writeAttempted){
                // Login/TicketGet failed before a write was attempted. Retrying
                // this particular action cannot duplicate a remote write.
                return RemoteReadError(e,"persönliche Znuny-API-Anmeldung");
            }catch(ZnunyReader.RemoteFailure e) when(e.DefinitivelyRejected){
                return RemoteWriteRejected(e);
            }catch(ZnunyReader.RemoteFailure e){return UncertainWrite(e);}
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
        }).RequireRateLimiting("znuny-ticket-write");

        group.MapPost("/tickets/{ticketId}/assign-to-me",async(string ticketId,
            HttpContext http,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} failure)return failure;
            if(context.AgentId is not >0)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            var writeAttempted=false;
            try{
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                writeAttempted=true;
                var confirmed=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Ticket"]=new Dictionary<string,object>{
                        ["OwnerID"]=context.AgentId.Value,["ResponsibleID"]=context.AgentId.Value
                    }},counter,ct);
                if(!confirmed)return UncertainWrite();
            }catch(ZnunyReader.RemoteFailure e) when(!writeAttempted){
                // Login/TicketGet failed before a write was attempted. Retrying
                // this particular action cannot duplicate a remote write.
                return RemoteReadError(e,"persönliche Znuny-API-Anmeldung");
            }catch(ZnunyReader.RemoteFailure e) when(e.DefinitivelyRejected){
                return RemoteWriteRejected(e);
            }catch(ZnunyReader.RemoteFailure e){return UncertainWrite(e);}
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
        }).RequireRateLimiting("znuny-ticket-write");

        group.MapPut("/tickets/{ticketId}/state",async(string ticketId,
            StateRequest input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
            ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            var context=await Context(http,users,config,vault,ticketId,false,ct);
            if(context.Error is {} failure)return failure;
            if(!context.Assigned)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            await using var db=await Open(config,ct);
            await using var stateCmd=new NpgsqlCommand(
                "SELECT lower(state_type)='closed' FROM znuny_cached_tickets WHERE ticket_id=@ticket",db);
            stateCmd.Parameters.AddWithValue("ticket",ticketId);
            var wasClosed=await stateCmd.ExecuteScalarAsync(ct) is true;
            if(input.Close==wasClosed)
                return Results.Conflict(new{error=input.Close?
                    "Ticket ist bereits geschlossen.":"Ticket ist bereits offen."});
            var nextState=input.Close?"closed successful":"open";
            var writeAttempted=false;
            try{
                var counter=new ZnunyReader.Counter(3);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                writeAttempted=true;
                var confirmed=await reader.UpdateAsync(context.Server!,session,ticketId,
                    new Dictionary<string,object>{["Ticket"]=new Dictionary<string,object>{
                        ["State"]=nextState
                    }},counter,ct);
                if(!confirmed)return UncertainWrite();
            }catch(ZnunyReader.RemoteFailure e) when(!writeAttempted){
                // Login/TicketGet failed before a write was attempted. Retrying
                // this particular action cannot duplicate a remote write.
                return RemoteReadError(e,"persönliche Znuny-API-Anmeldung");
            }catch(ZnunyReader.RemoteFailure e) when(e.DefinitivelyRejected){
                return RemoteWriteRejected(e);
            }catch(ZnunyReader.RemoteFailure e){return UncertainWrite(e);}
            try{
                await using var update=new NpgsqlCommand("""
                    UPDATE znuny_cached_tickets SET state=@state,state_type=@type,
                        changed_at=now(),synced_at=now() WHERE ticket_id=@ticket
                    """,db);
                update.Parameters.AddWithValue("state",nextState);
                update.Parameters.AddWithValue("type",input.Close?"closed":"open");
                update.Parameters.AddWithValue("ticket",ticketId);
                await update.ExecuteNonQueryAsync(ct);
                return Results.Ok(new{confirmed=true,cacheUpdated=true});
            }catch(PostgresException){
                return Results.Ok(new{confirmed=true,cacheUpdated=false});
            }
        }).RequireRateLimiting("znuny-ticket-write");

        group.MapPost("/tickets/{ticketId}/reply",async(string ticketId,
            ReplyRequest input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,SecretVault vault,
            ZnunyReader reader,CancellationToken ct)=>{
            if(!await Csrf(http,anti))return BadCsrf();
            if(input.Body is null||string.IsNullOrWhiteSpace(input.Body)||input.Body.Length>10000)
                return Results.BadRequest(new{error="Antwort muss zwischen 1 und 10.000 Zeichen enthalten."});
            var context=await Context(http,users,config,vault,ticketId,true,ct);
            if(context.Error is {} failure)return failure;
            if(!context.Assigned)return Results.Forbid();
            var credentials=await WriteCredentials(context,vault,ct);
            if(credentials is null)return MissingUserCredentials();
            // Client-supplied To is NOT accepted. Determine recipient from
            // authoritative customer email articles fetched on the server.
            var writeAttempted=false;
            var stage="persönliche Znuny-API-Anmeldung";
            try{
                var counter=new ZnunyReader.Counter(4);
                var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                    credentials.Value.Password,counter,ct);
                stage="TicketGet mit persönlicher Anmeldung";
                var detail=await reader.GetArticlesAsync(context.Server!,session,ticketId,counter,ct);
                // WPF Classic: prefer the latest email from a customer,
                // accept other customer-channel articles when their From is
                // a valid address, then fall back to Ticket.CustomerUserID.
                // Never infer a recipient from an agent/system article or a
                // browser-supplied To field.
                var customerArticles=detail.Articles.Where(a=>
                    a.SenderType.Contains("customer",StringComparison.OrdinalIgnoreCase) ||
                    a.Channel.Contains("email-external",StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var source=customerArticles.LastOrDefault(a=>
                    a.Channel.Contains("email",StringComparison.OrdinalIgnoreCase) &&
                    ValidEmail(a.ReplyTo,a.From) is not null) ??
                    customerArticles.LastOrDefault(a=>
                    ValidEmail(a.ReplyTo,a.From) is not null);
                var to=source is null
                    ?ValidEmail(detail.CustomerUserId,null)
                    :ValidEmail(source.ReplyTo,source.From);
                if(to is null)
                    return Results.Conflict(new{error="Znuny liefert weder eine gültige Kunden-E-Mail noch eine CustomerUserID mit E-Mail-Adresse. Bitte den Empfänger direkt in Znuny prüfen."});
                var title=source is not null && source.Subject.Length>0
                    ?source.Subject:detail.Title;
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
                var contents=new Dictionary<string,object>{["Article"]=article};
                if(input.Close)
                    contents["Ticket"]=new Dictionary<string,object>{
                        ["State"]="closed successful"
                    };
                writeAttempted=true;
                var confirmed=await reader.UpdateAsync(context.Server!,session,ticketId,
                    contents,counter,ct);
                if(!confirmed)return UncertainWrite();
                if(!input.Close)return Results.Ok(new{confirmed=true});
                // Remote ACK precedes cache update. No automatic repeat if
                // local cache write fails; next central sync reconciles it.
                try{
                    await using var db=await Open(config,ct);
                    await using var cmd=new NpgsqlCommand("""
                        UPDATE znuny_cached_tickets SET state='closed successful',
                            state_type='closed',changed_at=now(),synced_at=now()
                        WHERE ticket_id=@ticket
                        """,db);
                    cmd.Parameters.AddWithValue("ticket",ticketId);
                    await cmd.ExecuteNonQueryAsync(ct);
                    return Results.Ok(new{confirmed=true,cacheUpdated=true});
                }catch(PostgresException){
                    return Results.Ok(new{confirmed=true,cacheUpdated=false});
                }
            }catch(ZnunyReader.RemoteFailure e) when(!writeAttempted){
                // No TicketUpdate has been sent: expose the personal login or
                // TicketGet failure instead of an unexplained HTTP 502.
                return RemoteReadError(e,stage);
            }catch(ZnunyReader.RemoteFailure e) when(e.DefinitivelyRejected){
                return RemoteWriteRejected(e);
            }catch(ZnunyReader.RemoteFailure e){return UncertainWrite(e);}
        }).RequireRateLimiting("znuny-ticket-write");
    }

    private static IResult UncertainBooking(Guid id)=>Results.Problem(
        "Znuny-Zeitbuchung nicht eindeutig bestätigt. Sie bleibt zum Abgleich vorgemerkt "+
        "(Buchungs-ID "+id+"). Nicht erneut senden.",statusCode:502);

    private static bool BookingValueAllowed(string chosen,string current,
        IReadOnlyList<ZnunyReader.FieldOption> options)
    {
        if(string.IsNullOrWhiteSpace(chosen))return options.Count==0;
        return options.Any(x=>string.Equals(x.Key,chosen,StringComparison.Ordinal)) ||
            string.Equals(chosen,current,StringComparison.Ordinal) && current.Length>0;
    }

    private static async Task<IResult> AttachmentResult(
        string ticketId,string articleId,string fileId,bool inline,
        HttpContext http,UserManager<ApplicationUser> users,
        IConfiguration config,SecretVault vault,ZnunyReader reader,CancellationToken ct)
    {
        if(articleId.Length is <1 or >40 || fileId.Length is <1 or >30 ||
            !articleId.All(char.IsAsciiDigit)||!fileId.All(char.IsAsciiDigit))
            return Results.BadRequest(new{error="Ungültige Anhang-ID."});
        var context=await Context(http,users,config,vault,ticketId,false,ct);
        if(context.Error is {} denied)return denied;
        var credentials=await ReadCredentials(context,vault,ct);
        if(credentials is null)return MissingCredentials();
        try
        {
            var counter=new ZnunyReader.Counter(3);
            var session=await reader.SessionAsync(context.Server!,credentials.Value.Username,
                credentials.Value.Password,counter,ct);
            var file=await reader.GetAttachmentAsync(context.Server!,session,
                ticketId,articleId,fileId,counter,ct);
            if(file is null)return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            http.Response.Headers["X-Content-Type-Options"]="nosniff";
            if(!inline)
                return Results.File(file.Bytes,"application/octet-stream",
                    file.Filename,enableRangeProcessing:false);

            // Inline rendering is restricted to a handful of inert raster
            // formats. No remote images, HTML, SVG, PDF or arbitrary downloads
            // are embedded into the browser's HTML email sandbox.
            var mime=file.ContentType.Split(';')[0].Trim().ToLowerInvariant();
            if(mime is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp"))
                return Results.BadRequest(new{error="Kein unterstütztes Inline-Bild."});
            if(file.Bytes.Length>512*1024)
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            return Results.Ok(new{dataUri="data:"+mime+";base64,"+
                Convert.ToBase64String(file.Bytes)});
        }
        catch(ZnunyReader.RemoteFailure e) when(e.Code is
            "response-too-large" or "attachment-too-large")
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }
        catch(ZnunyReader.RemoteFailure e)
        {
            return RemoteReadError(e.Code);
        }
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
        if(ticketId is {Length:>80} || ticketId is not null &&
            (ticketId.Length==0||!ticketId.All(char.IsAsciiDigit)))
            return Fail(Results.BadRequest(new{error="Ungültige Ticket-ID."}));
        await using var db=await Open(config,ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT b.agent_id,
                COALESCE(p.username,''),
                c.service_username,
                CASE WHEN @ticket IS NULL THEN true
                     ELSE COALESCE((
                         t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id
                         OR (p.candidate_user_id IS NOT NULL AND (t.owner_agent_id IS NULL OR
                               t.owner_agent_id IN(0,p.candidate_user_id))
                         AND (t.responsible_agent_id IS NULL OR
                               t.responsible_agent_id IN(0,p.candidate_user_id)))
                         OR EXISTS(SELECT 1 FROM znuny_ticket_segments s
                           WHERE s.ticket_id=t.ticket_id AND
                             (s.user_id=@user OR EXISTS(
                               SELECT 1 FROM "AspNetUsers" account,
                                 unnest(s.participants) AS participant(email)
                               WHERE account."Id"=@user AND
                                 account."NormalizedEmail" IS NOT NULL AND
                                 account."NormalizedEmail"=upper(trim(participant.email)))))
                     ),false) END,
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
        // First deny access to foreign tickets, even if the integration is
        // disabled. Never reveal configuration state to unauthorized users.
        if(!vault.IsConfigured)return Fail(Results.Problem(
            "Secret-Vault ist nicht konfiguriert.",statusCode:503));
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
    private static IResult RemoteReadError(string code)=>RemoteReadError(
        new ZnunyReader.RemoteFailure(code),"Znuny-Datenabruf");
    private static IResult RemoteReadError(ZnunyReader.RemoteFailure failure,
        string operation)
    {
        // SessionCreate often replies HTTP 200 with a Znuny Error object
        // instead of a SessionID. This is a login failure, not an HTTP
        // gateway outage; surface actionable diagnostics without credentials.
        var loginProblem=operation.StartsWith("persönliche Znuny-API-Anmeldung",StringComparison.Ordinal);
        var detail=failure.Code switch{
            "session-rejected" or "remote-rejected" when loginProblem =>
                "Znuny hat für die persönlichen API-Zugangsdaten keine gültige "+
                "SessionID geliefert. Benutzernamen, Passwort, Agentenberechtigung "+
                "und den GenericInterface-Webservice prüfen.",
            "authentication" =>
                "Znuny hat den persönlichen API-Zugriff abgelehnt. "+
                "Berechtigung oder Zugangsdaten prüfen.",
            "route-not-found" =>
                "Znuny kennt die konfigurierte TicketGet-Route oder HTTP-Methode nicht.",
            "timeout" => "Znuny antwortet beim "+operation+" nicht rechtzeitig.",
            "network-error" => "Plenaro kann Znuny beim "+operation+" nicht erreichen.",
            "missing-ticket" or "ticket-id-mismatch" or "invalid-ticket" =>
                "Die Antwort von Znuny enthält kein passendes Ticket.",
            _ => "Znuny konnte "+operation+" nicht abschließen ("+failure.Code+")."
        };
        if(failure.RemoteCode is {Length:>0} remoteCode)
            detail+=" Znuny-Fehlercode: "+remoteCode+".";
        var status=loginProblem &&
            (failure.Code is "session-rejected" or "remote-rejected" or "authentication")
            ?StatusCodes.Status422UnprocessableEntity
            :StatusCodes.Status502BadGateway;
        return Results.Json(new{error=detail,stage=operation,code=failure.Code},
            statusCode:status);
    }
    private static IResult RemoteWriteRejected(ZnunyReader.RemoteFailure e)
    {
        var detail=e.Code switch {
            "route-not-found"=>
                "Znuny kennt die konfigurierte TicketUpdate-Route oder HTTP-Methode nicht. Bitte Ticketsystem → Classic-Routen prüfen.",
            "authentication"=>
                "Znuny hat die persönlichen Zugangsdaten oder die Schreibberechtigung abgelehnt.",
            "rate-limited"=>
                "Znuny hat die Anfrage wegen zu vieler Zugriffe abgelehnt.",
            _=>"Znuny hat die Ticketänderung ausdrücklich abgelehnt."
        };
        if(e.RemoteCode is {Length:>0} code)
            detail+=" Fehlercode: "+code+".";
        else if(e.Code=="remote-rejected")
            detail+=" Die Znuny-API-Konfiguration oder die benötigte Erweiterung (E-Mail-Versand) prüfen.";
        return Results.Json(new{error=detail},statusCode:422);
    }
    private static IResult UncertainWrite(ZnunyReader.RemoteFailure? failure=null)
    {
        var cause=failure?.Code switch {
            "timeout"=>"Zeitüberschreitung",
            "network-error"=>"Verbindungsabbruch",
            "server-error"=>"Znuny-Serverfehler (HTTP 5xx)",
            "invalid-json" or "protocol-error"=>"Ungültige Znuny-API-Antwort",
            "request-budget-exhausted"=>"Request-Budget überschritten",
            _=>"Keine eindeutige Bestätigung"
        };
        return Results.Problem(
            "Znuny hat die Änderung nicht eindeutig bestätigt ("+cause+
            "). Prüfe unbedingt den Ticketverlauf in Znuny, bevor du erneut sendest.",
            statusCode:502);
    }
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
