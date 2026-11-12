using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.DayMarkers;

/// <summary>
/// Delegated Graph Mail.Send – sender is the explicitly connected user's
/// own Microsoft mailbox. Never use app-only Mail.Send or a shared mailbox.
/// Graph HTTP 202 means accepted, not guaranteed delivered.
/// </summary>
public sealed class BrMailService(
    IHttpClientFactory clients, EntraConfiguration entra, SecretVault vault)
{
    public sealed class MailFailure(string message,bool safeToRetry):Exception(message)
    {
        public bool SafeToRetry{get;}=safeToRetry;
    }

    public async Task<string> PrepareTokenAsync(Guid user,CancellationToken ct)
    {
        if(!entra.Enabled||!vault.IsConfigured)
            throw new MailFailure("Microsoft-365-Mailversand ist nicht eingerichtet.",true);
        var refresh=await vault.GetForIntegrationAsync("user",user,
            Plenaro.Api.Graph.GraphCalendarService.RefreshSecretName,ct);
        if(string.IsNullOrEmpty(refresh))
            throw new MailFailure(
                "Bitte Outlook unter Einstellungen → Microsoft 365 verbinden.",true);
        using var req=new HttpRequestMessage(HttpMethod.Post,
            $"https://login.microsoftonline.com/{entra.TenantId}/oauth2/v2.0/token"){
            Content=new FormUrlEncodedContent(new Dictionary<string,string>{
                ["client_id"]=entra.ClientId,
                ["client_secret"]=entra.ClientSecret,
                ["grant_type"]="refresh_token",
                ["refresh_token"]=refresh,
                ["scope"]="User.Read Mail.Send offline_access"
            })
        };
        HttpResponseMessage response;
        try{
            response=await clients.CreateClient("Plenaro.Graph").SendAsync(req,ct);
        }catch(HttpRequestException){
            throw new MailFailure("Microsoft-Anmeldung derzeit nicht erreichbar.",true);
        }catch(TaskCanceledException) when(!ct.IsCancellationRequested){
            throw new MailFailure("Microsoft-Anmeldung antwortet nicht.",true);
        }
        using(response){
            if(response.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new MailFailure(
                    "Outlook-Berechtigung Mail.Send fehlt. Bitte Microsoft 365 erneut verbinden und Mail.Send freigeben.",true);
            if(!response.IsSuccessStatusCode)
                throw new MailFailure("Microsoft-Anmeldung derzeit nicht verfügbar.",true);
            using var body=await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
            if(body.RootElement.TryGetProperty("refresh_token",out var next)&&
                next.GetString() is {Length:>0} newRefresh)
                await vault.SetAsync("user",user,
                    Plenaro.Api.Graph.GraphCalendarService.RefreshSecretName,newRefresh,ct);
            var scope=body.RootElement.TryGetProperty("scope",out var granted)?
                granted.GetString()??"":"";
            if(!scope.Split(' ',StringSplitOptions.RemoveEmptyEntries)
                    .Contains("Mail.Send",StringComparer.OrdinalIgnoreCase))
                throw new MailFailure("Outlook Mail.Send wurde noch nicht freigegeben.",true);
            if(!body.RootElement.TryGetProperty("access_token",out var access)||
               access.GetString() is not {Length:>0} value)
                throw new MailFailure("Kein gültiges Microsoft-Token verfügbar.",true);
            return value;
        }
    }

    public static (string Subject,string Body) Compose(DateOnly day,string displayName)
    {
        var date=day.ToString("dd.MM.yyyy",
            System.Globalization.CultureInfo.GetCultureInfo("de-DE"));
        var name=(displayName??"").Trim();
        if(string.IsNullOrWhiteSpace(name))name="Plenaro-Benutzer";
        // Requested wording is literally Homeoffice even though triggered by BR.
        // Keep this explicit until the user approves a terminology change.
        return ($"Homeoffice am {date}",
            $"Servus,\n\nich mache am {date} Homeoffice.\n\nmfg.\n{name}");
    }

    public async Task SendAsync(string token,DateOnly day,string name,
        IReadOnlyList<string> recipients,CancellationToken ct)
    {
        var (subject,body)=Compose(day,name);
        var requestBody=new{
            message=new{
                subject,
                body=new{contentType="Text",content=body},
                toRecipients=recipients.Select(address=>new{
                    emailAddress=new{address}
                }).ToArray()
            },
            saveToSentItems=true
        };
        using var req=new HttpRequestMessage(HttpMethod.Post,
            "https://graph.microsoft.com/v1.0/me/sendMail"){
            Content=System.Net.Http.Json.JsonContent.Create(requestBody)
        };
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        HttpResponseMessage response;
        try{
            // Deliberately NO retry: timeout or connection loss after submission
            // might mean Graph accepted the email even without our response.
            response=await clients.CreateClient("Plenaro.Graph").SendAsync(req,ct);
        }catch(HttpRequestException){
            throw new MailFailure("Versandstatus unbekannt. Nicht erneut senden, zunächst Gesendete Elemente prüfen.",false);
        }catch(TaskCanceledException) when(!ct.IsCancellationRequested){
            throw new MailFailure("Versandstatus unbekannt (Timeout). Nicht automatisch wiederholen.",false);
        }
        using(response){
            if(response.StatusCode==HttpStatusCode.Accepted)return;
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new MailFailure("Mail.Send fehlt oder ist abgelaufen. Bitte Outlook erneut verbinden.",true);
            // Even an error after the POST cannot guarantee the absence of a mail.
            throw new MailFailure(
                $"Graph-Antwort HTTP {(int)response.StatusCode}. Versandstatus unklar; nicht automatisch wiederholen.",false);
        }
    }
}
