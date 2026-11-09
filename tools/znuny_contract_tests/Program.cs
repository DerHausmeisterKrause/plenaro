using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Plenaro.Api.Znuny;
using Plenaro.Api.Favorites;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

// Favoriten-Wildcard ist zunächst nur eine reservierte, niemals
// als Plenaro-Login-Host verwendbare Origin.
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "wiki.apps.example.org",null,"https://portal.example.org",
    "https://tickets.example.org"),"wildcard guard is opt-in");
Assert(FavoriteHostGuard.IsUnassignedAlias(
    "wiki.apps.example.org","apps.example.org","https://portal.example.org",
    "https://tickets.example.org"),"unknown app alias is denied");
Assert(FavoriteHostGuard.IsUnassignedAlias(
    "nested.wiki.apps.example.org","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "nested aliases cannot expose Plenaro UI");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "portal.apps.example.org","apps.example.org",
    "https://portal.apps.example.org","https://tickets.apps.example.org"),
    "main application host is unchanged");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "tickets.apps.example.org","apps.example.org",
    "https://portal.apps.example.org","https://tickets.apps.example.org"),
    "native Znuny proxy host is unchanged");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "127.0.0.1","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "IP-based health check unaffected");
Assert(!FavoriteHostGuard.IsUnassignedAlias(
    "unrelated.example.net","apps.example.org",
    "https://portal.example.org","https://tickets.example.org"),
    "other domains unaffected by optional guard");
var invalidFavoritesDomainRejected=false;
try{
    FavoriteHostGuard.IsUnassignedAlias("wiki.apps.example.org",
        "*.apps.example.org","https://portal.example.org",
        "https://tickets.example.org");
}catch(InvalidOperationException){invalidFavoritesDomainRejected=true;}
Assert(invalidFavoritesDomainRejected,"invalid base domains fail closed");

// The new wildcard app router is opt-in and never relaxes the
// established same-origin CSRF gate for the main Plenaro application.
var favoriteConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
        ["PLENARO_FAVORITES_BASE_DOMAIN"]="apps.example.org",
        ["Security:RequireHttps"]="true"
    }).Build();
var favoriteProxy=new FavoriteProxy(favoriteConfig);
Assert(favoriteProxy.BaseConfigured,"favorite wildcard app router configured");
var requestContext=new Microsoft.AspNetCore.Http.DefaultHttpContext();
requestContext.Request.Scheme="https";
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.org");
requestContext.Request.Headers["Origin"]="https://portal.example.org";
requestContext.Request.Headers["Sec-Fetch-Site"]="same-origin";
Assert(FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start accepts exact HTTPS browser origin");
requestContext.Request.Headers["Origin"]="https://untrusted.example.org";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start blocks cross-origin POST");
requestContext.Request.Headers["Origin"]="https://portal.example.org";
requestContext.Request.Headers["Sec-Fetch-Site"]="cross-site";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start blocks cross-site metadata");
requestContext.Request.Headers["Sec-Fetch-Site"]="same-origin";
requestContext.Request.Scheme="http";
Assert(!FavoriteProxy.IsTrustedStartOrigin(requestContext.Request,favoriteConfig),
    "favorite start rejects HTTP");
requestContext.Request.Scheme="https";
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("wiki.apps.example.org");
Assert(favoriteProxy.IsFavoriteHost(requestContext),
    "approved or unknown app alias stays on isolated app router");
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.org");
Assert(!favoriteProxy.IsFavoriteHost(requestContext),
    "main Plenaro origin cannot be intercepted as favorite");
requestContext.Request.Host=new Microsoft.AspNetCore.Http.HostString("tickets.example.org");
Assert(!favoriteProxy.IsFavoriteHost(requestContext),
    "Znuny native origin remains on existing proxy");

// A token-free probe must positively identify that the complete
// wildcard/Host/HTTPS routing chain reached the FAVORITE proxy (not Plenaro).
var favoriteProbe=new Microsoft.AspNetCore.Http.DefaultHttpContext();
favoriteProbe.Request.Method="GET";
favoriteProbe.Request.Scheme="https";
favoriteProbe.Request.Host=new Microsoft.AspNetCore.Http.HostString(
    "wiki.apps.example.org");
favoriteProbe.Request.Path="/_plenaro/favorite-health";
using var favoriteProbeBody=new MemoryStream();
favoriteProbe.Response.Body=favoriteProbeBody;
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==200 &&
    Encoding.UTF8.GetString(favoriteProbeBody.ToArray())==
        "plenaro-favorite-proxy-ready",
    "favorite wildcard health confirms correct proxy routing without token");
favoriteProbe.Request.Method="POST";
favoriteProbeBody.SetLength(0);
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==405,
    "favorite wildcard health is read-only");
