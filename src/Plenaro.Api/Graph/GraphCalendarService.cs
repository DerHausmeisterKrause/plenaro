using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Graph;

public sealed class GraphCalendarService(
    IHttpClientFactory clients, EntraConfiguration entra, SecretVault vault,
    ILogger<GraphCalendarService> logger)
{
    public const string Scheme = "Plenaro.Graph";
    public const string RefreshSecretName = "graph.refresh";
    private const string Scopes = "User.Read Calendars.Read offline_access";

    public sealed record CalendarEvent(
        string Id, string Subject, string Start, string End,
        bool IsAllDay, string ShowAs, string Location, string? WebLink);
    public sealed record CalendarView(IReadOnlyList<CalendarEvent> Events, bool HasMore);

    public async Task<CalendarView> ReadAsync(Guid localUserId, DateTimeOffset start,
        DateTimeOffset end, CancellationToken ct)
    {
        if (!entra.Enabled || !vault.IsConfigured)
            throw new GraphConnectionException("not-configured");

        var refresh = await vault.GetForIntegrationAsync("user", localUserId,
            RefreshSecretName, ct);
        if (string.IsNullOrEmpty(refresh))
            throw new GraphConnectionException("consent-required");

        string access;
        try
        {
            using var token = await RefreshAsync(refresh, ct);
            if (token is null) throw new GraphConnectionException("consent-required");
            access = token.RootElement.GetProperty("access_token").GetString()
                ?? throw new GraphConnectionException("consent-required");
            if (token.RootElement.TryGetProperty("refresh_token", out var next) &&
                next.GetString() is { Length: > 0 } replacement)
                await vault.SetAsync("user", localUserId, RefreshSecretName, replacement, ct);
        }
        catch (HttpRequestException)
        {
            throw new GraphConnectionException("microsoft-unavailable");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GraphConnectionException("microsoft-unavailable");
        }

        var uri = "https://graph.microsoft.com/v1.0/me/calendar/calendarView" +
            "?startDateTime=" + Uri.EscapeDataString(start.UtcDateTime.ToString("O")) +
            "&endDateTime=" + Uri.EscapeDataString(end.UtcDateTime.ToString("O")) +
            "&$select=id,subject,start,end,isAllDay,showAs,isCancelled,location,webLink" +
            "&$top=100";
        using var req = new HttpRequestMessage(HttpMethod.Get, uri);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Headers.TryAddWithoutValidation("Prefer", "outlook.timezone=\"UTC\"");
        HttpResponseMessage response;
        try { response = await clients.CreateClient("Plenaro.Graph").SendAsync(req, ct); }
        catch (HttpRequestException) { throw new GraphConnectionException("microsoft-unavailable"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { throw new GraphConnectionException("microsoft-unavailable"); }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new GraphConnectionException("consent-required");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Graph calendar request returned HTTP {Code}", (int)response.StatusCode);
                throw new GraphConnectionException("microsoft-unavailable");
            }
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),
                cancellationToken: ct);
            var root = document.RootElement;
            var events = new List<CalendarEvent>();
            if (root.TryGetProperty("value", out var items))
            {
                foreach (var e in items.EnumerateArray().Take(100))
                {
                    if (e.TryGetProperty("isCancelled", out var cancelled) &&
                        cancelled.ValueKind == JsonValueKind.True) continue;
                    events.Add(new CalendarEvent(
                        String(e, "id", 512), String(e, "subject", 500),
                        UtcDate(e, "start"), UtcDate(e, "end"),
                        e.TryGetProperty("isAllDay", out var allDay) && allDay.ValueKind == JsonValueKind.True,
                        String(e, "showAs", 32), Location(e), String(e, "webLink", 2048)));
                }
            }
            return new CalendarView(events, root.TryGetProperty("@odata.nextLink", out _));
        }
    }

    private async Task<JsonDocument?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post,
            $"https://login.microsoftonline.com/{entra.TenantId}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = entra.ClientId,
                ["client_secret"] = entra.ClientSecret,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = Scopes
            })
        };
        using var response = await clients.CreateClient("Plenaro.Graph").SendAsync(req, ct);
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
            return null; // Expired/revoked refresh token => reauthorize.
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Entra token refresh returned HTTP {Code}", (int)response.StatusCode);
            throw new GraphConnectionException("microsoft-unavailable");
        }
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    private static string String(JsonElement e, string key, int max)
    {
        if (!e.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
            return "";
        var result = value.GetString() ?? "";
        return result.Length > max ? result[..max] : result;
    }
    private static string Location(JsonElement e) =>
        e.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object
            ? String(location, "displayName", 200) : "";

    private static string UtcDate(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var part) || part.ValueKind != JsonValueKind.Object)
            return "";
        var raw = String(part, "dateTime", 60);
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return "";
        return parsed.ToString("O", CultureInfo.InvariantCulture);
    }
}

public sealed class GraphConnectionException(string reason) : Exception(reason)
{
    public string Reason => Message;
}
