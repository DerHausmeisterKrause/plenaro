using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Plenaro.Api.Znuny;
using static Plenaro.Api.Znuny.ZnunyEndpoints;

internal static class ReadCacheDatabaseTest
{
    public static async Task RunAsync(string connectionString)
    {
        const string schema="ci_znuny_read_cache";
        var ct=CancellationToken.None;
        var builder=new NpgsqlConnectionStringBuilder(connectionString){SearchPath=schema,Pooling=false};
        await using var root=new NpgsqlConnection(connectionString);
        await root.OpenAsync(ct);
        async Task Execute(NpgsqlConnection db,string sql){
            await using var cmd=new NpgsqlCommand(sql,db);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await Execute(root,"CREATE SCHEMA "+schema);
        try{
            await using var db=new NpgsqlConnection(builder.ConnectionString);
            await db.OpenAsync(ct);
            await Execute(db,"""
                CREATE TABLE znuny_cached_tickets(ticket_id varchar(80) PRIMARY KEY,changed_at timestamptz);
                CREATE TABLE znuny_sync_control(source_api_url text,service_username text,enabled boolean);
                INSERT INTO znuny_cached_tickets VALUES('77',now());
                INSERT INTO znuny_sync_control VALUES('https://znuny.example.org/api','service-a',true);
                """);
            await Execute(db,await File.ReadAllTextAsync("src/Plenaro.Api/Database/Migrations/0036_znuny_read_cache.sql",ct));
            var settings=new ServerSettings(ApiUrl:"https://znuny.example.org/api",
                CostCenterFieldName:"KostenstelleID",OrderFieldName:"AuftragsID",
                CostCenterOptions:"100=IT",OrderOptions:"AB=Auftrag");
            var config=new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string,string?>{["ConnectionStrings:Plenaro"]=builder.ConnectionString}).Build();
            var requests=0;
            var ticketCalls=0;
            var generation=1;
            var offline=false;
            var reader=new ZnunyReader(new FakeFactory(req=>{
                requests++;
                var path=req.RequestUri!.AbsolutePath;
                if(path.EndsWith("/Session",StringComparison.Ordinal))
                    return Json("{\"SessionID\":\"ci-cache-session\"}");
                if(path.EndsWith("/Ticket/77",StringComparison.Ordinal)){
                    ticketCalls++;
                    if(offline)return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){
                        Content=new StringContent("{}",Encoding.UTF8,"application/json")};
                    return Json("""{"Ticket":{"TicketID":"77","TicketNumber":"20260077","Title":"Test","DynamicField":[{"Name":"KostenstelleID","Value":"100"},{"Name":"AuftragsID","Value":"AB"}],"Article":[{"ArticleID":"12","Body":"<p>Original</p>","ContentType":"text/html","Subject":"Generation GENERATION"}]}}""".Replace("GENERATION",generation.ToString()));
                }
                if(path.EndsWith(settings.AgentListRoute,StringComparison.Ordinal))
                    return Json("{\"Agents\":[{\"UserID\":77,\"Name\":\"Test Agent\"}]}");
                if(path.EndsWith("/Options",StringComparison.Ordinal))
                    return new HttpResponseMessage(HttpStatusCode.NotFound){Content=new StringContent("{}",Encoding.UTF8,"application/json")};
                throw new Exception("Unexpected cache mock request");
            }));
            using var cache=new ZnunyReadCache(config,reader,NullLogger<ZnunyReadCache>.Instance);
            var central=new ZnunyReadCache.Credentials("service-a","ci-only",true);
            var user=Guid.NewGuid();
            var otherUser=Guid.NewGuid();
            var first=await cache.TicketAsync(settings,central,user,"77",false,ct);
            Check(first.Detail.Articles.Single().Body=="<p>Original</p>"&&ticketCalls==1,"first ticket read fills cache");
            var before=requests;
            for(var i=0;i<150;i++){
                var hit=await cache.TicketAsync(settings,central,otherUser,"77",false,ct);
                Check(hit.Detail.Articles.Single().Subject=="Generation 1","cached detail unchanged");
            }
            Check(requests==before,"150 cache hits do not contact Znuny or consume live quota");
            var fields=await cache.BookingAsync(settings,central,user,"77",ct);
            Check(fields.CostCenter=="100"&&fields.Order=="AB"&&fields.CostCenterOptions.Single().Key=="100","booking fields retain values and fallback options");
            var agents=await cache.AgentsAsync(settings,central,user,ct);
            before=requests;
            Check((await cache.AgentsAsync(settings,central,otherUser,ct)).Single().Name==agents.Single().Name,"shared agent directory");
            _=await cache.BookingAsync(settings,central,otherUser,"77",ct);
            Check(requests==before,"booking options and agents reused without network requests");
            generation=2;
            var refreshed=await cache.TicketAsync(settings,central,user,"77",true,ct);
            Check(refreshed.Detail.Articles.Single().Subject=="Generation 2"&&ticketCalls==2,"manual refresh reads upstream");
            await cache.InvalidateAsync("77");
            generation=3;
            var afterWrite=await cache.TicketAsync(settings,central,user,"77",false,ct);
            Check(afterWrite.Detail.Articles.Single().Subject=="Generation 3"&&ticketCalls==3,"confirmed write invalidates snapshot");
            // A fetch started before the invalidation cannot resurrect old content.
            await cache.InvalidateAsync("77");
            await ZnunyReadCache.SaveTicketAsync(db,settings,central.Username,"77",null,
                first.FetchedAt,first.Detail,ct);
            generation=4;
            Check((await cache.TicketAsync(settings,central,user,"77",false,ct)).Detail.Articles.Single().Subject=="Generation 4",
                "old in-flight snapshot cannot override invalidation");
            await Execute(db,"UPDATE znuny_cached_tickets SET changed_at=clock_timestamp()");
            offline=true;
            var fallback=await cache.TicketAsync(settings,central,user,"77",false,ct);
            Check(fallback.Stale&&fallback.Detail.Articles.Single().Subject=="Generation 4","temporary upstream failure keeps last good snapshot with stale flag");
            offline=false;
            generation=5;
            var personal=new ZnunyReadCache.Credentials("personal-user","ci-only",false);
            Check((await cache.TicketAsync(settings,personal,user,"77",false,ct)).Detail.Articles.Single().Subject=="Generation 5","personal-account reads never use shared snapshots");
            await Execute(db,"UPDATE znuny_sync_control SET service_username='service-b'");
            await using var count=new NpgsqlCommand("SELECT (SELECT count(*) FROM znuny_ticket_detail_cache)+(SELECT count(*) FROM znuny_shared_read_cache)",db);
            Check(Convert.ToInt64(await count.ExecuteScalarAsync(ct))==0,"source account change clears caches");
            Console.WriteLine("PASS: PostgreSQL read-cache regression tests (mock upstream).");
        }finally{await Execute(root,"DROP SCHEMA "+schema+" CASCADE");}
    }
    private static HttpResponseMessage Json(string json)=>new(HttpStatusCode.OK){
        Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private static void Check(bool value,string description){
        if(!value)throw new Exception("FAILED: "+description);
    }
}