favoriteProbe.Request.Method="GET";
favoriteProbe.Request.Scheme="http";
await favoriteProxy.ServeAsync(favoriteProbe,CancellationToken.None);
Assert(favoriteProbe.Response.StatusCode==404,
    "favorite wildcard health is unavailable via plaintext");


 // The new single-level wildcard aliases coexist with the original
 // slug.base-domain URLs. Classic bookmark IDs, ACLs and sessions stay intact.
 var templateOnly=new ConfigurationBuilder().AddInMemoryCollection(
     new Dictionary<string,string?>{
         ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
         ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
         ["PLENARO_FAVORITES_HOST_TEMPLATE"]="{slug}-plenaro.example.org",
         ["Security:RequireHttps"]="true"
     }).Build();
 var templatedProxy=new FavoriteProxy(templateOnly);
 Assert(templatedProxy.BaseConfigured,
     "single-level template can run without legacy base domain");
 Assert(FavoriteHostNames.PreferredOrigin("wiki",null,
     "{slug}-plenaro.example.org")?.AbsoluteUri=="https://wiki-plenaro.example.org/",
     "new favorites start on one-level TLS wildcard-compatible host");
 Assert(FavoriteHostNames.PreferredOrigin("wiki","apps.example.org",
     null)?.AbsoluteUri=="https://wiki.apps.example.org/",
     "legacy favorites keep original generated URLs without template");
 Assert(FavoriteHostNames.TryGetSlug("wiki-plenaro.example.org",null,
     "{slug}-plenaro.example.org",out var newSlug) && newSlug=="wiki",
     "template alias resolves original favorite slug");
 Assert(FavoriteHostNames.TryGetSlug("wiki.apps.example.org",
     "apps.example.org","{slug}-plenaro.example.org",out var oldSlug) &&
     oldSlug=="wiki","legacy aliases remain routable when template enabled");
 Assert(FavoriteHostGuard.IsUnassignedAlias("wiki-plenaro.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"new aliases isolated from Plenaro login");
 Assert(FavoriteHostGuard.IsUnassignedAlias("a.b-plenaro.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"malformed deep aliases are still isolated");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("portal.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"primary Plenaro host never intercepted");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("tickets.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"Znuny host never intercepted");
 Assert(!FavoriteHostGuard.IsUnassignedAlias("unrelated.example.org",null,
     "https://portal.example.org","https://tickets.example.org",
     "{slug}-plenaro.example.org"),"unrelated hosts remain unchanged");
 foreach(var invalid in new[]{"https://{slug}-plenaro.example.org",
     "*.{slug}.example.org","{slug}.example.org/{slug}",
     "{slug}-plenaro.example.org:443"})
 {
     var rejected=false;
     try{FavoriteHostNames.PreferredOrigin("wiki",null,invalid);}
     catch(InvalidOperationException){rejected=true;}
     Assert(rejected,"invalid hostname templates are rejected");
 }
 var templatedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 templatedRequest.Request.Method="GET";
 templatedRequest.Request.Scheme="https";
 templatedRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "wiki-plenaro.example.org");
 templatedRequest.Request.Path="/_plenaro/favorite-health";
 using(var body=new MemoryStream())
 {
     templatedRequest.Response.Body=body;
     Assert(templatedProxy.IsFavoriteHost(templatedRequest),
         "template alias is handled before identity/static files");
     await templatedProxy.ServeAsync(templatedRequest,CancellationToken.None);
     Assert(templatedRequest.Response.StatusCode==200 &&
         Encoding.UTF8.GetString(body.ToArray())=="plenaro-favorite-proxy-ready",
         "templated host exposes diagnostic without app credentials");
 }
 var legacyAndNew=new ConfigurationBuilder().AddInMemoryCollection(
     new Dictionary<string,string?>{
         ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.org",
         ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.org",
         ["PLENARO_FAVORITES_BASE_DOMAIN"]="apps.example.org",
         ["PLENARO_FAVORITES_HOST_TEMPLATE"]="{slug}-plenaro.example.org",
         ["Security:RequireHttps"]="true"
     }).Build();
 var bothProxy=new FavoriteProxy(legacyAndNew);
 var legacyRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 legacyRequest.Request.Method="GET";
 legacyRequest.Request.Scheme="https";
 legacyRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "wiki.apps.example.org");
 legacyRequest.Request.Path="/_plenaro/favorite-health";
 using(var body=new MemoryStream())
 {
     legacyRequest.Response.Body=body;
     Assert(bothProxy.IsFavoriteHost(legacyRequest),
         "legacy alias still enters favorite proxy after migration");
     await bothProxy.ServeAsync(legacyRequest,CancellationToken.None);
     Assert(legacyRequest.Response.StatusCode==200 &&
         Encoding.UTF8.GetString(body.ToArray())=="plenaro-favorite-proxy-ready",
         "legacy favorite host stays accessible when template configured");
 }
 var malformedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext();
 malformedRequest.Request.Method="GET";
 malformedRequest.Request.Scheme="https";
 malformedRequest.Request.Host=new Microsoft.AspNetCore.Http.HostString(
     "a.b-plenaro.example.org");
 malformedRequest.Request.Path="/_plenaro/favorite-health";
 Assert(templatedProxy.IsFavoriteHost(malformedRequest),
     "malformed alias is intercepted, never falls through");
 await templatedProxy.ServeAsync(malformedRequest,CancellationToken.None);
 Assert(malformedRequest.Response.StatusCode==404,
     "malformed aliases return 404 instead of Plenaro UI");

// Znuny article bodies are nested iframes: every ancestor must satisfy
// frame-ancestors. Parent Plenaro plus the proxy's own origin are permitted,
// without allowing unrelated websites or disabling framing protection.
var nativeCspMethod=typeof(ZnunyNativeProxy).GetMethod(
    "BrowserContentSecurityPolicy",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var nativeCsp=(string)nativeCspMethod.Invoke(null,new object[]{
    new Uri("https://portal.example.org")
})!;
var frameAncestors=nativeCsp.Split(';',StringSplitOptions.TrimEntries)
    .Single(x=>x.StartsWith("frame-ancestors ",StringComparison.Ordinal));
Assert(frameAncestors=="frame-ancestors 'self' https://portal.example.org",
    "Znuny nested article iframes may embed under proxy self and Plenaro");
Assert(!frameAncestors.Contains('*') &&
    !frameAncestors.Contains("example.net",StringComparison.Ordinal) &&
    nativeCsp.Contains("frame-src 'self'",StringComparison.Ordinal),
    "Znuny nested frame fix retains restricted framing and child frame policy");

// A global service-account sync can contain far more tickets than the
// authenticated agent can read. The personal session's TicketSearch result
// is the only positive allowlist; a ticket from a foreign queue is excluded.
var personalSearchCalls=0;
var personalSearch=new ZnunyReader(new FakeFactory(request=>{
    var path=request.RequestUri?.AbsolutePath??"";
    if(path.EndsWith("/Session",StringComparison.Ordinal)){
        Assert(request.Method==HttpMethod.Post,
            "permission session uses normal personal Znuny login");
        using var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync()
            .GetAwaiter().GetResult());
        Assert(body.RootElement.GetProperty("UserLogin").GetString()=="ci-agent",
            "queue permissions are checked as the signed-in agent");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("{\"SessionID\":\"personal-only\"}",
                Encoding.UTF8,"application/json")
        };
    }
    if(path.EndsWith("/Ticket",StringComparison.Ordinal)){
        personalSearchCalls++;
        var query=System.Web.HttpUtility.ParseQueryString(
            request.RequestUri!.Query);
        Assert(query["SessionID"]=="personal-only" &&
            query["TicketCreateTimeNewerDate"] is not null,
            "TicketSearch uses personal session and explicit date window");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("{\"TicketID\":[\"77\",\"79\"]}",
                Encoding.UTF8,"application/json")
        };
    }
    if(path.EndsWith("/Ticket/77",StringComparison.Ordinal)){
        Assert(request.RequestUri!.Query.Contains("SessionID=personal-only"),
            "ticket detail permission verifies with personal TicketGet");
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(
                "{\"Ticket\":{\"TicketID\":\"77\",\"TicketNumber\":\"2026#77\",\"Title\":\"Authorized\",\"Queue\":\"IT\",\"State\":\"open\",\"StateType\":\"open\"}}",
                Encoding.UTF8,"application/json")
        };
    }
    throw new Exception("Unexpected mock Znuny URL");
}));
var personalSettings=new ServerSettings(ApiUrl:
    "https://znuny.example.org/otrs/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST",
    SearchLimit:100);
