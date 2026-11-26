using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.DayMarkers;
using Plenaro.Api.Identity;
using Plenaro.Api.Znuny;

namespace Plenaro.Api.Graph;

// The outbox is committed in the SAME transaction as the segment. Graph
// failures must not lose local planning or cause an untracked duplicate event.
public sealed class SegmentCalendarSync(IServiceScopeFactory scopes,
    IConfiguration config,ILogger<SegmentCalendarSync> logger) : BackgroundService
{
    private const string Graph="https://graph.microsoft.com/v1.0/me/events";
    public const string MarkerProperty="String {5c7d116b-8152-41ac-8b76-c43f460f4f87} Name PlenaroSegment";
    private sealed record Job(string Kind,Guid Id,Guid User,long Revision,string? EventId,bool Attempted,Guid Lease);
    public sealed record SegmentData(string Title,DateTime Start,DateTime End,string Note,string[] Participants,string Link);

    public static async Task QueueAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        string kind,Guid id,Guid user,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            INSERT INTO segment_calendar_sync(kind,segment_id,user_id)
            VALUES(@kind,@id,@user)
            ON CONFLICT(kind,segment_id,user_id) DO UPDATE SET
                revision=segment_calendar_sync.revision+1,status='pending',
                last_error='',next_attempt=now()
            """,db,tx);
        cmd.Parameters.AddWithValue("kind",kind);
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("user",user);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        try{await db.OpenAsync(ct);return db;}catch{await db.DisposeAsync();throw;}
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested){
            try{
                await using var db=await OpenAsync(stoppingToken);
                var job=await ClaimAsync(db,stoppingToken);
                if(job is null){await Task.Delay(TimeSpan.FromSeconds(5),stoppingToken);continue;}
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                deadline.CancelAfter(TimeSpan.FromSeconds(90));
                try{await ProcessAsync(db,job,deadline.Token);}
                catch(Exception e) when(!stoppingToken.IsCancellationRequested){
                    var message=e is SyncFailure or HomeOfficeCalendarService.CalendarFailure
                        ?e.Message:"Outlook-Synchronisierung nicht eindeutig bestätigt. Erneute Prüfung folgt.";
                    // Do not log Graph URLs, tokens, meeting subjects or participant addresses.
                    logger.LogWarning("Segment calendar sync failed ({Type})",e.GetType().Name);
                    await FinishAsync(db,job,null,"error",message,stoppingToken);
                }
            }catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception e){
                logger.LogWarning("Segment calendar worker unavailable ({Type})",e.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(15),stoppingToken);
            }
        }
    }

    private static async Task<Job?> ClaimAsync(NpgsqlConnection db,CancellationToken ct)
    {
        var lease=Guid.NewGuid();
        await using var cmd=new NpgsqlCommand("""
            WITH candidate AS (
                SELECT kind,segment_id,user_id FROM segment_calendar_sync
                WHERE status IN ('pending','syncing','error') AND next_attempt<=now()
                  AND (lease_until IS NULL OR lease_until<now())
                ORDER BY next_attempt,segment_id LIMIT 1 FOR UPDATE SKIP LOCKED
            )
            UPDATE segment_calendar_sync s SET status='syncing',lease_token=@lease,
                lease_until=now()+interval '3 minutes'
            FROM candidate c WHERE s.kind=c.kind AND s.segment_id=c.segment_id AND s.user_id=c.user_id
            RETURNING s.kind,s.segment_id,s.user_id,s.revision,s.event_id,s.create_attempted
            """,db);
        cmd.Parameters.AddWithValue("lease",lease);
        await using var row=await cmd.ExecuteReaderAsync(ct);
        return await row.ReadAsync(ct)?new Job(row.GetString(0),row.GetGuid(1),row.GetGuid(2),
            row.GetInt64(3),row.IsDBNull(4)?null:row.GetString(4),row.GetBoolean(5),lease):null;
    }

    private static void Bind(NpgsqlCommand cmd,Job job)
    {
        cmd.Parameters.AddWithValue("kind",job.Kind);
        cmd.Parameters.AddWithValue("id",job.Id);
        cmd.Parameters.AddWithValue("user",job.User);
        cmd.Parameters.AddWithValue("lease",job.Lease);
    }

    private static async Task FinishAsync(NpgsqlConnection db,Job job,string? eventId,
        string status,string error,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            UPDATE segment_calendar_sync SET
                event_id=COALESCE(@event,event_id),
                status=CASE WHEN revision=@revision THEN @status ELSE 'pending' END,
                last_error=CASE WHEN revision=@revision THEN @error ELSE '' END,
                next_attempt=CASE WHEN revision=@revision THEN now()+interval '5 minutes' ELSE now() END,
                lease_until=NULL,lease_token=NULL
            WHERE kind=@kind AND segment_id=@id AND user_id=@user AND lease_token=@lease
            """,db);
        Bind(cmd,job);
        cmd.Parameters.AddWithValue("revision",job.Revision);
        cmd.Parameters.AddWithValue("status",status);
        cmd.Parameters.AddWithValue("error",error);
        cmd.Parameters.Add("event",NpgsqlTypes.NpgsqlDbType.Text).Value=(object?)eventId??DBNull.Value;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task MarkAttemptAsync(NpgsqlConnection db,Job job,bool attempted,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            UPDATE segment_calendar_sync SET create_attempted=@attempted
            WHERE kind=@kind AND segment_id=@id AND user_id=@user AND lease_token=@lease
            """,db);
        Bind(cmd,job);cmd.Parameters.AddWithValue("attempted",attempted);
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)throw new SyncFailure("Outlook-Auftrag wurde zwischenzeitlich geändert.");
    }

    private static async Task<SegmentData?> ReadSegmentAsync(NpgsqlConnection db,Job job,CancellationToken ct)
    {
        var sql=job.Kind=="ticket"?"""
            SELECT t.title,s.starts_at,s.ends_at,s.note,s.participants,s.ticket_id
            FROM znuny_ticket_segments s JOIN znuny_cached_tickets t ON t.ticket_id=s.ticket_id
            WHERE s.id=@id AND s.user_id=@user
            """:"""
            SELECT t.title,s.starts_at,s.ends_at,s.note,s.participants,''::text
            FROM task_segments s JOIN tasks t ON t.id=s.task_id AND t.owner_user_id=s.owner_user_id
            WHERE s.id=@id AND s.owner_user_id=@user
            """;
        string ticket;
        SegmentData data;
        await using(var cmd=new NpgsqlCommand(sql,db)){
            cmd.Parameters.AddWithValue("id",job.Id);
            cmd.Parameters.AddWithValue("user",job.User);
            await using var row=await cmd.ExecuteReaderAsync(ct);
            if(!await row.ReadAsync(ct))return null;
            data=new(row.GetString(0),row.GetDateTime(1),row.GetDateTime(2),
                row.GetString(3),row.GetFieldValue<string[]>(4),"");
            ticket=row.GetString(5);
        }
        if(job.Kind=="ticket"){
            var server=await ZnunyEndpoints.LoadServerAsync(db,ct);
            data=data with{Link=ZnunyTicketDetailEndpoints.BuildDirectTicketUrl(server.WebUrl,ticket)};
        }
        return data;
    }

    public static string Marker(string kind,Guid user,Guid segmentId)=>kind+":"+user.ToString("N")+":"+segmentId.ToString("N");
    public static Dictionary<string,object> Payload(SegmentData data,string marker,Guid transactionId,bool create)
    {
        string Date(DateTime value)=>value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff",CultureInfo.InvariantCulture);
        var body=new Dictionary<string,object>{
            ["subject"]=string.IsNullOrWhiteSpace(data.Title)?"Plenaro-Zeitsegment":data.Title,
            ["body"]=new{contentType="Text",content=string.Join("\n",new[]{data.Note,data.Link}.Where(s=>!string.IsNullOrWhiteSpace(s)))},
            ["start"]=new{dateTime=Date(data.Start),timeZone="UTC"},
            ["end"]=new{dateTime=Date(data.End),timeZone="UTC"},
            ["showAs"]="busy",
            ["isAllDay"]=false,
            ["attendees"]=data.Participants.Select(s=>s.Trim()).Where(s=>s.Length>0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(s=>new{emailAddress=new{address=s},type="required"}).ToArray()
        };
        if(create){
            body["transactionId"]=transactionId.ToString("D");
            body["singleValueExtendedProperties"]=new[]{new{id=MarkerProperty,value=marker}};
        }
        return body;
    }

    private async Task ProcessAsync(NpgsqlConnection db,Job job,CancellationToken ct)
    {
        var data=await ReadSegmentAsync(db,job,ct);
        if(data is null&&!job.Attempted&&job.EventId is null){
            await FinishAsync(db,job,null,"removed","",ct);return;
        }
        using var scope=scopes.CreateScope();
        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var entra=scope.ServiceProvider.GetRequiredService<EntraConfiguration>();
        var user=await users.FindByIdAsync(job.User.ToString());
        if(user is null||user.IsDisabled||!entra.Enabled||
           !(await users.GetLoginsAsync(user)).Any(x=>x.LoginProvider==EntraConfiguration.Provider&&
               x.ProviderKey.StartsWith(entra.TenantId+":",StringComparison.Ordinal)))
            throw new SyncFailure("Outlook-Verbindung fehlt oder das Konto ist deaktiviert.");
        var token=await scope.ServiceProvider.GetRequiredService<HomeOfficeCalendarService>()
            .PrepareTokenAsync(job.User,ct);
        var client=scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Plenaro.Graph");
        var marker=Marker(job.Kind,job.User,job.Id);
        var id=await FindAsync(client,token,job.EventId,marker,ct);
        if(id is null){
            if(job.Attempted||job.EventId is not null)
                throw new SyncFailure("Outlook-Termin nicht eindeutig auffindbar. Kein weiterer Erstellungsversuch; bitte Kalender prüfen.");
            if(data is null){await FinishAsync(db,job,null,"removed","",ct);return;}
            // Persist intent BEFORE the network request. If the process dies
            // or Graph times out, only the exact marker is searched afterwards.
            await MarkAttemptAsync(db,job,true,ct);
            using var created=await SendAsync(client,token,HttpMethod.Post,Graph,
                Payload(data,marker,job.Id,true),ct);
            if(!created.IsSuccessStatusCode){
                if(created.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or
                   HttpStatusCode.Forbidden or HttpStatusCode.NotFound or HttpStatusCode.TooManyRequests)
                    await MarkAttemptAsync(db,job,false,ct);
                throw HttpFailure(created.StatusCode);
            }
            using var doc=await JsonDocument.ParseAsync(await created.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
            id=Field(doc.RootElement,"id");
            if(string.IsNullOrEmpty(id))throw new SyncFailure("Outlook-Erstellung nicht eindeutig bestätigt. Kein zweiter POST.");
        }else{
            using var result=await SendAsync(client,token,data is null?HttpMethod.Delete:HttpMethod.Patch,
                Graph+"/"+Uri.EscapeDataString(id),data is null?null:Payload(data,marker,job.Id,false),ct);
            if(!result.IsSuccessStatusCode)throw HttpFailure(result.StatusCode);
        }
        await FinishAsync(db,job,id,data is null?"removed":"synced","",ct);
    }

    private static string Field(JsonElement element,string key)=>
        element.TryGetProperty(key,out var field)&&field.ValueKind==JsonValueKind.String?field.GetString()??"":"";
    private static bool Owned(JsonElement item,string marker)=>
        item.TryGetProperty("isOrganizer",out var organizer)&&organizer.ValueKind==JsonValueKind.True&&
        item.TryGetProperty("singleValueExtendedProperties",out var properties)&&properties.ValueKind==JsonValueKind.Array&&
        properties.EnumerateArray().Any(p=>Field(p,"id")==MarkerProperty&&Field(p,"value")==marker);

    private static async Task<string?> FindAsync(HttpClient client,string token,string? known,string marker,CancellationToken ct)
    {
        var expand="singleValueExtendedProperties($filter=id eq '"+MarkerProperty+"')";
        if(known is not null){
            using var direct=await SendAsync(client,token,HttpMethod.Get,
                Graph+"/"+Uri.EscapeDataString(known)+"?$select=id,isOrganizer&$expand="+Uri.EscapeDataString(expand),null,ct);
            if(direct.IsSuccessStatusCode){
                using var doc=await JsonDocument.ParseAsync(await direct.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
                if(!Owned(doc.RootElement,marker))throw new SyncFailure("Outlook-Termin gehört nicht eindeutig zu diesem Segment.");
                return Field(doc.RootElement,"id");
            }
            if(direct.StatusCode!=HttpStatusCode.NotFound)throw HttpFailure(direct.StatusCode);
        }
        var filter="singleValueExtendedProperties/Any(ep: ep/id eq '"+MarkerProperty+"' and ep/value eq '"+marker+"')";
        using var response=await SendAsync(client,token,HttpMethod.Get,
            Graph+"?$select=id,isOrganizer&$top=2&$filter="+Uri.EscapeDataString(filter)+"&$expand="+Uri.EscapeDataString(expand),null,ct);
        if(!response.IsSuccessStatusCode)throw HttpFailure(response.StatusCode);
        using var result=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
        var rows=result.RootElement.GetProperty("value").EnumerateArray().ToArray();
        if(rows.Length==0)return null;
        if(rows.Length!=1||result.RootElement.TryGetProperty("@odata.nextLink",out _)||!Owned(rows[0],marker))
            throw new SyncFailure("Mehrere oder nicht eindeutig zugeordnete Outlook-Termine gefunden.");
        var id=Field(rows[0],"id");
        if(id.Length==0)throw new SyncFailure("Outlook-Termin-ID fehlt.");
        return id;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client,string token,HttpMethod method,string url,
        object? body,CancellationToken ct)
    {
        using var req=new HttpRequestMessage(method,url);
        req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        req.Headers.TryAddWithoutValidation("Prefer","IdType=\"ImmutableId\"");
        if(body is not null)req.Content=JsonContent.Create(body);
        return await client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);
    }
    private sealed class SyncFailure(string message):Exception(message);
    private static SyncFailure HttpFailure(HttpStatusCode status)=>new(status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        ?"Outlook-Schreibzugriff fehlt. Microsoft 365 erneut verbinden."
        :"Outlook-Synchronisierung fehlgeschlagen (HTTP "+(int)status+").");
}
