using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

namespace Plenaro.Api.Znuny;

/// <summary>Session-authenticated, strictly read-only GenericInterface requests.
/// Search uses Classic GET /Ticket by default; installations can select POST
/// and route /Ticket/Search. All search windows fail CLOSED on truncation.</summary>
public sealed class ZnunyReader(IHttpClientFactory factory)
{
    public sealed record Ticket(string Id, string Number, string Title,
        string Queue, string State, string StateType, int? OwnerId,
        int? ResponsibleId, DateTimeOffset? Created, DateTimeOffset? Changed);
    public sealed class RemoteFailure(string code,string? remoteCode=null) : Exception(code)
    {
        public string Code => Message;
        // Only a short, validated Znuny machine-readable error code, never
        // remote message/body which may contain customer data or secrets.
        public string? RemoteCode {get;}=remoteCode is {Length:>0 and <=100} value &&
            value.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '_' or '-')?
            value:null;
        public bool DefinitivelyRejected => Code is
            "remote-rejected" or "route-not-found" or "authentication" or "rate-limited";
    }

    private static readonly TimeZoneInfo ServerZone =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private static string FormatRemote(DateTimeOffset time) =>
        TimeZoneInfo.ConvertTime(time, ServerZone).ToString("yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseRemote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var offset) &&
            (value.EndsWith('Z') || value.Contains('+') || value.Contains('T')))
            return offset.ToUniversalTime();
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var local)) return null;
        if (ServerZone.IsInvalidTime(local)) return null;
        var offsetValue=ServerZone.GetUtcOffset(local);
        return new DateTimeOffset(local,offsetValue).ToUniversalTime();
    }

    public async Task<string> SessionAsync(ServerSettings settings,
        string username,string password,Counter counter,CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            settings.ApiUrl.TrimEnd('/') + "/Session");
        request.Content = new StringContent(JsonSerializer.Serialize(
            new { UserLogin=username,Password=password }),Encoding.UTF8,"application/json");
        using var json = await SendAsync(request,counter,ct);
        var root=json.RootElement;
        if (root.TryGetProperty("SessionID",out var element) &&
            element.ValueKind==JsonValueKind.String &&
            element.GetString() is { Length: > 0 } id) return id;
        throw new RemoteFailure("session-rejected");
    }

    public async Task<IReadOnlyList<string>> SearchAsync(
        ServerSettings settings,string session,string mode,
        DateTimeOffset? after,DateTimeOffset? before,
        Counter counter,CancellationToken ct)
    {
        var values = new Dictionary<string,string>{
            ["SessionID"]=session,
            ["Limit"]=settings.SearchLimit.ToString(CultureInfo.InvariantCulture)
        };
        if (mode=="created")
        {
            if(after is { } start)values["TicketCreateTimeNewerDate"]=FormatRemote(start);
            if(before is { } end)values["TicketCreateTimeOlderDate"]=FormatRemote(end);
        }
        else if (mode=="changed")
        {
            if(after is { } start)values["TicketLastChangeTimeNewerDate"]=FormatRemote(start);
            if(before is { } end)values["TicketLastChangeTimeOlderDate"]=FormatRemote(end);
        }
        else if (mode=="open")
        {
            values["StateType"]="Open";
            if(after is { } start)values["TicketCreateTimeNewerDate"]=FormatRemote(start);
            if(before is { } end)values["TicketCreateTimeOlderDate"]=FormatRemote(end);
        }
        else throw new ArgumentOutOfRangeException(nameof(mode));

        var baseUrl=settings.ApiUrl.TrimEnd('/')+settings.TicketSearchRoute;
        HttpRequestMessage request;
        if(settings.TicketSearchMethod=="GET")
        {
            var qs=string.Join("&",values.Select(p =>
                Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));
            request=new HttpRequestMessage(HttpMethod.Get,baseUrl+"?"+qs);
        }
        else
        {
            request=new HttpRequestMessage(HttpMethod.Post,baseUrl);
            request.Content=new StringContent(JsonSerializer.Serialize(values),Encoding.UTF8,
                "application/json");
        }
        using(request)
        using(var json=await SendAsync(request,counter,ct))
        {
            var root=json.RootElement;
            if(root.TryGetProperty("TicketID",out var ids) ||
                root.TryGetProperty("TicketIDs",out ids))
            {
                if(ids.ValueKind==JsonValueKind.Null) return [];
                if(ids.ValueKind!=JsonValueKind.Array) throw new RemoteFailure("invalid-search");
                return ids.EnumerateArray().Select(x=>x.ToString())
                    .Where(x=>x.Length is > 0 and <= 80 && x.All(char.IsAsciiDigit))
                    .Distinct(StringComparer.Ordinal).ToArray();
            }
            // Znuny may return {} for zero results.
            if(root.EnumerateObject().Any())
                throw new RemoteFailure("unsupported-search-response");
            return [];
        }
    }

    public async Task<Ticket> GetAsync(ServerSettings settings,string session,
        string ticketId,Counter counter,CancellationToken ct)
    {
        var route=settings.TicketGetRouteTemplate.Replace("{TicketID}",Uri.EscapeDataString(ticketId));
        var baseUrl=settings.ApiUrl.TrimEnd('/')+route;
        HttpRequestMessage request;
        var options=new Dictionary<string,string>{
            ["SessionID"]=session,["AllArticles"]="0",["DynamicFields"]="0"
        };
        if (settings.TicketGetMethod=="GET")
        {
            request=new HttpRequestMessage(HttpMethod.Get,baseUrl+"?"+
                string.Join("&",options.Select(p=>Uri.EscapeDataString(p.Key)+"="+
                    Uri.EscapeDataString(p.Value))));
        }
        else
        {
            request=new HttpRequestMessage(HttpMethod.Post,baseUrl);
            request.Content=new StringContent(JsonSerializer.Serialize(options),
                Encoding.UTF8,"application/json");
        }
        using(request)
        using(var doc=await SendAsync(request,counter,ct))
        {
            var root=doc.RootElement;
            if(!root.TryGetProperty("Ticket",out var ticket)) throw new RemoteFailure("missing-ticket");
            if(ticket.ValueKind==JsonValueKind.Array)
            {
                if(ticket.GetArrayLength()!=1) throw new RemoteFailure("invalid-ticket");
                ticket=ticket[0];
            }
            if(ticket.ValueKind!=JsonValueKind.Object)throw new RemoteFailure("invalid-ticket");
            var id=String(ticket,"TicketID",80);
            if(id!=ticketId)throw new RemoteFailure("ticket-id-mismatch");
            return new Ticket(id,String(ticket,"TicketNumber",100),
                String(ticket,"Title",500),String(ticket,"Queue",200),
                String(ticket,"State",100),String(ticket,"StateType",100),
                Int(ticket,"OwnerID"),Int(ticket,"ResponsibleID"),
                ParseRemote(String(ticket,"Created",64)),
                ParseRemote(String(ticket,"Changed",64)));
        }
    }


    // Read full ticket articles only after the API endpoint has performed the
    // approved-agent/owner authorization check. No HTTP body is logged.
    public sealed record Attachment(string Id,string Filename,string ContentType,
        string ContentId,long Size,string Disposition);
    public sealed record AttachmentFile(byte[] Bytes,string Filename,string ContentType);
    public sealed record Article(string Id,string Subject,string Body,string From,
        string To,string ReplyTo,string SenderType,string Channel,string Created,
        string ContentType,IReadOnlyList<Attachment> Attachments);
    public sealed record Detail(string TicketId,string TicketNumber,string Title,
        IReadOnlyList<Article> Articles,string CustomerUserId = "");
    public sealed record Agent(int Id,string Name);

    public async Task<Detail> GetArticlesAsync(ServerSettings settings,string session,
        string ticketId,Counter counter,CancellationToken ct)
    {
        var route=settings.TicketGetRouteTemplate.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var values=new Dictionary<string,string>{
            ["SessionID"]=session,["AllArticles"]="1",["DynamicFields"]="1",
            ["ArticleLimit"]=settings.ArticleLimit.ToString(CultureInfo.InvariantCulture),
            ["Attachments"]="1",["GetAttachmentContents"]="0"
        };
        using var request=JsonRequest(settings.ApiUrl.TrimEnd('/')+route,
            settings.TicketGetMethod,values);
        using var doc=await SendAsync(request,counter,ct);
        var root=doc.RootElement;
        if(!root.TryGetProperty("Ticket",out var ticket))
            throw new RemoteFailure("missing-ticket");
        if(ticket.ValueKind==JsonValueKind.Array)
        {
            if(ticket.GetArrayLength()!=1)throw new RemoteFailure("invalid-ticket");
            ticket=ticket[0];
        }
        if(ticket.ValueKind!=JsonValueKind.Object||
            String(ticket,"TicketID",80)!=ticketId)
            throw new RemoteFailure("ticket-id-mismatch");
        var articles=new List<Article>();
        if(!ticket.TryGetProperty("Article",out var data) &&
            !ticket.TryGetProperty("Articles",out data))
        {
            root.TryGetProperty("Article",out data);
        }
        if(data.ValueKind==JsonValueKind.Array)
        {
            foreach(var row in data.EnumerateArray())AddArticle(row);
        }
        else if(data.ValueKind==JsonValueKind.Object) AddArticle(data);
        void AddArticle(JsonElement row)
        {
            if(row.ValueKind!=JsonValueKind.Object ||
                articles.Count>=500)return;
            // Preserve original MIME body and HTML formatting. Previously a
            // tag-stripping regex collapsed table layouts, quotes and whitespace.
            // The browser now displays HTML only in a CSP-restricted sandboxed
            // iframe; plain text is rendered escaped with preserved line breaks.
            var body=String(row,"Body",120000);
            var contentType=String(row,"ContentType",160);
            if(string.IsNullOrWhiteSpace(contentType))
                contentType=String(row,"MimeType",120);
            // Znuny's GenericInterface sometimes exposes SenderTypeID and
            // ArticleType instead of their human-readable counterparts.
            var sender=String(row,"SenderType",80);
            if((sender=="3"||string.IsNullOrWhiteSpace(sender)) &&
                (sender=="3"||String(row,"SenderTypeID",10)=="3"))
                sender="customer";
            var channel=String(row,"CommunicationChannel",100);
            if(string.IsNullOrWhiteSpace(channel))
                channel=String(row,"ArticleType",100);
            if(string.IsNullOrWhiteSpace(channel))
                channel=String(row,"ArticleTypeID",16);
            articles.Add(new Article(String(row,"ArticleID",80),
                String(row,"Subject",500),body,
                String(row,"From",320),String(row,"To",320),
                String(row,"ReplyTo",320),sender,channel,
                String(row,"CreateTime",80),contentType,
                AttachmentIndex(row)));
        }
        return new Detail(ticketId,String(ticket,"TicketNumber",100),
            String(ticket,"Title",500),
            articles.OrderBy(a=>a.Created,StringComparer.Ordinal)
                .TakeLast(settings.ArticleLimit).ToArray(),
            string.IsNullOrWhiteSpace(String(ticket,"CustomerUserID",320))
                ?String(ticket,"CustomerUser",320):String(ticket,"CustomerUserID",320));
    }

    // Znuny TicketGet returns attachment metadata without binary content when
    // GetAttachmentContents=0. Do not expose Base64 files in the article list.
    private static IReadOnlyList<Attachment> AttachmentIndex(JsonElement article)
    {
        if(!article.TryGetProperty("Attachment",out var value) &&
            !article.TryGetProperty("Attachments",out value))return [];
        var result=new List<Attachment>();
        void Add(JsonElement item)
        {
            if(item.ValueKind!=JsonValueKind.Object || result.Count>=50)return;
            var id=String(item,"FileID",30);
            if(id.Length==0 || !id.All(char.IsAsciiDigit))return;
            var size=long.TryParse(String(item,"FilesizeRaw",30),
                NumberStyles.None,CultureInfo.InvariantCulture,out var n) && n>=0?n:0;
            result.Add(new Attachment(id,String(item,"Filename",200),
                String(item,"ContentType",140),String(item,"ContentID",320),
                size,String(item,"Disposition",24)));
        }
        if(value.ValueKind==JsonValueKind.Array)
            foreach(var item in value.EnumerateArray())Add(item);
        else Add(value);
        return result;
    }

    // Fetch an individual file on demand. TicketGet requires fetching article
    // attachments, so bound the entire response and the decoded file strictly.
    // This is called only after the endpoint has rechecked local ticket access.
    public async Task<AttachmentFile?> GetAttachmentAsync(ServerSettings settings,
        string session,string ticketId,string articleId,string fileId,
        Counter counter,CancellationToken ct)
    {
        var route=settings.TicketGetRouteTemplate.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var values=new Dictionary<string,string>{
            ["SessionID"]=session,["AllArticles"]="1",["Attachments"]="1",
            ["GetAttachmentContents"]="1",
            ["ArticleLimit"]=settings.ArticleLimit.ToString(CultureInfo.InvariantCulture)
        };
        using var request=JsonRequest(settings.ApiUrl.TrimEnd('/')+route,
            settings.TicketGetMethod,values);
        using var doc=await SendAsync(request,counter,ct,8*1024*1024);
        var root=doc.RootElement;
        if(!root.TryGetProperty("Ticket",out var ticket))return null;
        if(ticket.ValueKind==JsonValueKind.Array)
        {
            if(ticket.GetArrayLength()!=1)return null;
            ticket=ticket[0];
        }
        if(ticket.ValueKind!=JsonValueKind.Object ||
            String(ticket,"TicketID",80)!=ticketId)return null;
        if(!ticket.TryGetProperty("Article",out var articles) &&
            !ticket.TryGetProperty("Articles",out articles))
        {
            root.TryGetProperty("Article",out articles);
        }
        AttachmentFile? FindInArticle(JsonElement article)
        {
            if(article.ValueKind!=JsonValueKind.Object ||
                String(article,"ArticleID",80)!=articleId)return null;
            if(!article.TryGetProperty("Attachment",out var attachments) &&
                !article.TryGetProperty("Attachments",out attachments))return null;
            AttachmentFile? Decode(JsonElement item)
            {
                if(item.ValueKind!=JsonValueKind.Object ||
                    String(item,"FileID",30)!=fileId ||
                    !item.TryGetProperty("Content",out var raw) ||
                    raw.ValueKind!=JsonValueKind.String)return null;
                var encoded=raw.GetString();
                if(string.IsNullOrEmpty(encoded) || encoded.Length>7*1024*1024)
                    throw new RemoteFailure("attachment-too-large");
                byte[] bytes;
                try {bytes=Convert.FromBase64String(encoded);}
                catch(FormatException){throw new RemoteFailure("invalid-attachment");}
                if(bytes.Length>5*1024*1024)
                    throw new RemoteFailure("attachment-too-large");
                var filename=String(item,"Filename",180);
                filename=new string(filename.Where(c=>!char.IsControl(c) &&
                    c!='/' && c!='\\' && c!=':' ).ToArray()).Trim();
                if(filename is "" or "." or "..")filename="znuny-anhang-"+fileId;
                return new AttachmentFile(bytes,filename,String(item,"ContentType",140));
            }
            if(attachments.ValueKind==JsonValueKind.Array)
            {
                foreach(var item in attachments.EnumerateArray())
                    if(Decode(item) is {} file)return file;
            }
            else return Decode(attachments);
            return null;
        }
        if(articles.ValueKind==JsonValueKind.Array)
        {
            foreach(var article in articles.EnumerateArray())
                if(FindInArticle(article) is {} file)return file;
        }
        else return FindInArticle(articles);
        return null;
    }

    public sealed record FieldOption(string Key,string DisplayText);
    public sealed record BookingFields(string CostCenter,string Order,
        IReadOnlyList<FieldOption> CostCenterOptions,
        IReadOnlyList<FieldOption> OrderOptions);

    // Mirrors the WPF picker: current DynamicField values from TicketGet and
    // Key/Value options from the administrator-configured options endpoint.
    // Configured fallback options use the same "key=label;..." syntax as WPF.
    public async Task<BookingFields> GetBookingFieldsAsync(ServerSettings settings,
        string session,string ticketId,Counter counter,CancellationToken ct)
    {
        var route=settings.TicketGetRouteTemplate.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var values=new Dictionary<string,string>{
            ["SessionID"]=session,["AllArticles"]="0",["DynamicFields"]="1"
        };
        using var request=JsonRequest(settings.ApiUrl.TrimEnd('/')+route,
            settings.TicketGetMethod,values);
        using var doc=await SendAsync(request,counter,ct);
        var root=doc.RootElement;
        if(!root.TryGetProperty("Ticket",out var ticket))
            throw new RemoteFailure("missing-ticket");
        if(ticket.ValueKind==JsonValueKind.Array){
            if(ticket.GetArrayLength()!=1)throw new RemoteFailure("invalid-ticket");
            ticket=ticket[0];
        }
        if(ticket.ValueKind!=JsonValueKind.Object ||
            String(ticket,"TicketID",80)!=ticketId)
            throw new RemoteFailure("ticket-id-mismatch");
        var cost=FindDynamic(ticket,settings.CostCenterFieldName);
        var order=FindDynamic(ticket,settings.OrderFieldName);
        var costOptions=await OptionsAsync(settings,session,settings.CostCenterFieldName,
            settings.CostCenterOptions,counter,ct);
        var orderOptions=await OptionsAsync(settings,session,settings.OrderFieldName,
            settings.OrderOptions,counter,ct);
        return new BookingFields(cost,order,costOptions,orderOptions);
    }

    private static string FindDynamic(JsonElement ticket,string fieldName)
    {
        if(string.IsNullOrWhiteSpace(fieldName))return "";
        if(!ticket.TryGetProperty("DynamicField",out var fields) &&
           !ticket.TryGetProperty("DynamicFields",out fields))return "";
        if(fields.ValueKind==JsonValueKind.Array){
            foreach(var row in fields.EnumerateArray())
                if(row.ValueKind==JsonValueKind.Object &&
                    String(row,"Name",200).Equals(fieldName,StringComparison.OrdinalIgnoreCase))
                    return String(row,"Value",200);
        }else if(fields.ValueKind==JsonValueKind.Object){
            if(fields.TryGetProperty(fieldName,out var value))return value.ToString()[..Math.Min(200,value.ToString().Length)];
            if(String(fields,"Name",200).Equals(fieldName,StringComparison.OrdinalIgnoreCase))
                return String(fields,"Value",200);
        }
        return "";
    }

    private async Task<IReadOnlyList<FieldOption>> OptionsAsync(ServerSettings settings,
        string session,string name,string fallback,Counter counter,CancellationToken ct)
    {
        var options=new List<FieldOption>();
        if(!string.IsNullOrWhiteSpace(name)){
            var route=settings.DynamicFieldOptionsRoute.Replace("{FieldName}",
                Uri.EscapeDataString(name),StringComparison.Ordinal);
            try{
                using var req=JsonRequest(settings.ApiUrl.TrimEnd('/')+route,"GET",
                    new Dictionary<string,string>{["SessionID"]=session});
                using var doc=await SendAsync(req,counter,ct);
                var root=doc.RootElement;
                if(root.TryGetProperty("Data",out var data)&&
                    data.ValueKind==JsonValueKind.Object)root=data;
                if(root.TryGetProperty("Field",out var field)&&
                    field.ValueKind==JsonValueKind.Object &&
                    field.TryGetProperty("Options",out var items)&&
                    items.ValueKind==JsonValueKind.Array)
                    foreach(var item in items.EnumerateArray().Take(2000))
                        if(item.ValueKind==JsonValueKind.Object){
                            var key=String(item,"Key",200);
                            if(!string.IsNullOrWhiteSpace(key))
                                options.Add(new FieldOption(key,String(item,"Value",240)));
                        }
            }
            catch(RemoteFailure e) when(e.Code is "server-error" or
                "route-not-found" or "remote-rejected" or "protocol-error" or
                "remote-protocol-error" or "unsupported-options" or "invalid-json"){
                // DynamicField options are an optional custom extension, NOT
                // part of every GenericTicketConnectorREST deployment. A 404
                // or unsupported endpoint must not disable time bookings.
                // Use only the admin-supplied fallback keys below.
                // Custom Znuny connectors may not expose this route. Use only
                // administrator-configured options, never invented values.
            }
        }
        if(options.Count==0)
            foreach(var entry in (fallback??"").Split(';',
                StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){
                var parts=entry.Split('=',2,StringSplitOptions.TrimEntries);
                if(parts[0].Length>0 && parts[0].Length<=200)
                    options.Add(new FieldOption(parts[0],
                        parts.Length>1&&parts[1].Length>0?parts[1]:parts[0]));
            }
        return options.GroupBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)
            .Select(x=>x.First()).Take(1000).ToArray();
    }

    public async Task<IReadOnlyList<Agent>> GetAgentsAsync(ServerSettings settings,
        string session,Counter counter,CancellationToken ct)
    {
        using var request=JsonRequest(settings.ApiUrl.TrimEnd('/')+settings.AgentListRoute,
            "GET",new Dictionary<string,string>{["SessionID"]=session});
        using var doc=await SendAsync(request,counter,ct);
        var root=doc.RootElement;
        JsonElement list=default;
        foreach(var key in new[]{"Agents","Agent","Users","User"})
            if(root.TryGetProperty(key,out list))break;
        if(list.ValueKind==JsonValueKind.Undefined &&
            root.TryGetProperty("Data",out var nested) &&
            nested.ValueKind==JsonValueKind.Object)
            nested.TryGetProperty("Agents",out list);
        if(list.ValueKind is not (JsonValueKind.Array or JsonValueKind.Object))
            throw new RemoteFailure("unsupported-agent-list");
        var agents=new List<Agent>();
        if(list.ValueKind==JsonValueKind.Array)
        {
            foreach(var a in list.EnumerateArray())
                if(a.ValueKind==JsonValueKind.Object){
                    var id=Int(a,"UserID")??Int(a,"AgentID");
                    if(id is >0)agents.Add(new Agent(id.Value,
                        new[]{String(a,"Name",256),
                            String(a,"FirstName",128)+" "+String(a,"LastName",128),
                            String(a,"Login",256),String(a,"UserLogin",256)}
                            .FirstOrDefault(name=>!string.IsNullOrWhiteSpace(name))?.Trim()
                            ?? "Agent #"+id));
                }
        }
        else if(list.ValueKind==JsonValueKind.Object)
        {
            foreach(var p in list.EnumerateObject()){
                if(int.TryParse(p.Name,out var id)&&id>0)
                    agents.Add(new Agent(id,p.Value.ToString()[..Math.Min(256,p.Value.ToString().Length)]));
            }
        }
        return agents.GroupBy(a=>a.Id).Select(a=>a.First())
            .OrderBy(a=>a.Name,StringComparer.OrdinalIgnoreCase).Take(1000).ToArray();
    }

    public async Task<bool> UpdateAsync(ServerSettings settings,string session,
        string ticketId,Dictionary<string,object> contents,Counter counter,CancellationToken ct)
    {
        // WPF used POST /Ticket/{TicketID}/Update; modern Znuny may map PATCH
        // /Ticket/{TicketID}. Both use identical JSON bodies, selected by admin.
        // No automatic retries; an interrupted write has an uncertain outcome.
        var route=settings.TicketUpdateRoute.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var body=new Dictionary<string,object>(contents){
            ["SessionID"]=session,["TicketID"]=ticketId
        };
        using var request=new HttpRequestMessage(
            settings.TicketUpdateMethod=="PATCH"?HttpMethod.Patch:HttpMethod.Post,
            settings.ApiUrl.TrimEnd('/')+route){
            Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,
                "application/json")
        };
        using var doc=await SendAsync(request,counter,ct);
        var root=doc.RootElement;
        // Explicit Success=0 is a deterministic remote rejection even if
        // the server omits an Error object.
        if(root.TryGetProperty("Success",out _) && !ConfirmedUpdate(root,ticketId))
            throw new RemoteFailure("remote-rejected");
        // Znuny GenericInterface v7.x can return {"Success":1} without
        // TicketID or ArticleID; Classic custom connectors often use IDs.
        // Both are valid explicit ACKs. Never infer success from HTTP 200
        // alone or an empty JSON object.
        return ConfirmedUpdate(root,ticketId);
    }

    public static bool ConfirmedUpdate(JsonElement root,string ticketId)
    {
        if(root.ValueKind!=JsonValueKind.Object)return false;
        if(root.TryGetProperty("Error",out var err) &&
            err.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))return false;
        if(root.TryGetProperty("Success",out var success))
            return success.ValueKind switch {
                JsonValueKind.True=>true,
                JsonValueKind.Number=>success.TryGetInt32(out var i)&&i==1,
                JsonValueKind.String=>success.GetString() is "1" or "true",
                _=>false
            };
        if(root.TryGetProperty("Data",out var data) &&
            data.ValueKind==JsonValueKind.Object)
            return ConfirmedUpdate(data,ticketId);
        if(root.TryGetProperty("TicketID",out var id))
            return id.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
                string.Equals(id.ToString(),ticketId,StringComparison.Ordinal);
        return root.TryGetProperty("ArticleID",out var article) &&
            article.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
            long.TryParse(article.ToString(),out var articleId) && articleId>0;
    }

    private static HttpRequestMessage JsonRequest(string url,string method,
        Dictionary<string,string> values)
    {
        if(method=="POST")
            return new HttpRequestMessage(HttpMethod.Post,url){
                Content=new StringContent(JsonSerializer.Serialize(values),Encoding.UTF8,
                    "application/json")
            };
        return new HttpRequestMessage(HttpMethod.Get,url+"?"+
            string.Join("&",values.Select(pair=>
                Uri.EscapeDataString(pair.Key)+"="+Uri.EscapeDataString(pair.Value))));
    }

    public async Task<IReadOnlyList<string>> SearchWindowAsync(
        ServerSettings settings,string session,string mode,DateTimeOffset start,
        DateTimeOffset end,Counter counter,CancellationToken ct)
    {
        if(end<=start)return [];
        var all=new HashSet<string>(StringComparer.Ordinal);
        await SearchPartitionAsync(start,end,0);
        return all.ToArray();

        async Task SearchPartitionAsync(DateTimeOffset from,DateTimeOffset to,int level)
        {
            if(level>25)throw new RemoteFailure("window-too-dense");
            var ids=await SearchAsync(settings,session,mode,from,to,counter,ct);
            if(ids.Count<settings.SearchLimit)
            {
                all.UnionWith(ids);
                return;
            }
            if((to-from)<TimeSpan.FromMinutes(2))
                throw new RemoteFailure("search-limit-exceeded");
            var mid=from+TimeSpan.FromTicks((to-from).Ticks/2);
            // Inclusive date bounds overlap; de-duplicate by TicketID.
            await SearchPartitionAsync(from,mid,level+1);
            await SearchPartitionAsync(mid,to,level+1);
        }
    }

    private async Task<JsonDocument> SendAsync(
        HttpRequestMessage request,Counter counter,CancellationToken ct,
        int maxResponseBytes=524288)
    {
        if(!counter.Take())throw new RemoteFailure("request-budget-exhausted");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var token=timeout.Token;
        try
        {
            using var response=await factory.CreateClient("Plenaro.Znuny")
                .SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if(!response.IsSuccessStatusCode)
            {
                var status=response.StatusCode;
                var code=status switch {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden=>"authentication",
                    HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed=>"route-not-found",
                    HttpStatusCode.TooManyRequests=>"rate-limited",
                    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity=>"remote-rejected",
                    _=>"server-error"
                };
                // HTTP 4xx is definitive rejection. A remote 5xx may have
                // committed an update: do NOT retry it automatically.
                string? remoteCode=null;
                if((int)status>=400 && (int)status<500){
                    var length=Math.Min(response.Content.Headers.ContentLength??4096,4096);
                    if(length>0){
                        using var bounded=CancellationTokenSource.CreateLinkedTokenSource(token);
                        try{
                            var raw=await response.Content.ReadAsStringAsync(bounded.Token);
                            if(raw.Length<=4096){
                                using var errorDoc=JsonDocument.Parse(raw);
                                remoteCode=ErrorCode(errorDoc.RootElement);
                            }
                        }catch(JsonException){}catch(OperationCanceledException) when(!ct.IsCancellationRequested){}
                    }
                }
                throw new RemoteFailure(code,remoteCode);
            }
            if(response.Content.Headers.ContentLength>maxResponseBytes)
                throw new RemoteFailure("response-too-large");
            await using var source=await response.Content.ReadAsStreamAsync(token);
            using var buffer=new MemoryStream();
            var chunk=new byte[8192];
            while(true)
            {
                var count=await source.ReadAsync(chunk,token);
                if(count==0)break;
                if(buffer.Length+count>maxResponseBytes)throw new RemoteFailure("response-too-large");
                buffer.Write(chunk,0,count);
            }
            buffer.Position=0;
            var doc=await JsonDocument.ParseAsync(buffer,cancellationToken:token);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)
            { doc.Dispose();throw new RemoteFailure("protocol-error"); }
            var reportedCode=ErrorCode(doc.RootElement);
            if(reportedCode is not null ||
                (doc.RootElement.TryGetProperty("Error",out var apiError) &&
                 apiError.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)))
            { doc.Dispose();throw new RemoteFailure("remote-rejected",reportedCode); }
            return doc;
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {throw new RemoteFailure("timeout");}
        catch(HttpRequestException){throw new RemoteFailure("network-error");}
        catch(JsonException){throw new RemoteFailure("invalid-json");}
    }
    private static string? ErrorCode(JsonElement root)
    {
        if(root.ValueKind!=JsonValueKind.Object)return null;
        if(root.TryGetProperty("Error",out var nested) &&
           nested.ValueKind==JsonValueKind.Object)
            root=nested;
        if(root.TryGetProperty("ErrorCode",out var code)&&
            code.ValueKind==JsonValueKind.String)
            return code.GetString();
        return null;
    }
    private static string String(JsonElement root,string key,int max)
    {
        if(!root.TryGetProperty(key,out var value))return "";
        var s=value.ToString();
        return s.Length<=max?s:s[..max];
    }
    private static int? Int(JsonElement root,string key)
    {
        var s=String(root,key,20);
        return int.TryParse(s,out var i) && i>0?i:null;
    }
    public sealed class Counter(int budget)
    {
        public int Calls{get;private set;}
        public bool Take()
        {
            if(Calls>=budget)return false;
            Calls++;return true;
        }
    }
}
