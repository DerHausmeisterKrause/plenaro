using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;
using Plenaro.Api.Znuny;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

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
    string path="/otrs/index.pl",string query="") {
    var http=new Microsoft.AspNetCore.Http.DefaultHttpContext();
    http.Request.Method=method;
    http.Request.Path=path;
    http.Request.QueryString=new Microsoft.AspNetCore.Http.QueryString(query);
    if(accept is not null)http.Request.Headers["Accept"]=accept;
    if(destination is not null)http.Request.Headers["Sec-Fetch-Dest"]=destination;
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
Assert(!Navigation("POST","text/html","document"),
    "POST form responses do not enter the MIME override");

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