var personalCounter=new ZnunyReader.Counter(5);
var personalSession=await personalSearch.SessionAsync(personalSettings,
    "ci-agent","ci-password",personalCounter,CancellationToken.None);
var personalAuthorized=await personalSearch.SearchWindowAsync(
    personalSettings,personalSession,"created",
    new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero),
    new DateTimeOffset(2026,11,9,0,0,0,TimeSpan.Zero),
    personalCounter,CancellationToken.None);
Assert(personalSearchCalls==1,
    "single unsaturated per-agent search produces authoritative ticket IDs");
var restricted=ZnunyTicketVisibility.Intersect(
    new[]{"42","77","78","79","80"},personalAuthorized.ToHashSet());
Assert(restricted.SequenceEqual(new[]{"77","79"}) && !restricted.Contains("42"),
    "central cache rows from foreign queues excluded by per-agent search");
var checkedTicket=await personalSearch.GetAsync(personalSettings,personalSession,
    "77",personalCounter,CancellationToken.None);
Assert(checkedTicket.Id=="77" && checkedTicket.Queue=="IT",
    "direct ticket reads revalidate permissions under agent identity");

var settings=new ServerSettings(ApiUrl:"https://znuny.test/otrs/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST",
    WebUrl:"https://znuny.test/otrs/index.pl");

