using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.DayMarkers;

/// <summary>
/// Personal mail recipients and confirmed BR-request mail delivery.
/// The WPF reference sends an Outlook mail for HO; this explicit BR route
/// intentionally uses the user-requested Homeoffice mail wording.
/// </summary>
public static class BrRequestEndpoints
{
    public sealed record RecipientSettings(string Recipient1="",string Recipient2="");
    public sealed record BrRequestStatus(string Status);

    public static void MapPlenaroBrRequests(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/br-requests").RequireAuthorization();

        group.MapGet("/settings",async(HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            var user=await Owner(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            return Results.Ok(await Recipients(db,user.Id,ct));
        });

        group.MapPut("/settings",async(RecipientSettings input,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            var user=await Owner(http,users);
            if(user is null)return Results.Unauthorized();
            if(!TryNormalize(input,out var normalized))
                return Results.BadRequest(new{error="Bitte gültige einzelne E-Mail-Adressen eingeben (maximal zwei Empfänger)."});
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO user_preferences(user_id,preferences,updated_at)
                VALUES(@uid,jsonb_build_object(
                  'brMailRecipient1',@one,'brMailRecipient2',@two),now())
                ON CONFLICT(user_id) DO UPDATE
                SET preferences=COALESCE(user_preferences.preferences,'{}'::jsonb)
                  || EXCLUDED.preferences, updated_at=now()
                """,db);
            cmd.Parameters.AddWithValue("uid",user.Id);
            cmd.Parameters.AddWithValue("one",normalized.Recipient1);
            cmd.Parameters.AddWithValue("two",normalized.Recipient2);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/{day}/status",async(string day,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>{
            if(!TryDay(day,out var parsed))
                return Results.BadRequest(new{error="Ungültiges Datum."});
            var user=await Owner(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT status FROM br_mail_requests WHERE user_id=@uid AND day=@day
                """,db);
            cmd.Parameters.AddWithValue("uid",user.Id);
            cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=parsed;
            return Results.Ok(new BrRequestStatus(
                await cmd.ExecuteScalarAsync(ct) as string??"none"));
        });

        group.MapPost("/{day}/send",async(string day,HttpContext http,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,BrMailService sender,CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            if(!TryDay(day,out var parsed))
                return Results.BadRequest(new{error="Ungültiger BR-Tag."});
            var user=await Owner(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            // A request can only be sent for a BR marker already persisted by
            // this exact user, never for an arbitrary client-supplied date.
            await using(var marker=new NpgsqlCommand("""
                SELECT EXISTS(SELECT 1 FROM day_markers
                  WHERE user_id=@uid AND day=@day AND (is_br OR is_ho))
                """,db)){
                marker.Parameters.AddWithValue("uid",user.Id);
                marker.Parameters.Add("day",NpgsqlDbType.Date).Value=parsed;
                if(await marker.ExecuteScalarAsync(ct) is not true)
                    return Results.Conflict(new{
                        error="Bitte zuerst den HO- oder BR-Marker für diesen Tag speichern."});
            }
            var configRecipients=await Recipients(db,user.Id,ct);
            if(!TryNormalize(configRecipients,out var recipients))
                return Results.BadRequest(new{error="Die persönlichen E-Mail-Empfänger sind ungültig."});
            var addresses=new[]{recipients.Recipient1,recipients.Recipient2}
                .Where(x=>!string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(addresses.Length==0)return Results.Conflict(new{
                error="Bitte zuerst unter Einstellungen → Aufgaben & Kalender mindestens einen E-Mail-Empfänger hinterlegen."});
            // Check Microsoft consent BEFORE claiming a one-time send: missing
            // Mail.Send consent must not burn the daily idempotency key.
            string token;
            try{token=await sender.PrepareTokenAsync(user.Id,ct);}
            catch(BrMailService.MailFailure e){
                return Results.Conflict(new{error=e.Message,code="mail-consent"});
            }
            // Unique primary key + ON CONFLICT prevents concurrent double mails,
            // including after a request timeout or a service restart.
            await using(var claim=new NpgsqlCommand("""
                INSERT INTO br_mail_requests(user_id,day,status)
                VALUES(@uid,@day,'processing')
                ON CONFLICT(user_id,day) DO NOTHING
                """,db)){
                claim.Parameters.AddWithValue("uid",user.Id);
                claim.Parameters.Add("day",NpgsqlDbType.Date).Value=parsed;
                if(await claim.ExecuteNonQueryAsync(ct)==0)
                    return Results.Conflict(new{
                        error="Für diesen Tag existiert bereits ein Versandversuch. Kein erneuter automatischer Versand (Doppelversandschutz).",
                        code="already-submitted"});
            }

            try{
                // The sender is the user's own delegated M365 mailbox.
                await sender.SendAsync(token,parsed,user.DisplayName,addresses,ct);
                await SaveState(db,user.Id,parsed,"accepted",CancellationToken.None);
                return Results.Ok(new{status="accepted",
                    message="Microsoft Graph hat den Versand angenommen. Die tatsächliche Zustellung ist noch nicht bestätigt."});
            }catch(BrMailService.MailFailure e){
                if(e.SafeToRetry){
                    await using var remove=new NpgsqlCommand("""
                        DELETE FROM br_mail_requests
                        WHERE user_id=@uid AND day=@day AND status='processing'
                        """,db);
                    remove.Parameters.AddWithValue("uid",user.Id);
                    remove.Parameters.Add("day",NpgsqlDbType.Date).Value=parsed;
                    await remove.ExecuteNonQueryAsync(CancellationToken.None);
                    return Results.Conflict(new{error=e.Message,code="mail-consent"});
                }
                await SaveState(db,user.Id,parsed,"unknown",CancellationToken.None);
                return Results.Problem(e.Message,statusCode:502);
            }catch(Exception) when(!ct.IsCancellationRequested){
                await SaveState(db,user.Id,parsed,"unknown",CancellationToken.None);
                return Results.Problem(
                    "Versandstatus unbekannt. Bitte zuerst im Outlook-Ordner Gesendete Elemente prüfen. Keine automatische Wiederholung.",
                    statusCode:502);
            }
        }).RequireRateLimiting("znuny-native-start");
    }

    public static bool TryNormalize(RecipientSettings input,out RecipientSettings result)
    {
        result=new RecipientSettings((input.Recipient1??"").Trim(),
            (input.Recipient2??"").Trim());
        foreach(var address in new[]{result.Recipient1,result.Recipient2}){
            if(address.Length==0)continue;
            if(address.Length>254||address.Any(char.IsControl)||
               address.Contains(',')||address.Contains(';')||
               !MailAddress.TryCreate(address,out var parsed)||
               !parsed.Address.Equals(address,StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static bool TryDay(string input,out DateOnly date)=>
        DateOnly.TryParseExact(input,"yyyy-MM-dd",CultureInfo.InvariantCulture,
            DateTimeStyles.None,out date)&&date.Year is >=2000 and <=2099;

    private static async Task<ApplicationUser?> Owner(HttpContext http,
        UserManager<ApplicationUser> users)
    {
        var user=await users.GetUserAsync(http.User);
        return user is {IsDisabled:false}?user:null;
    }

    private static async Task<RecipientSettings> Recipients(NpgsqlConnection db,
        Guid user,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT preferences->>'brMailRecipient1',preferences->>'brMailRecipient2'
            FROM user_preferences WHERE user_id=@uid
            """,db);
        cmd.Parameters.AddWithValue("uid",user);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?
            new RecipientSettings(reader.IsDBNull(0)?"":reader.GetString(0),
                reader.IsDBNull(1)?"":reader.GetString(1)):
            new RecipientSettings();
    }

    private static async Task SaveState(NpgsqlConnection db,Guid user,
        DateOnly day,string status,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            UPDATE br_mail_requests SET status=@status,updated_at=now()
            WHERE user_id=@uid AND day=@day AND status='processing'
            """,db);
        cmd.Parameters.AddWithValue("uid",user);
        cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
        cmd.Parameters.AddWithValue("status",status);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration config,
        CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }

    private static async Task<bool> ValidCsrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static IResult CsrfError()=>Results.BadRequest(new{
        error="Sicherheitstoken fehlt oder ist ungültig."
    });
}
