using System.Net;
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