static void Assert(bool condition,string description)
{
    if(!condition)throw new Exception("FAILED: "+description);
    Console.WriteLine("PASS: "+description);
}
static bool Ack(string json)
{
    using var doc=JsonDocument.Parse(json);
    return ZnunyReader.ConfirmedUpdate(doc.RootElement,"77");
}
foreach(var good in new[]{
    "{\"Success\":1}","{\"Success\":true}","{\"Success\":\"1\"}",
    "{\"TicketID\":77}","{\"TicketID\":\"77\"}",
    "{\"Data\":{\"TicketID\":77}}","{\"ArticleID\":812}"
})Assert(Ack(good),"Znuny success ACK "+good);
foreach(var bad in new[]{
    "{}","{\"Success\":0}","{\"Success\":false,\"TicketID\":77}",
    "{\"TicketID\":\"999\"}","{\"ArticleID\":0}",
    "{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"},\"Success\":1}"
})Assert(!Ack(bad),"reject ambiguous/mismatch ACK "+bad);

async Task<(bool Confirmed,JsonElement? Sent)> SendWrite(string response,int status,
    Dictionary<string,object> changes)
{
    var requests=0;
    JsonElement? sent=null;
    var factory=new FakeFactory(request=>{
        requests++;
        Assert(request.Method==HttpMethod.Post,"TicketUpdate HTTP POST");
        Assert(request.RequestUri?.AbsolutePath.EndsWith("/Ticket/77/Update")==true,
            "configured legacy TicketUpdate route respected");
        using var doc=JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        sent=doc.RootElement.Clone();
        return new HttpResponseMessage((HttpStatusCode)status){
            Content=new StringContent(response,Encoding.UTF8,"application/json")
        };
    });
    var reader=new ZnunyReader(factory);
    var result=await reader.UpdateAsync(settings,"session", "77",changes,
        new ZnunyReader.Counter(2),CancellationToken.None);
    Assert(requests==1,"one remote TicketUpdate, never retried");
    return(result,sent);
}

var reply=new Dictionary<string,object>{
    ["Article"]=new Dictionary<string,object>{
        ["CommunicationChannel"]="Email",["SenderType"]="agent",
        ["IsVisibleForCustomer"]=1,["ArticleSend"]=1,
        ["AppendSignatureToBody"]=1,["To"]="kunde@example.org",
        ["Subject"]="Re: Test",["Body"]="Hallo Kunde",
        ["ContentType"]="text/html; charset=utf-8",["MimeType"]="text/html",
        ["Charset"]="utf-8"
    }
};
var normal=await SendWrite("{\"Success\":1}",200,reply);
Assert(normal.Confirmed,"reply Success=1 accepted");
var actual=normal.Sent!.Value;
Assert(actual.GetProperty("SessionID").GetString()=="session" &&
    actual.GetProperty("TicketID").GetString()=="77" &&
    actual.GetProperty("Article").GetProperty("ArticleSend").GetInt32()==1 &&
    actual.GetProperty("Article").GetProperty("To").GetString()=="kunde@example.org",
    "reply uses Classic outbound-email payload");
var close=new Dictionary<string,object>(reply){
    ["Ticket"]=new Dictionary<string,object>{["State"]="closed successful"}
};
var closeResult=await SendWrite("{\"Success\":1}",200,close);
Assert(closeResult.Confirmed &&
    closeResult.Sent!.Value.GetProperty("Ticket").GetProperty("State")
        .GetString()=="closed successful",
    "answer + close in one TicketUpdate");
var booking=new Dictionary<string,object>{
    ["Article"]=new Dictionary<string,object>{
        ["Subject"]="TaskTool Zeitbuchung",["Body"]="Prüfung",
        ["CommunicationChannel"]="Internal",["SenderType"]="agent",
        ["TimeUnit"]=30,["IsVisibleForCustomer"]=0,
        ["ContentType"]="text/plain; charset=utf-8",
        ["MimeType"]="text/plain",["Charset"]="utf-8"
    },
    ["DynamicField"]=new[]{
        new Dictionary<string,string>{{"Name","KostenstelleID"},{"Value","100"}},
        new Dictionary<string,string>{{"Name","AuftragsID"},{"Value","200"}}
    }
};
var bookingResult=await SendWrite("{\"Success\":1}",200,booking);
Assert(bookingResult.Confirmed &&
    bookingResult.Sent!.Value.GetProperty("Article").GetProperty("TimeUnit").GetInt32()==30 &&
    bookingResult.Sent!.Value.GetProperty("DynamicField").GetArrayLength()==2,
    "time booking confirmed with exact 15-min-rounded TimeUnit and dynamic fields");
var assigned=await SendWrite("{\"TicketID\":77}",200,
    new Dictionary<string,object>{["Ticket"]=new Dictionary<string,object>{
        ["OwnerID"]=145,["ResponsibleID"]=200
    }});
Assert(assigned.Confirmed,"assignment accepts legacy ID ACK");
var unconfirmed=await SendWrite("{}",200,reply);
Assert(!unconfirmed.Confirmed,"empty 200 does NOT imply successful write");

