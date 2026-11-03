using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Plenaro.Api.Znuny;

public sealed partial class ZnunyRemoteBrowser
{
    // Selenium Grid brokers this socket only inside the Docker network.
    // The user connects exclusively to Plenaro's authenticated SSE endpoint.
    private async Task StreamAsync(Guid user,HttpResponse response,
        IConfiguration config,CancellationToken ct)
    {
        var browser=Current(user);
        await browser.Gate.WaitAsync(ct);
        try{await ConfirmHostAsync(browser,ct);}
        finally{browser.Gate.Release();}

        var sessionId=browser.SessionId;
        if(!SafeSession.IsMatch(sessionId))throw new InvalidOperationException();
        using var socket=new ClientWebSocket();
        socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(15);
        using(var connect=CancellationTokenSource.CreateLinkedTokenSource(ct)){
            connect.CancelAfter(TimeSpan.FromSeconds(12));
            await socket.ConnectAsync(
                new Uri("ws://znuny-browser:4444/session/"+sessionId+"/se/cdp"),
                connect.Token);
        }

        var nextId=0;
        await SendCdp(socket,++nextId,"Target.getTargets",null,null,ct);
        using var targetList=await CdpReply(socket,nextId,ct);
        var infos=targetList.RootElement.GetProperty("result")
            .GetProperty("targetInfos").EnumerateArray();
        string? targetId=null;
        foreach(var info in infos){
            if(info.GetProperty("type").GetString()!="page")continue;
            var url=info.GetProperty("url").GetString();
            if(url is null||!Uri.TryCreate(url,UriKind.Absolute,out var parsed)||
                !Allowed(browser.Entry,parsed))continue;
            targetId=info.GetProperty("targetId").GetString();
            break;
        }
        if(string.IsNullOrEmpty(targetId))throw new InvalidOperationException();
        await SendCdp(socket,++nextId,"Target.attachToTarget",
            new{targetId,flatten=true},null,ct);
        using var attached=await CdpReply(socket,nextId,ct);
        var targetSession=attached.RootElement.GetProperty("result")
            .GetProperty("sessionId").GetString();
        if(string.IsNullOrEmpty(targetSession))throw new InvalidOperationException();

        await SendCdp(socket,++nextId,"Page.enable",new{},targetSession,ct);
        using(var enabled=await CdpReply(socket,nextId,ct)){}
        await SendCdp(socket,++nextId,"Page.getFrameTree",new{},targetSession,ct);
        using(var tree=await CdpReply(socket,nextId,ct)){
            var currentUrl=tree.RootElement.GetProperty("result")
                .GetProperty("frameTree").GetProperty("frame")
                .GetProperty("url").GetString();
            if(currentUrl is null||
                !Uri.TryCreate(currentUrl,UriKind.Absolute,out var parsed)||
                !Allowed(browser.Entry,parsed))throw new InvalidOperationException();
        }

        response.Headers["X-Accel-Buffering"]="no";
        await response.WriteAsync(": Plenaro native image stream\n\n",ct);
        await response.Body.FlushAsync(ct);
        using var heartbeatStop=CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var writeGate=new SemaphoreSlim(1,1);
        var heartbeat=Task.Run(async()=>{
            try{
                while(!heartbeatStop.IsCancellationRequested){
                    await Task.Delay(TimeSpan.FromSeconds(12),heartbeatStop.Token);
                    // Only the owner can receive frames; every long-running
                    // connection rechecks the authorization and session.
                    if(!ReferenceEquals(Current(user),browser)||
                        !await ApprovedAsync(user,config,heartbeatStop.Token)){
                        heartbeatStop.Cancel();
                        return;
                    }
                    await writeGate.WaitAsync(heartbeatStop.Token);
                    try{
                        await response.WriteAsync(": keepalive\n\n",heartbeatStop.Token);
                        await response.Body.FlushAsync(heartbeatStop.Token);
                    }finally{writeGate.Release();}
                }
            }catch(OperationCanceledException){}
            catch{heartbeatStop.Cancel();}
        },heartbeatStop.Token);

        try{
            await SendCdp(socket,++nextId,"Page.startScreencast",
                new{format="jpeg",quality=66,maxWidth=2400,maxHeight=1600,
                    everyNthFrame=1,maxFramesInFlight=1},targetSession,ct);
            var last=DateTimeOffset.MinValue;
            while(!ct.IsCancellationRequested&&!heartbeatStop.IsCancellationRequested){
                using var message=await ReadCdp(socket,ct);
                var root=message.RootElement;
                if(root.TryGetProperty("error",out _))throw new InvalidOperationException();
                if(!root.TryGetProperty("method",out var method))continue;
                if(root.TryGetProperty("sessionId",out var envelope)&&
                    envelope.GetString()!=targetSession)continue;
                var name=method.GetString();
                if(name=="Page.frameNavigated"){
                    var page=root.GetProperty("params").GetProperty("frame");
                    if(!page.TryGetProperty("parentId",out _)){
                        var url=page.GetProperty("url").GetString();
                        if(url is null||
                            !Uri.TryCreate(url,UriKind.Absolute,out var parsed)||
                            !Allowed(browser.Entry,parsed))
                            throw new InvalidOperationException();
                    }
                    continue;
                }
                if(name!="Page.screencastFrame")continue;
                var data=root.GetProperty("params");
                var frameId=data.GetProperty("sessionId").GetInt32();
                var encoded=data.GetProperty("data").GetString();
                // The source URL is checked before opening the screencast,
                // on navigation events and by the existing WebDriver input.
                if(encoded is not null&&encoded.Length is >100 and <9000000&&
                    DateTimeOffset.UtcNow-last>=TimeSpan.FromMilliseconds(110)){
                    if(!ReferenceEquals(Current(user),browser))break;
                    await writeGate.WaitAsync(ct);
                    try{
                        await response.WriteAsync("event: frame\ndata: "+encoded+"\n\n",ct);
                        await response.Body.FlushAsync(ct);
                    }finally{writeGate.Release();}
                    last=DateTimeOffset.UtcNow;
                }
                // Acknowledge only after the frontend transport has flushed:
                // one frame in flight avoids unbounded frame queues.
                await SendCdp(socket,++nextId,"Page.screencastFrameAck",
                    new{sessionId=frameId},targetSession,ct);
            }
        }finally{
            await heartbeatStop.CancelAsync();
            try{await heartbeat;}catch(OperationCanceledException){}
            if(socket.State==WebSocketState.Open){
                using var stop=CancellationTokenSource.CreateLinkedTokenSource(ct);
                stop.CancelAfter(TimeSpan.FromSeconds(2));
                try{await SendCdp(socket,++nextId,"Page.stopScreencast",
                    new{},targetSession,stop.Token);}
                catch(OperationCanceledException){}
                catch(WebSocketException){}
            }
        }
    }

