using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Plenaro.Api.Favorites;

public sealed partial class FavoriteProxy
{
    // Diagnostic data lives only in this web process. Never write secrets,
    // request/response bodies, cookies, headers, full paths, or query strings.
    public sealed record FavoriteDebugEntry(
        long Id,DateTimeOffset At,string Level,string Code,
        string Method,string Resource,int? Status,string Route="");

    private readonly ConcurrentDictionary<Guid,byte> _debugAdmins=new();
    private readonly ConcurrentDictionary<(Guid User,Guid Favorite),
        ConcurrentQueue<FavoriteDebugEntry>> _debugEvents=new();
    private long _debugSequence;
    private readonly byte[] _debugPathKey=RandomNumberGenerator.GetBytes(32);
    private const int MaxDebugEvents=250;

    public void ConfigureDebug(Guid user,bool enabled)
    {
        if(enabled){_debugAdmins[user]=1;return;}
        _debugAdmins.TryRemove(user,out _);
        foreach(var key in _debugEvents.Keys)
            if(key.User==user)_debugEvents.TryRemove(key,out _);
    }

    internal bool DebugEnabled(Guid user)=>_debugAdmins.ContainsKey(user);

    public IReadOnlyList<FavoriteDebugEntry> DebugEvents(
        Guid user,Guid favorite,long after)
    {
        if(!DebugEnabled(user)||
           !_debugEvents.TryGetValue((user,favorite),out var queue))
            return [];
        var cutoff=DateTimeOffset.UtcNow.AddMinutes(-30);
        return queue.Where(item=>item.At>=cutoff&&item.Id>after)
            .OrderBy(item=>item.Id).TakeLast(MaxDebugEvents).ToArray();
    }

    public void ClearDebugEvents(Guid user,Guid favorite)
        => _debugEvents.TryRemove((user,favorite),out _);

    // No request-derived strings reach the log. Only safe fixed event codes
    // from our own call sites and strict HTTP-method/resource categories.
    internal void Debug(Guid user,Guid favorite,string level,string code,
        string method="",string resource="",int? status=null)
        => WriteDebug(user,favorite,level,code,method,resource,status,"");

    // A random per-process HMAC fingerprint groups repeated HTTP 400s by
    // endpoint WITHOUT exposing even a sanitized piece of a sensitive path.
    // Path query parameters are never included, even in the fingerprint.
    internal void DebugHttp(Guid user,Guid favorite,string level,string code,
        string method,string resource,int? status,string path)
        => WriteDebug(user,favorite,level,code,method,resource,status,
            DebugRoute(path));

    internal string DebugRoute(string path)
    {
        var clean=path.Split('?',2)[0];
        var bytes=Encoding.UTF8.GetBytes(clean.Length>2048?clean[..2048]:clean);
        var digest=HMACSHA256.HashData(_debugPathKey,bytes);
        return Convert.ToHexString(digest.AsSpan(0,5));
    }

    private void WriteDebug(Guid user,Guid favorite,string level,string code,
        string method,string resource,int? status,string route)
    {
        if(!DebugEnabled(user))return;
        var entry=new FavoriteDebugEntry(
            Interlocked.Increment(ref _debugSequence),
            DateTimeOffset.UtcNow,
            level is "error" or "warning" ? level : "info",
            code,
            method is "GET" or "HEAD" or "POST" or "PUT" or "PATCH" or "DELETE"
                ? method : "",
            resource is "document" or "script" or "style" or "image" or
                "font" or "api" or "asset" ? resource : "other",
            status is >=100 and <=599 ? status : null,
            route);
        var queue=_debugEvents.GetOrAdd((user,favorite),
            _=>new ConcurrentQueue<FavoriteDebugEntry>());
        queue.Enqueue(entry);
        while(queue.Count>MaxDebugEvents)queue.TryDequeue(out _);
    }

    // Never include raw resource names: paths can carry session IDs, tokens,
    // usernames and credentials just as query strings can.
    internal static string DebugResource(string path,bool navigation=false)
    {
        if(navigation)return "document";
        var clean=path.Split('?',2)[0].ToLowerInvariant();
        if(clean.EndsWith(".js")||clean.EndsWith(".mjs"))return "script";
        if(clean.EndsWith(".css"))return "style";
        if(clean.EndsWith(".png")||clean.EndsWith(".jpg")||
           clean.EndsWith(".jpeg")||clean.EndsWith(".svg")||
           clean.EndsWith(".webp")||clean.EndsWith(".gif"))return "image";
        if(clean.EndsWith(".woff")||clean.EndsWith(".woff2")||
           clean.EndsWith(".ttf"))return "font";
        if(clean.Contains("/api/")||clean.Contains("/rest/"))return "api";
        if(clean.Contains("/assets/")||clean.Contains("/static/"))return "asset";
        return "other";
    }
}