async Task ExpectFailure(string body,int status,string code,bool rejected)
{
    var reader=new ZnunyReader(new FakeFactory(_=>
        new HttpResponseMessage((HttpStatusCode)status){
            Content=new StringContent(body,Encoding.UTF8,"application/json")
        }));
    try{
        await reader.UpdateAsync(settings,"session","77",reply,
            new ZnunyReader.Counter(2),CancellationToken.None);
        throw new Exception("Expected remote failure "+code);
    }catch(ZnunyReader.RemoteFailure ex){
        Assert(ex.Code==code && ex.DefinitivelyRejected==rejected,
            "remote "+status+" is "+code+" and rejected="+rejected);
        if(code=="remote-rejected" && body.Contains("ErrorCode",StringComparison.Ordinal))
            Assert(ex.RemoteCode=="TicketUpdate.InvalidParameter",
                "safe diagnostic exposes Znuny machine code only");
        if(code=="remote-rejected" && !body.Contains("ErrorCode",StringComparison.Ordinal))
            Assert(ex.RemoteCode is null,"no invented Znuny error code");
    }
}
await ExpectFailure("{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"}}",
    200,"remote-rejected",true);
await ExpectFailure("{\"Error\":{\"ErrorCode\":\"TicketUpdate.InvalidParameter\"}}",
    400,"remote-rejected",true);
await ExpectFailure("{}",404,"route-not-found",true);
await ExpectFailure("{}",401,"authentication",true);
await ExpectFailure("{}",500,"server-error",false);
await ExpectFailure("{}",502,"server-error",false);
// Standard modern Znuny mapping uses PATCH /Ticket/{ID}; it is selected
// by the administrator, never attempted as a fallback to an ambiguous POST.
var patchCalls=0;
var patched=new ZnunyReader(new FakeFactory(request=>{
    patchCalls++;
    Assert(request.Method==HttpMethod.Patch &&
        request.RequestUri?.AbsolutePath.EndsWith("/Ticket/77")==true,
        "modern Znuny PATCH TicketUpdate route, not a second POST");
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent("{\"Success\":1}",Encoding.UTF8,"application/json")
    };
}));
var modern=settings with {TicketUpdateRoute="/Ticket/{TicketID}",TicketUpdateMethod="PATCH"};
Assert(await patched.UpdateAsync(modern,"session","77",reply,
        new ZnunyReader.Counter(2),CancellationToken.None) && patchCalls==1,
    "configured PATCH contract accepted with exactly one write");
await ExpectFailure("{\"Success\":0}",200,"remote-rejected",true);
// A 200 response to SessionCreate may contain an API error rather than a
// SessionID. Never attempt TicketGet or a write after such a response.
var loginCalls=0;
var badLogin=new ZnunyReader(new FakeFactory(request=>{
    loginCalls++;
    Assert(request.RequestUri?.AbsolutePath.EndsWith("/Session")==true,
        "personal login stops before TicketGet when session invalid");
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(
            """{"Error":{"ErrorCode":"SessionCreate.AuthFail"}}""",
            Encoding.UTF8,"application/json")
    };
}));
try{
    await badLogin.SessionAsync(settings,"bad-user","bad-password",
        new ZnunyReader.Counter(2),CancellationToken.None);
    throw new Exception("SessionCreate missing SessionID accepted unexpectedly");
}catch(ZnunyReader.RemoteFailure e){
    Assert(e.Code=="remote-rejected" &&
        e.RemoteCode=="SessionCreate.AuthFail" && loginCalls==1,
        "personal SessionCreate errors are recognized without attempting writes");
}

// A stock Znuny connector may lack the *optional* DynamicField/Options
// endpoint; admin-configured keys must still allow a time booking to proceed.
var optionCalls=0;
var optionReader=new ZnunyReader(new FakeFactory(request=>{
    var path=request.RequestUri?.AbsolutePath??"";
    if(path.EndsWith("/Ticket/77"))
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(
                """{"Ticket":{"TicketID":"77"}}""",
                Encoding.UTF8,"application/json")
        };
    optionCalls++;
    Assert(path.Contains("/DynamicField/") && path.EndsWith("/Options"),
        "optional DynamicField endpoint queried by server");
    return new HttpResponseMessage(HttpStatusCode.NotFound){
        Content=new StringContent("{}",Encoding.UTF8,"application/json")
    };
}));
var fallbackSettings=settings with{
    CostCenterOptions="100=IT;200=Service",OrderOptions="AB=Auftrag"
};
var fieldResult=await optionReader.GetBookingFieldsAsync(fallbackSettings,
    "session","77",new ZnunyReader.Counter(4),CancellationToken.None);
Assert(optionCalls==2 && fieldResult.CostCenterOptions.Count==2 &&
    fieldResult.OrderOptions.Single().Key=="AB",
    "404 on optional DynamicField route falls back to admin-controlled booking keys");

