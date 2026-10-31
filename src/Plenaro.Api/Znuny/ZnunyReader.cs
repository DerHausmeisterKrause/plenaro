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
    public sealed class RemoteFailure(string code) : Exception(code)
    { public string Code => Message; }

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
    public sealed record Article(string Id,string Subject,string Body,string From,
        string To,string ReplyTo,string SenderType,string Channel,string Created);
    public sealed record Detail(string TicketId,string TicketNumber,string Title,
        IReadOnlyList<Article> Articles);
    public sealed record Agent(int Id,string Name);

    public async Task<Detail> GetArticlesAsync(ServerSettings settings,string session,
        string ticketId,Counter counter,CancellationToken ct)
    {
        var route=settings.TicketGetRouteTemplate.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var values=new Dictionary<string,string>{
            ["SessionID"]=session,["AllArticles"]="1",["DynamicFields"]="1",
            ["ArticleLimit"]=settings.ArticleLimit.ToString(CultureInfo.InvariantCulture)
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
            var body=String(row,"Body",24000);
            var mime=String(row,"ContentType",120);
            if(mime.Contains("html",StringComparison.OrdinalIgnoreCase)||
                body.Contains("<p",StringComparison.OrdinalIgnoreCase))
            {
                body=System.Text.RegularExpressions.Regex.Replace(body,
                    "<(br|/p|/div|/li)[^>]*>","\\n",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                body=System.Text.RegularExpressions.Regex.Replace(body,"<[^>]+>","");
                body=WebUtility.HtmlDecode(body);
            }
            articles.Add(new Article(String(row,"ArticleID",80),
                String(row,"Subject",500),body,
                String(row,"From",320),String(row,"To",320),
                String(row,"ReplyTo",320),String(row,"SenderType",80),
                String(row,"CommunicationChannel",100),
                String(row,"CreateTime",80)));
        }
        return new Detail(ticketId,String(ticket,"TicketNumber",100),
            String(ticket,"Title",500),
            articles.OrderBy(a=>a.Created,StringComparer.Ordinal)
                .TakeLast(settings.ArticleLimit).ToArray());
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
        // WPF used POST /Ticket/{TicketID}/Update with Ticket and/or Article.
        // No automatic retries; an interrupted write has an uncertain outcome.
        var route=settings.TicketUpdateRoute.Replace("{TicketID}",
            Uri.EscapeDataString(ticketId),StringComparison.Ordinal);
        var body=new Dictionary<string,object>(contents){
            ["SessionID"]=session,["TicketID"]=ticketId
        };
        using var request=new HttpRequestMessage(HttpMethod.Post,
            settings.ApiUrl.TrimEnd('/')+route){
            Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,
                "application/json")
        };
        using var doc=await SendAsync(request,counter,ct);
        var root=doc.RootElement;
        // GenericInterface generally returns TicketID (and optionally ArticleID).
        // Some custom implementations only return {} on success; reject these
        // unconfirmable writes rather than presenting false success.
        return root.TryGetProperty("TicketID",out var id) &&
            id.ToString()==ticketId ||
            root.TryGetProperty("ArticleID",out var article) &&
            article.ValueKind is JsonValueKind.String or JsonValueKind.Number &&
            article.ToString().Length>0;
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
        HttpRequestMessage request,Counter counter,CancellationToken ct)
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
                throw new RemoteFailure(response.StatusCode is HttpStatusCode.Unauthorized or
                    HttpStatusCode.Forbidden ? "authentication" : "server-error");
            if(response.Content.Headers.ContentLength is > 524288)
                throw new RemoteFailure("response-too-large");
            await using var source=await response.Content.ReadAsStreamAsync(token);
            using var buffer=new MemoryStream();
            var chunk=new byte[8192];
            while(true)
            {
                var count=await source.ReadAsync(chunk,token);
                if(count==0)break;
                if(buffer.Length+count>524288)throw new RemoteFailure("response-too-large");
                buffer.Write(chunk,0,count);
            }
            buffer.Position=0;
            var doc=await JsonDocument.ParseAsync(buffer,cancellationToken:token);
            if(doc.RootElement.ValueKind!=JsonValueKind.Object)
            { doc.Dispose();throw new RemoteFailure("protocol-error"); }
            if(doc.RootElement.TryGetProperty("Error",out _))
            { doc.Dispose();throw new RemoteFailure("remote-protocol-error"); }
            return doc;
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested)
        {throw new RemoteFailure("timeout");}
        catch(HttpRequestException){throw new RemoteFailure("network-error");}
        catch(JsonException){throw new RemoteFailure("invalid-json");}
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
