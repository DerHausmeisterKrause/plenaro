using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Graph;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.DayMarkers;

/// <summary>
/// WPF HomeOfficeService parity: one owned all-day Outlook appointment with
/// showAs=workingElsewhere, not just a BR/HO marker or email. Only an explicitly
/// connected user's delegated Microsoft mailbox is written. The immutable
/// (user,day) journal protects against duplicate calendar POSTs, including
/// after uncertain network responses and server restarts.
/// </summary>
public sealed class HomeOfficeCalendarService(
    IHttpClientFactory clients, EntraConfiguration entra, SecretVault vault)
{
    public sealed class CalendarFailure(string message,bool safeToRetry)
        :Exception(message)
    {
        public bool SafeToRetry{get;}=safeToRetry;
    }

    private const string TimeZone="W. Europe Standard Time";
    private const string Location="An anderem Ort tätig";
    private const string GraphBase="https://graph.microsoft.com/v1.0/";
    private const int MaxPages=5;
    private const int PageSize=100;

    public static string Subject(DateOnly day) =>
        "Homeoffice am "+day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);

    // Exactly the WPF fields: subject/body, start=00:00, end=NEXT day's
    // 00:00 (exclusive), allDay, no reminder, working elsewhere and location.
    internal static object CreateEventBody(DateOnly day,Guid transactionId)
    {
        var subject=Subject(day);
        var start=day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        var end=day.AddDays(1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        return new {
            subject,
            body=new{contentType="Text",content=subject},
            start=new{dateTime=start+"T00:00:00",timeZone=TimeZone},
            end=new{dateTime=end+"T00:00:00",timeZone=TimeZone},
            isAllDay=true,
            isReminderOn=false,
            showAs="workingElsewhere",
            location=new{displayName=Location},
            categories=Array.Empty<string>(),
            transactionId=transactionId.ToString("D")
        };
    }

    private static string Field(JsonElement element,string field) =>
        element.ValueKind==JsonValueKind.Object &&
        element.TryGetProperty(field,out var value) &&
        value.ValueKind==JsonValueKind.String
            ? value.GetString()??"" : "";

    internal static bool MatchesExisting(JsonElement element,DateOnly day)
    {
        if(element.ValueKind!=JsonValueKind.Object ||
           element.TryGetProperty("isCancelled",out var cancelled) &&
               cancelled.ValueKind==JsonValueKind.True ||
           !element.TryGetProperty("isAllDay",out var allDay) ||
           allDay.ValueKind!=JsonValueKind.True ||
           !string.Equals(Field(element,"subject"),Subject(day),StringComparison.Ordinal) ||
           !string.Equals(Field(element,"showAs"),"workingElsewhere",StringComparison.OrdinalIgnoreCase) ||
           !element.TryGetProperty("location",out var location) ||
           !string.Equals(Field(location,"displayName"),Location,StringComparison.OrdinalIgnoreCase) ||
           !element.TryGetProperty("start",out var start) ||
           !element.TryGetProperty("end",out var end))
            return false;
        var from=Field(start,"dateTime");
        var until=Field(end,"dateTime");
        // The request has Prefer: outlook.timezone="W. Europe Standard Time".
        // Compare LOCAL calendar dates, never UTC clock overlap (see #51).
        var expected=day.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        var next=day.AddDays(1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        return from.Length>=10 && until.Length>=10 &&
            from.StartsWith(expected,StringComparison.Ordinal) &&
            until.StartsWith(next,StringComparison.Ordinal);
    }

    public async Task<string> PrepareTokenAsync(Guid user,CancellationToken ct)
    {
        if(!entra.Enabled||!vault.IsConfigured)
            throw new CalendarFailure("Microsoft-365-Kalender ist nicht eingerichtet.",true);
        var refresh=await vault.GetForIntegrationAsync("user",user,
            GraphCalendarService.RefreshSecretName,ct);
        if(string.IsNullOrEmpty(refresh))
            throw new CalendarFailure(
                "Bitte Outlook unter Einstellungen → Aufgaben & Kalender verbinden.",true);
        using var req=new HttpRequestMessage(HttpMethod.Post,
            $"https://login.microsoftonline.com/{entra.TenantId}/oauth2/v2.0/token"){
            Content=new FormUrlEncodedContent(new Dictionary<string,string>{
                ["client_id"]=entra.ClientId,
                ["client_secret"]=entra.ClientSecret,
                ["grant_type"]="refresh_token",
                ["refresh_token"]=refresh,
                ["scope"]="User.Read Calendars.ReadWrite offline_access"
            })
        };
        HttpResponseMessage response;
        try{response=await clients.CreateClient("Plenaro.Graph").SendAsync(req,ct);}
        catch(HttpRequestException){
            throw new CalendarFailure("Microsoft-Anmeldung derzeit nicht erreichbar.",true);
        }catch(TaskCanceledException) when(!ct.IsCancellationRequested){
            throw new CalendarFailure("Microsoft-Anmeldung antwortet nicht.",true);
        }
        using(response){
            if(response.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new CalendarFailure(
                    "Outlook-Berechtigung Calendars.ReadWrite fehlt. Bitte Microsoft 365 unter Einstellungen → Aufgaben & Kalender neu verbinden und Kalender-Schreibzugriff freigeben.",true);
            if(!response.IsSuccessStatusCode)
                throw new CalendarFailure("Microsoft-Anmeldung derzeit nicht verfügbar.",true);
            using var body=await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
            if(body.RootElement.TryGetProperty("refresh_token",out var next)&&
               next.GetString() is {Length:>0} newRefresh)
                await vault.SetAsync("user",user,GraphCalendarService.RefreshSecretName,
                    newRefresh,ct);
            var scopes=Field(body.RootElement,"scope").Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if(!scopes.Contains("Calendars.ReadWrite",StringComparer.OrdinalIgnoreCase))
                throw new CalendarFailure(
                    "Outlook Calendars.ReadWrite ist nicht freigegeben. Bitte Microsoft 365 erneut verbinden.",true);
            var token=Field(body.RootElement,"access_token");
            if(string.IsNullOrWhiteSpace(token))
                throw new CalendarFailure("Kein gültiges Outlook-Kalendertoken erhalten.",true);
            return token;
        }
    }

    private async Task<List<string>> FindExistingAsync(
        string token,DateOnly day,CancellationToken ct)
    {
        var timezone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        DateTimeOffset BerlinMidnight(DateOnly date){
            var local=date.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local,timezone.GetUtcOffset(local));
        }
        var from=BerlinMidnight(day).ToString("O",CultureInfo.InvariantCulture);
        var until=BerlinMidnight(day.AddDays(1)).ToString("O",CultureInfo.InvariantCulture);
        var uri=GraphBase+"me/calendar/calendarView?startDateTime="+
            Uri.EscapeDataString(from)+"&endDateTime="+Uri.EscapeDataString(until)+
            "&$select=id,subject,start,end,isAllDay,isCancelled,showAs,location,bodyPreview"+
            "&$top="+PageSize;
        var matches=new List<string>();
        for(var page=0;page<MaxPages;page++){
            using var req=new HttpRequestMessage(HttpMethod.Get,uri);
            req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
            req.Headers.TryAddWithoutValidation("Prefer",
                "outlook.timezone=\""+TimeZone+"\"");
            HttpResponseMessage response;
            try{response=await clients.CreateClient("Plenaro.Graph").SendAsync(req,ct);}
            catch(HttpRequestException){
                throw new CalendarFailure("Outlook-Kalender ist nicht erreichbar; kein Termin erstellt.",true);
            }catch(TaskCanceledException) when(!ct.IsCancellationRequested){
                throw new CalendarFailure("Outlook-Kalender antwortet nicht; kein Termin erstellt.",true);
            }
            using(response){
                if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    throw new CalendarFailure("Outlook-Kalenderberechtigung fehlt. Bitte Microsoft 365 neu verbinden.",true);
                if(!response.IsSuccessStatusCode)
                    throw new CalendarFailure(
                        "Outlook-Termine konnten nicht auf vorhandene Homeoffice-Einträge geprüft werden.",true);
                using var doc=await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
                if(!doc.RootElement.TryGetProperty("value",out var events)||
                   events.ValueKind!=JsonValueKind.Array)
                    throw new CalendarFailure("Outlook lieferte keine gültige Terminliste.",true);
                foreach(var ev in events.EnumerateArray()){
                    if(MatchesExisting(ev,day)){
                        var id=Field(ev,"id");
                        if(string.IsNullOrWhiteSpace(id)||id.Length>512)
                            throw new CalendarFailure("Outlook lieferte eine ungültige Termin-ID.",true);
                        matches.Add(id);
                        if(matches.Count>1)
                            throw new CalendarFailure(
                                "Mehrere passende Plenaro-Homeoffice-Termine gefunden; kein weiterer Termin erstellt.",false);
                    }
                }
                var next=Field(doc.RootElement,"@odata.nextLink");
                if(string.IsNullOrEmpty(next))return matches;
                if(!Uri.TryCreate(next,UriKind.Absolute,out var link) ||
                   link.Scheme!=Uri.UriSchemeHttps ||
                   !link.Host.Equals("graph.microsoft.com",StringComparison.OrdinalIgnoreCase) ||
                   !link.AbsolutePath.Equals("/v1.0/me/calendar/calendarView",
                       StringComparison.Ordinal))
                    throw new CalendarFailure("Ungültige Outlook-Kalenderpagination; kein Termin erstellt.",true);
                uri=link.AbsoluteUri;
            }
        }
        throw new CalendarFailure(
            "Die Outlook-Terminliste ist zu groß, um Doppeltermine sicher auszuschließen.",false);
    }

    private async Task<string> CreateAsync(
        string token,DateOnly day,Guid transactionId,CancellationToken ct)
    {
        using var req=new HttpRequestMessage(HttpMethod.Post,GraphBase+"me/calendar/events"){
            Content=JsonContent.Create(CreateEventBody(day,transactionId))
        };
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        HttpResponseMessage response;
        try{response=await clients.CreateClient("Plenaro.Graph").SendAsync(req,ct);}
        catch(HttpRequestException){
            throw new CalendarFailure(
                "Outlook-Terminstatus unbekannt (Netzwerkfehler). Kein automatischer erneuter Erstellungsversuch.",false);
        }catch(TaskCanceledException) when(!ct.IsCancellationRequested){
            throw new CalendarFailure(
                "Outlook-Terminstatus unbekannt (Timeout). Bitte im Outlook-Kalender prüfen.",false);
        }
        using(response){
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new CalendarFailure(
                    "Outlook Calendars.ReadWrite fehlt oder ist abgelaufen. Bitte Microsoft 365 neu verbinden.",true);
            if(response.StatusCode==HttpStatusCode.BadRequest)
                throw new CalendarFailure("Outlook hat den Homeoffice-Terminentwurf abgelehnt.",true);
            if(response.StatusCode!=HttpStatusCode.Created)
                throw new CalendarFailure(
                    "Outlook-Terminstatus unbekannt (Graph HTTP "+(int)response.StatusCode+
                    "). Bitte Kalender prüfen, nicht automatisch erneut erstellen.",false);
            try{
                using var data=await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
                var id=Field(data.RootElement,"id");
                if(!string.IsNullOrWhiteSpace(id)&&id.Length<=512)return id;
            }catch(JsonException){/* 201 may already have created event. */}
            throw new CalendarFailure(
                "Outlook bestätigte einen Termin, lieferte aber keine gültige ID. Bitte Kalender prüfen.",false);
        }
    }

    public async Task<string> EnsureCreatedAsync(NpgsqlConnection db,Guid user,
        DateOnly day,string token,CancellationToken ct)
    {
        // One in-flight or completed Graph POST per user/day, even when two
        // browser tabs submit simultaneously. No automatic second POST after
        // an uncertain result. Reconciliation is READ ONLY.
        var requestId=Guid.NewGuid();
        var fresh=false;
        await using(var claim=new NpgsqlCommand("""
            INSERT INTO homeoffice_calendar_events(user_id,day,status,transaction_id)
            VALUES(@user,@day,'processing',@tx)
            ON CONFLICT(user_id,day) DO NOTHING
            """,db)){
            claim.Parameters.AddWithValue("user",user);
            claim.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
            claim.Parameters.AddWithValue("tx",requestId);
            fresh=await claim.ExecuteNonQueryAsync(ct)==1;
        }
        if(!fresh){
            await using var inspect=new NpgsqlCommand("""
                SELECT status,event_id,transaction_id FROM homeoffice_calendar_events
                WHERE user_id=@user AND day=@day
                """,db);
            inspect.Parameters.AddWithValue("user",user);
            inspect.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
            await using var reader=await inspect.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))
                throw new CalendarFailure("Outlook-Terminzustand wurde parallel verändert.",true);
            var status=reader.GetString(0);
            var knownId=reader.IsDBNull(1)?"":reader.GetString(1);
            await reader.CloseAsync();
            if(status=="created" && !string.IsNullOrEmpty(knownId))
                return knownId;
            if(status=="processing")
                throw new CalendarFailure(
                    "Ein Outlook-Homeoffice-Terminantrag läuft bereits. Kein doppelter Termin wird erstellt.",false);
            if(status=="unknown"){
                var recovered=await FindExistingAsync(token,day,ct);
                if(recovered.Count==1){
                    await SaveStatusAsync(db,user,day,"created",recovered[0],
                        CancellationToken.None);
                    return recovered[0];
                }
                throw new CalendarFailure(
                    "Der vorherige Outlook-Erstellungsversuch ist unklar. Bitte im Outlook-Kalender prüfen; kein zweiter POST.",false);
            }
            throw new CalendarFailure(
                "Homeoffice-Terminstatus nicht zur Erzeugung freigegeben.",false);
        }
        var attemptedPost=false;
        try{
            var existing=await FindExistingAsync(token,day,ct);
            var id=existing.Count==1?existing[0]:null;
            if(id is null){
                attemptedPost=true;
                id=await CreateAsync(token,day,requestId,ct);
            }
            await SaveStatusAsync(db,user,day,"created",id,
                CancellationToken.None);
            return id;
        }catch(CalendarFailure e){
            if(attemptedPost&&!e.SafeToRetry){
                await SaveStatusAsync(db,user,day,"unknown",null,
                    CancellationToken.None);
            }else if(e.SafeToRetry){
                await DeleteReservationAsync(db,user,day,CancellationToken.None);
            }else{
                await SaveStatusAsync(db,user,day,"unknown",null,
                    CancellationToken.None);
            }
            throw;
        }catch{
            // Unknown result after POST: do not automatically repeat.
            if(attemptedPost)
                await SaveStatusAsync(db,user,day,"unknown",null,
                    CancellationToken.None);
            else
                await DeleteReservationAsync(db,user,day,CancellationToken.None);
            throw;
        }
    }

    private static async Task SaveStatusAsync(
        NpgsqlConnection db,Guid user,DateOnly day,string status,string? id,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            UPDATE homeoffice_calendar_events
            SET status=@status,event_id=@id,updated_at=now()
            WHERE user_id=@user AND day=@day
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
        cmd.Parameters.AddWithValue("status",status);
        cmd.Parameters.AddWithValue("id",(object?)id??DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteReservationAsync(
        NpgsqlConnection db,Guid user,DateOnly day,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            DELETE FROM homeoffice_calendar_events
            WHERE user_id=@user AND day=@day AND status='processing'
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.Add("day",NpgsqlDbType.Date).Value=day;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
