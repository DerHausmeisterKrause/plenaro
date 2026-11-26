using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Plenaro.Api.Znuny;

// Only scanned bytes enter this bounded, process-local cache. No attachments
// are written to the database, disk or a shared Docker volume.
public sealed class AttachmentPreviewCache : BackgroundService
{
    public const int MaxFileBytes=5*1024*1024;
    private const int MaxCacheBytes=64*1024*1024;
    private const int MaxEntries=32;
    public static readonly TimeSpan IdleLifetime=TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MaxLifetime=TimeSpan.FromMinutes(30);
    public sealed record Key(Guid User,int Agent,string Server,string Ticket,string Article,string File);
    public sealed record File(byte[] Bytes,string Mime);
    private sealed record Entry(File File,DateTimeOffset Created,DateTimeOffset LastAccess);
    private readonly Dictionary<Key,Entry> _entries=new();
    private readonly object _gate=new();
    private int _bytes;
    // Bound the upstream JSON/decode and scan work as well as cached bytes.
    public SemaphoreSlim ScanSlots {get;}=new(2,2);

    private void Remove(Key key)
    {
        if(_entries.Remove(key,out var entry))_bytes-=entry.File.Bytes.Length;
    }
    private void Prune(DateTimeOffset now)
    {
        foreach(var pair in _entries.ToArray())
            if(now-pair.Value.LastAccess>=IdleLifetime||now-pair.Value.Created>=MaxLifetime)
                Remove(pair.Key);
    }
    public File? Get(Key key,DateTimeOffset now)
    {
        lock(_gate){
            Prune(now);
            if(!_entries.TryGetValue(key,out var entry))return null;
            _entries[key]=entry with{LastAccess=now};
            return entry.File;
        }
    }
    public void Put(Key key,File file,DateTimeOffset now)
    {
        if(file.Bytes.Length is <=0 or >MaxFileBytes)throw new ArgumentOutOfRangeException(nameof(file));
        lock(_gate){
            Prune(now);Remove(key);
            while(_entries.Count>=MaxEntries||_bytes+file.Bytes.Length>MaxCacheBytes){
                var oldest=_entries.MinBy(x=>x.Value.LastAccess);
                Remove(oldest.Key);
            }
            _entries.Add(key,new Entry(file,now,now));_bytes+=file.Bytes.Length;
        }
    }
    public void Sweep(DateTimeOffset now){lock(_gate)Prune(now);}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(15));
        while(await timer.WaitForNextTickAsync(stoppingToken))Sweep(DateTimeOffset.UtcNow);
    }

    // Reject active document types and content-type spoofing. This is format
    // recognition, not a proof of safety; ClamAV must separately approve bytes.
    public static string? PreviewMime(ReadOnlySpan<byte> bytes,string declared)
    {
        if(bytes.Length is <=0 or >MaxFileBytes)return null;
        string? actual=null;
        if(bytes.StartsWith(new byte[]{137,80,78,71,13,10,26,10}))actual="image/png";
        else if(bytes.StartsWith(new byte[]{255,216,255}))actual="image/jpeg";
        else if(bytes.StartsWith("GIF87a"u8)||bytes.StartsWith("GIF89a"u8))actual="image/gif";
        else if(bytes.Length>=12&&bytes.StartsWith("RIFF"u8)&&bytes[8..].StartsWith("WEBP"u8))actual="image/webp";
        else if(bytes.StartsWith("%PDF-"u8))actual="application/pdf";
        var type=(declared??"").Split(';')[0].Trim().ToLowerInvariant();
        return actual is not null&&(type==actual||type is "" or "application/octet-stream")?actual:null;
    }
}

public enum AttachmentScanVerdict { Clean,Blocked,Unavailable }

public sealed class AttachmentScanner(IConfiguration configuration)
{
    // clamd has no authentication; the service is on a dedicated Docker
    // network with NO published host port. Use INSTREAM, never SCAN paths.
    public static AttachmentScanVerdict ParseVerdict(string reply)
    {
        if(reply=="stream: OK")return AttachmentScanVerdict.Clean;
        if(reply.StartsWith("stream: ",StringComparison.Ordinal)&&
           reply.EndsWith(" FOUND",StringComparison.Ordinal))return AttachmentScanVerdict.Blocked;
        return AttachmentScanVerdict.Unavailable;
    }
    public async Task<AttachmentScanVerdict> ScanAsync(byte[] bytes,CancellationToken ct)
    {
        if(bytes.Length is <=0 or >AttachmentPreviewCache.MaxFileBytes)
            return AttachmentScanVerdict.Unavailable;
        var host=configuration["PLENARO_ATTACHMENT_SCANNER_HOST"];
        if(string.IsNullOrWhiteSpace(host))return AttachmentScanVerdict.Unavailable;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        var token=deadline.Token;
        try{
            using var client=new TcpClient();
            await client.ConnectAsync(host,3310,token);
            await using var stream=client.GetStream();
            await stream.WriteAsync("zINSTREAM\0"u8.ToArray(),token);
            var length=new byte[4];
            for(int offset=0;offset<bytes.Length;){
                int count=Math.Min(65536,bytes.Length-offset);
                BinaryPrimitives.WriteInt32BigEndian(length,count);
                await stream.WriteAsync(length,token);
                await stream.WriteAsync(bytes.AsMemory(offset,count),token);
                offset+=count;
            }
            Array.Clear(length);
            await stream.WriteAsync(length,token);
            // Require a complete NUL-terminated reply. Any error, limit,
            // timeout, unknown verdict or malformed response fails closed.
            var buffer=new byte[1024];int received=0;
            while(received<buffer.Length){
                int n=await stream.ReadAsync(buffer.AsMemory(received),token);
                if(n==0)break;
                int end=Array.IndexOf(buffer,(byte)0,received,n);
                if(end>=0)return ParseVerdict(Encoding.ASCII.GetString(buffer,0,end));
                received+=n;
            }
        }catch(Exception e) when(e is IOException or SocketException ||
            e is OperationCanceledException&&!ct.IsCancellationRequested){
            return AttachmentScanVerdict.Unavailable;
        }
        return AttachmentScanVerdict.Unavailable;
    }
}
