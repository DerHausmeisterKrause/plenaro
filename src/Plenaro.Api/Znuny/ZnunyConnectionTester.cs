using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Explicit read-only SessionCreate probe. No redirects, cookies, proxy, response
/// reflection, secret logging or automated retries. TCP connects only to
/// checked DNS addresses, preventing localhost/metadata DNS rebinding.
/// </summary>
public sealed class ZnunyConnectionTester(IHttpClientFactory clients)
{
    public sealed record ConnectionResult(bool Success, string Status);

    public async Task<ConnectionResult> TrySessionAsync(
        string baseUrl, string username, string password, CancellationToken ct)
    {
        var url = baseUrl.TrimEnd('/') + "/Session";
        using var req = new HttpRequestMessage(HttpMethod.Post,url);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            UserLogin = username,
            Password = password
        }),Encoding.UTF8,"application/json");
        try
        {
            using var response = await clients.CreateClient("Plenaro.Znuny")
                .SendAsync(req,HttpCompletionOption.ResponseHeadersRead,ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new(false,"auth-rejected");
            if (!response.IsSuccessStatusCode)
                return new(false,"remote-error");
            if (response.Content.Headers.ContentLength is > 32768)
                return new(false,"invalid-response");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var limited = new MemoryStream();
            var buffer = new byte[4096];
            while (true)
            {
                var bytes = await stream.ReadAsync(buffer,ct);
                if (bytes == 0) break;
                if (limited.Length + bytes > 32768) return new(false,"invalid-response");
                limited.Write(buffer,0,bytes);
            }
            limited.Position = 0;
            using var document = await JsonDocument.ParseAsync(limited,cancellationToken:ct);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new(false,"invalid-response");
            if (document.RootElement.TryGetProperty("SessionID",out var id) &&
                id.ValueKind == JsonValueKind.String &&
                id.GetString() is { Length: > 0 })
                return new(true,"connected");
            return new(false,"auth-or-protocol-error");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false,"timeout");
        }
        catch (HttpRequestException) { return new(false,"unavailable"); }
        catch (SocketException) { return new(false,"unavailable"); }
        catch (IOException) { return new(false,"unavailable"); }
        catch (JsonException) { return new(false,"invalid-response"); }
    }

    public static SocketsHttpHandler SecureHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.None,
        MaxResponseHeadersLength = 16,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host,ct);
            // Restrict the actual TCP peer, not just the user-provided URL.
            // RFC1918/private LAN hosts remain allowed for administrator-controlled installations.
            var allowed = addresses.FirstOrDefault(Allowed);
            if (allowed is null)
                throw new HttpRequestException("Host address is blocked.");
            var socket = new Socket(allowed.AddressFamily,SocketType.Stream,ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(allowed,context.DnsEndPoint.Port,ct);
                return new NetworkStream(socket,ownsSocket:true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    internal static bool Allowed(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip=ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) ||
            ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6Multicast ||
            ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
            return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes=ip.GetAddressBytes();
            if (bytes[0] is 0 or 127 or >= 224) return false;
            if (bytes[0] == 169 && bytes[1] == 254) return false;
            if (bytes[0] == 100 && bytes[1] == 100) return false; // metadata / CGN special
        }
        return true;
    }
}