// Native iframe proxy tests use ONLY synthetic example hosts, never
// customer infrastructure. Verify the exact standard agent login markup.
var proxyType=typeof(ZnunyNativeProxy);
var findLogin=proxyType.GetMethod("FindLoginForm",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var upstream=new Uri("https://ticketsystem.example.test");
var page=new Uri("https://ticketsystem.example.test/otrs/index.pl");
const string formHtml="""
    <html><form method="post" action="/otrs/index.pl">
      <input type="hidden" name="ChallengeToken" value="synthetic-challenge">
      <input type="hidden" name="Action" value="Login">
      <input type="hidden" name="RequestedURL" value="">
      <input type="text" name="User" value="">
      <input type="password" name="Password">
    </form></html>
    """;
var parsed=findLogin.Invoke(null,new object[]{
    formHtml,page,upstream,"agent.example","test-password"
})!;
var target=(Uri)parsed.GetType().GetProperty("Action")!.GetValue(parsed)!;
var fields=(IEnumerable<KeyValuePair<string,string>>)parsed.GetType()
    .GetProperty("Fields")!.GetValue(parsed)!;
Assert(target==page && fields.Any(f=>f.Key=="ChallengeToken"&&
    f.Value=="synthetic-challenge")&&
    fields.Any(f=>f.Key=="User"&&f.Value=="agent.example")&&
    fields.Any(f=>f.Key=="Password"&&f.Value=="test-password"),
    "native agent login retains CSRF hidden fields and fills user credentials");
try{
    findLogin.Invoke(null,new object[]{
        formHtml.Replace("action=\"/otrs/index.pl\"",
            "action=\"https://other.example.test/steal\""),
        page,upstream,"agent.example","test-password"
    });
    throw new Exception("Cross-origin login action accepted");
}catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException){
    Assert(true,"native login blocks credential delivery to a different host");
}
var rewriter=proxyType.GetMethod("Rewrite",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var rewritten=(string)rewriter.Invoke(null,new object[]{
    "https://ticketsystem.example.test/otrs/index.pl "+
    "https:\\/\\/ticketsystem.example.test/otrs/index.pl",
    upstream,new Uri("https://tickets.example.test")
})!;
Assert(!rewritten.Contains("ticketsystem.example.test")&&
    rewritten.Contains("https://tickets.example.test"),
    "native proxy rewrites original absolute and escaped JS URLs");
var config=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["Security:RequireHttps"]="true",
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.test",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.example.test"
    }).Build();
var proxy=new ZnunyNativeProxy(config);
var origins=proxyType.GetMethod("Origins",
    BindingFlags.Instance|BindingFlags.NonPublic)!;
Assert((bool)origins.Invoke(proxy,new object?[]{null,null})!,
    "native proxy accepts two distinct same-site HTTPS origins");
var badConfig=new ConfigurationBuilder().AddInMemoryCollection(
    new Dictionary<string,string?>{
        ["Security:RequireHttps"]="true",
        ["PLENARO_PUBLIC_ORIGIN"]="https://portal.example.test",
        ["PLENARO_ZNUNY_PROXY_ORIGIN"]="https://tickets.other.test"
    }).Build();
Assert(!(bool)origins.Invoke(new ZnunyNativeProxy(badConfig),
    new object?[]{null,null})!,
    "native proxy rejects cross-site HTTPS origins");

// The native proxy must not fail closed merely because a legacy agent
// installation uses a custom/multistep HTML login. The regular browser can
// complete that login through the same already-authorized proxy session.
var nativeLogin=proxyType.GetMethod("LoginAsync",
    BindingFlags.Static|BindingFlags.NonPublic)!;
async Task<string> RunNativeLogin(Func<HttpRequestMessage,HttpResponseMessage> respond)
{
    using var http=new HttpClient(new MockHandler(respond));
    var task=(Task)nativeLogin.Invoke(null,new object[]{
        http,page,upstream,"agent.example","example-password",CancellationToken.None
    })!;
    await task;
    return task.GetType().GetProperty("Result")!.GetValue(task)!.ToString()!;
}
// Some internal gateways use nonstandard Content-Type headers for normal
// agent HTML. The source body, not just its MIME label, determines whether
// the initial agent document is usable. All hosts here are fictional.
foreach(var mime in new[]{
    "text/plain","application/octet-stream","application/xhtml+xml"
}){
    var successful=await RunNativeLogin(request=>{
        if(request.Method==HttpMethod.Post){
            var redirect=new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location=new Uri(
                "/otrs/index.pl?Action=AgentDashboard",UriKind.Relative);
            return redirect;
        }
        if(request.RequestUri?.Query.Contains("AgentDashboard")==true)
            return new HttpResponseMessage(HttpStatusCode.OK){
                Content=new StringContent(
                    "<!doctype html><html><body>Dashboard</body></html>",
                    Encoding.UTF8,"text/html")
            };
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(formHtml,Encoding.UTF8,mime)
        };
    });
    Assert(successful=="Automatic",
        "native auto-login accepts real HTML labeled "+mime);
}
var missingContentType=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post)return new HttpResponseMessage(
        HttpStatusCode.NoContent);
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new ByteArrayContent(Encoding.UTF8.GetBytes(formHtml))
    };
});
Assert(missingContentType=="Automatic",
    "native auto-login accepts HTML with missing Content-Type");