    private static async Task SendCdp(ClientWebSocket socket,int id,string method,
        object? parameters,string? targetSession,CancellationToken ct)
    {
        var command=new Dictionary<string,object?>{
            ["id"]=id,["method"]=method,["params"]=parameters??new{}
        };
        if(targetSession is not null)command["sessionId"]=targetSession;
        var bytes=JsonSerializer.SerializeToUtf8Bytes(command);
        await socket.SendAsync(new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,true,ct);
    }

    private static async Task<JsonDocument> CdpReply(ClientWebSocket socket,
        int expectedId,CancellationToken ct)
    {
        // Chrome can emit target/page events between command replies.
        for(var n=0;n<300;n++){
            var doc=await ReadCdp(socket,ct);
            var root=doc.RootElement;
            if(root.TryGetProperty("id",out var id)&&
                id.ValueKind==JsonValueKind.Number&&id.GetInt32()==expectedId){
                if(root.TryGetProperty("error",out _)){
                    doc.Dispose();
                    throw new InvalidOperationException();
                }
                return doc;
            }
            doc.Dispose();
        }
        throw new InvalidOperationException();
    }

    private static async Task<JsonDocument> ReadCdp(ClientWebSocket socket,
        CancellationToken ct)
    {
        using var output=new MemoryStream();
        var buffer=new byte[32768];
        WebSocketReceiveResult result;
        do{
            result=await socket.ReceiveAsync(
                new ArraySegment<byte>(buffer),ct);
            if(result.MessageType!=WebSocketMessageType.Text)
                throw new InvalidOperationException();
            output.Write(buffer,0,result.Count);
            if(output.Length>12*1024*1024)throw new InvalidOperationException();
        }while(!result.EndOfMessage);
        return JsonDocument.Parse(output.ToArray());
    }
}