foreach(var (mime,body,expected) in new[]{
    ("application/json","{\"error\":\"not a login page\"}","page-json"),
    ("text/plain","This is not HTML","page-not-html"),
    ("text/html","","page-empty")
}){
    try{
        await RunNativeLogin(_=>new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(body,Encoding.UTF8,mime)
        });
        throw new Exception("Non-HTML document incorrectly accepted");
    }catch(InvalidOperationException ex) when(ex.Message==expected){
        Assert(true,"native login safely rejects non-HTML page: "+expected);
    }
}

var successfulPosts=0;
var autoLogin=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post){
        successfulPosts++;
        Assert(request.Headers.TryGetValues("Origin",out var origins)&&
            origins.Single()=="https://ticketsystem.example.test",
            "native login includes upstream-only Origin for CSRF validation");
        var post=new HttpResponseMessage(HttpStatusCode.Found);
        post.Headers.Location=new Uri("/otrs/index.pl?Action=AgentDashboard",UriKind.Relative);
        return post;
    }
    if(request.RequestUri?.Query.Contains("AgentDashboard")==true)
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent("<html><main>Agent dashboard</main></html>",
                Encoding.UTF8,"text/html")
        };
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
    };
});
Assert(autoLogin=="Automatic"&&successfulPosts==1,
    "standard native login POST follows dashboard redirect exactly once");

var rejectedPosts=0;
var interactiveLogin=await RunNativeLogin(request=>{
    if(request.Method==HttpMethod.Post)rejectedPosts++;
    return new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
    };
});
Assert(interactiveLogin=="Interactive"&&rejectedPosts==1,
    "login form returned after POST allows normal isolated browser login");

var customLogin=await RunNativeLogin(_=>
    new HttpResponseMessage(HttpStatusCode.OK){
        Content=new StringContent("<html><main>Custom login</main></html>",
            Encoding.UTF8,"text/html")
    });
Assert(customLogin=="Interactive",
    "unrecognized custom login markup no longer yields unhelpful HTTP 500");

try{
    await RunNativeLogin(request=>{
        if(request.Method==HttpMethod.Post){
            var blocked=new HttpResponseMessage(HttpStatusCode.Found);
            blocked.Headers.Location=new Uri("https://other.example.test/login");
            return blocked;
        }
        return new HttpResponseMessage(HttpStatusCode.OK){
            Content=new StringContent(formHtml,Encoding.UTF8,"text/html")
        };
    });
    throw new Exception("External authentication redirect incorrectly accepted");
}catch(InvalidOperationException ex) when(ex.Message=="unsafe-redirect"){
    Assert(true,"native auto-login never follows cross-origin authentication redirects");
}

// The outer HTTPS gateway may send an internal Host value to Plenaro.
// Browser Origin is still the public URL. All hostnames here are fictitious.
var expectedPublicOrigin=new Uri("https://portal.example.test");
var proxiedRequest=new Microsoft.AspNetCore.Http.DefaultHttpContext().Request;
proxiedRequest.Scheme="https";
proxiedRequest.Host=new Microsoft.AspNetCore.Http.HostString("gateway.example.test");
proxiedRequest.Headers["Origin"]="https://portal.example.test";
proxiedRequest.Headers["Sec-Fetch-Site"]="same-origin";
Assert(ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap accepts matching browser Origin through normalized Host");
proxiedRequest.Headers.Remove("Origin");
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap does not blindly trust normalized internal Host");
proxiedRequest.Host=new Microsoft.AspNetCore.Http.HostString("portal.example.test");
Assert(ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap preserves direct same-host clients without Origin");
proxiedRequest.Headers["Origin"]="https://other.example.test";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects a cross-origin POST even when Host matches");
proxiedRequest.Headers["Origin"]="null";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects opaque/null browser Origin");
proxiedRequest.Headers["Origin"]="https://portal.example.test";
proxiedRequest.Headers["Sec-Fetch-Site"]="cross-site";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap rejects cross-site fetch metadata");
proxiedRequest.Headers["Sec-Fetch-Site"]="same-origin";
proxiedRequest.Scheme="http";
Assert(!ZnunyNativeProxy.AllowedStartOrigin(proxiedRequest,expectedPublicOrigin),
    "native bootstrap requires verified HTTPS");

// Regression: a real HTML agent page can arrive as plain text or binary
// and must render as HTML for a navigation. Assets/downloads must not be
// promoted. Only synthetic markup and fictional hostnames are tested.
var sniff=proxyType.GetMethod("LooksLikeHtmlDocument",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var nav=proxyType.GetMethod("IsDocumentNavigation",
    BindingFlags.Static|BindingFlags.NonPublic)!;
var mimeCheck=proxyType.GetMethod("PotentialHtmlMime",
    BindingFlags.Static|BindingFlags.NonPublic)!;
bool Html(string body)=>(bool)sniff.Invoke(null,new object[]{body})!;
bool Mime(string? contentType)=>(bool)mimeCheck.Invoke(
    null,new object?[]{contentType})!;
bool Navigation(string method,string? accept,string? destination,
    string path="/otrs/index.pl",string query="",string? mode=null) {
    var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
    http.Request.Method=method;
    http.Request.Path=path;
    http.Request.QueryString=new Microsoft.AspNetCore.Http.QueryString(query);
    if(accept is not null)http.Request.Headers["Accept"]=accept;
    if(destination is not null)http.Request.Headers["Sec-Fetch-Dest"]=destination;
    if(mode is not null)http.Request.Headers["Sec-Fetch-Mode"]=mode;
    return (bool)nav.Invoke(null,new object[]{
        http.Request,new Uri("https://ticketsystem.example.test"+path+query),
        new Uri("https://ticketsystem.example.test/otrs/index.pl")
    })!;
}
Assert(Html("<!DOCTYPE html><html><head><title>Agent</title></head><body></body></html>"),
    "a complete agent HTML document is recognized by its markup");
Assert(!Html("{\"error\":\"no-html\"}")&&!Html("binary")&&!Html(""),
    "non-HTML and JSON cannot trigger the HTML MIME override");
foreach(var mime in new string?[]{
    null,"text/plain","text/xml","application/xml",
    "application/octet-stream","application/x-octet-stream","binary/octet-stream"
})Assert(Mime(mime),"potential upstream CGI HTML MIME supported");
foreach(var mime in new[]{"application/javascript","application/json",
    "text/css","image/png","application/pdf","font/woff2"})
    Assert(!Mime(mime),"asset/attachment MIME remains unchanged: "+mime);
Assert(Navigation("GET","text/html,application/xhtml+xml",null,
    "/otrs/index.pl","?Action=AgentDashboard"),
    "HTML navigation with a CGI query must be recognized");
Assert(Navigation("GET",null,"iframe","/otrs/index.pl","?Action=AgentTicketZoom"),
    "iframe document navigation recognized with CGI query");
Assert(!Navigation("GET","*/*",null,"/otrs/js/ticket.js"),
    "AJAX and JavaScript resource requests are never retyped as HTML");
Assert(Navigation("POST","text/html","document",
    "/otrs/index.pl","?Action=AgentTicketCompose"),
    "reply form POST navigation can render full HTML document");
Assert(Navigation("POST","text/html",null,
    "/otrs/index.pl","?Action=AgentTicketCompose",mode:"navigate"),
    "browser navigate-mode POST accepts HTML MIME fallback");
Assert(!Navigation("POST","text/html",null,
    "/otrs/index.pl","?Action=AgentTicketCompose"),
    "AJAX POST is not misclassified as a document navigation");
Assert(!Navigation("POST","application/json",null,
    "/otrs/index.pl","?Action=AgentTicketCompose",mode:"cors"),
    "AJAX JSON POST never forces HTML MIME");

// Verify the actual opaque-content reader, not only its MIME predicates.
var opaqueReader=proxyType.GetMethod("ReadOpaqueNavigationAsync",
    BindingFlags.Static|BindingFlags.NonPublic)!;
async Task<(byte[]? Html,byte[] Streamed)> ProbeOpaque(byte[] source)
{
    using var content=new ByteArrayContent(source);
    using var streamed=new MemoryStream();
    var task=(Task<byte[]?>)opaqueReader.Invoke(null,new object[]{
        content,streamed,CancellationToken.None
    })!;
    var result=await task;
    return(result,streamed.ToArray());
}
var syntheticHtml=Encoding.UTF8.GetBytes(
    "<!DOCTYPE html><html><body><form method='post'>"+
    "<input name='User'></form></body></html>");
var htmlResult=await ProbeOpaque(syntheticHtml);
Assert(htmlResult.Html is not null &&
    htmlResult.Html.SequenceEqual(syntheticHtml) &&
    htmlResult.Streamed.Length==0,
    "opaque CGI HTML is buffered for safe URL rewriting");
var syntheticBinary=new byte[6*1024*1024+8192];
syntheticBinary[0]=0x00;
syntheticBinary[^1]=0xff;
var binaryResult=await ProbeOpaque(syntheticBinary);
Assert(binaryResult.Html is null &&
    binaryResult.Streamed.SequenceEqual(syntheticBinary),
    "large non-HTML binary downloads stream unchanged without HTML size cap");

Console.WriteLine("Znuny mock integration tests completed successfully. No real ticket modified.");

sealed class FakeFactory(Func<HttpRequestMessage,HttpResponseMessage> answer):IHttpClientFactory
{
    public HttpClient CreateClient(string name)=>new(new MockHandler(answer));
}
sealed class MockHandler(Func<HttpRequestMessage,HttpResponseMessage> answer):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken token)=>Task.FromResult(answer(request));
}
