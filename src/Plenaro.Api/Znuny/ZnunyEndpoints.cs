using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Initial read-only Znuny/OTRS GenericTicketConnectorREST integration.
/// Defaults match Classic's AppSettings and ZnunySyncPolicy. Server URL and
/// routes are controlled by admins, usernames and secrets by each user.
/// There are no ticket write endpoints, polling jobs or automatic retries.
/// </summary>
public static class ZnunyEndpoints
{
    private const string GlobalKey = "tickets.znuny";
    private const string PasswordKey = "znuny.password";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // From project_tool/Models/AppSettings.cs and Models/ZnunySyncPolicy.cs
    public sealed record ServerSettings(
        string WebUrl = "https://SERVER/index.pl",
        string ApiUrl = "https://SERVER/nph-genericinterface.pl/Webservice/GenericTicketConnectorREST",
        string TicketSearchRoute = "/Ticket",
        string TicketSearchMethod = "GET",
        string TicketSearchAuthMode = "Session",
        string TicketGetRouteTemplate = "/Ticket/{TicketID}",
        string TicketGetMethod = "GET",
        string TicketGetAuthMode = "Session",
        string TicketUpdateRoute = "/Ticket/{TicketID}/Update",
        string AgentListRoute = "/Ticket/Agent/List",
        string TicketCreateRoute = "/TicketCreate",
        string TicketCreateMethod = "POST",
        string TicketCreateQueue = "",
        string TicketCreateState = "open",
        string TicketCreatePriority = "3 normal",
        string TicketCreateType = "",
        string TicketCreateCustomerUser = "",
        string TicketReplyTemplate = "",
        string DynamicFieldOptionsRoute = "/Ticket/DynamicField/{FieldName}/Options",
        string CostCenterFieldName = "KostenstelleID",
        string OrderFieldName = "AuftragsID",
        string CostCenterOptions = "",
        string OrderOptions = "",
        int SyncIntervalMinutes = 15,
        int CandidateSyncIntervalMinutes = 5,
        int SearchLimit = 100,
        int ArticleLimit = 20,
        bool OnlyOpenTickets = true,
        bool ShowClosedTickets = false
    );

    public sealed record PersonalSettings(
        string Username = "",
        int AgentId = 0,
        int CandidateUserId = 1,
        string CandidateKeywords = "",
        string CandidateExcludeKeywords = "",
        bool IncludeOwner = true,
        bool IncludeResponsible = true,
        bool NotifyOnNewAssignedTickets = true,
        bool NotifyOnTicketChanges = true,
        bool ShowAllCandidatePoolTickets = false
    );

    public sealed record PersonalView(
        string Username, int AgentId, int CandidateUserId,
        string CandidateKeywords, string CandidateExcludeKeywords,
        bool IncludeOwner, bool IncludeResponsible, bool NotifyOnNewAssignedTickets,
        bool NotifyOnTicketChanges, bool ShowAllCandidatePoolTickets,
        bool PasswordConfigured, bool VaultReady);

    public sealed record PasswordInput(string Password);

    public static void MapPlenaroZnuny(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/znuny").RequireAuthorization();
        var admin = app.MapGroup("/api/v1/admin/znuny").RequireAuthorization("AdminOnly");

        group.MapGet("/server", async (HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            if (await OwnerAsync(http, users) is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            return Results.Ok(await LoadServerAsync(db, ct));
        });

        admin.MapPut("/server", async (ServerSettings settings, HttpContext http,
            IAntiforgery anti, IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            if (ValidateServer(settings) is { } error)
                return Results.BadRequest(new { error });
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO system_settings(setting_key,setting_value)
                VALUES(@key,@payload)
                ON CONFLICT(setting_key) DO UPDATE
                SET setting_value=EXCLUDED.setting_value, updated_at=now()
                """, db);
            cmd.Parameters.AddWithValue("key", GlobalKey);
            cmd.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value =
                JsonSerializer.Serialize(settings, Json);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        admin.MapDelete("/server", async (HttpContext http, IAntiforgery anti,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                DELETE FROM system_settings WHERE setting_key=@key
                """,db);
            cmd.Parameters.AddWithValue("key",GlobalKey);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/me", async (HttpContext http, UserManager<ApplicationUser> users,
            IConfiguration config, SecretVault vault, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            var settings = await LoadPersonalAsync(db, owner.Value, ct);
            var hasPassword = vault.IsConfigured &&
                (await vault.ListNamesAsync("user",owner.Value,ct)).Contains(PasswordKey);
            return Results.Ok(new PersonalView(
                settings.Username, settings.AgentId, settings.CandidateUserId,
                settings.CandidateKeywords, settings.CandidateExcludeKeywords,
                settings.IncludeOwner, settings.IncludeResponsible,
                settings.NotifyOnNewAssignedTickets, settings.NotifyOnTicketChanges,
                settings.ShowAllCandidatePoolTickets,
                hasPassword,vault.IsConfigured));
        });

        group.MapPut("/me", async (PersonalSettings input, HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (ValidatePersonal(input) is { } error)
                return Results.BadRequest(new { error });
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO znuny_user_settings(user_id,username,agent_id,candidate_user_id,
                    candidate_keywords,candidate_exclude_keywords,
                    include_owner,include_responsible,notify_new_assigned,
                    notify_ticket_changes,show_all_candidate_pool)
                VALUES(@owner,@username,@agent,@candidate,@keywords,@excluded,@owner_role,
                    @responsible,@notify_new,@notify_changes,@show_all)
                ON CONFLICT(user_id) DO UPDATE SET
                    username=EXCLUDED.username, agent_id=EXCLUDED.agent_id,
                    candidate_user_id=EXCLUDED.candidate_user_id,
                    candidate_keywords=EXCLUDED.candidate_keywords,
                    candidate_exclude_keywords=EXCLUDED.candidate_exclude_keywords,
                    include_owner=EXCLUDED.include_owner,
                    include_responsible=EXCLUDED.include_responsible,
                    notify_new_assigned=EXCLUDED.notify_new_assigned,
                    notify_ticket_changes=EXCLUDED.notify_ticket_changes,
                    show_all_candidate_pool=EXCLUDED.show_all_candidate_pool,updated_at=now()
                """, db);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("username", input.Username.Trim());
            cmd.Parameters.AddWithValue("agent", input.AgentId);
            cmd.Parameters.AddWithValue("candidate", input.CandidateUserId);
            cmd.Parameters.AddWithValue("keywords", input.CandidateKeywords.Trim());
            cmd.Parameters.AddWithValue("excluded", input.CandidateExcludeKeywords.Trim());
            cmd.Parameters.AddWithValue("owner_role", input.IncludeOwner);
            cmd.Parameters.AddWithValue("responsible", input.IncludeResponsible);
            cmd.Parameters.AddWithValue("notify_new", input.NotifyOnNewAssignedTickets);
            cmd.Parameters.AddWithValue("notify_changes", input.NotifyOnTicketChanges);
            cmd.Parameters.AddWithValue("show_all", input.ShowAllCandidatePoolTickets);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        group.MapPut("/password", async (PasswordInput input, HttpContext http,
            UserManager<ApplicationUser> users, SecretVault vault, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (!vault.IsConfigured)
                return Results.Problem("Secret-Vault nicht eingerichtet.", statusCode:503);
            if (input.Password is not { Length: >= 1 and <= 4096 })
                return Results.BadRequest(new { error = "Ungültige Passwortlänge." });
            await vault.SetAsync("user",owner.Value,PasswordKey,input.Password,ct);
            return Results.NoContent();
        });

        group.MapDelete("/password", async (HttpContext http,
            UserManager<ApplicationUser> users, SecretVault vault, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http,users);
            if (owner is null) return Results.Unauthorized();
            if (!vault.IsConfigured)
                return Results.Problem("Secret-Vault nicht eingerichtet.", statusCode:503);
            await vault.DeleteAsync("user",owner.Value,PasswordKey,ct);
            return Results.NoContent();
        });

        // A single explicit login test. Never return or cache a remote SessionID.
        // Admin determines target URL; normal users cannot choose an SSRF target.
        group.MapPost("/connection-test", async (HttpContext http,
            UserManager<ApplicationUser> users, IConfiguration config,
            SecretVault vault, ZnunyConnectionTester tester, IAntiforgery anti,
            CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, anti)) return CsrfError();
            var owner = await OwnerAsync(http,users);
            if (owner is null) return Results.Unauthorized();
            if (!vault.IsConfigured)
                return Results.Problem("Secret-Vault nicht eingerichtet.",statusCode:503);
            await using var db = await OpenAsync(config,ct);
            var server = await LoadServerAsync(db,ct);
            var personal = await LoadPersonalAsync(db,owner.Value,ct);
            if (string.IsNullOrWhiteSpace(personal.Username))
                return Results.BadRequest(new { error="Bitte zuerst einen Znuny-Benutzernamen hinterlegen." });
            var password = await vault.GetForIntegrationAsync("user",owner.Value,PasswordKey,ct);
            if (string.IsNullOrWhiteSpace(password))
                return Results.BadRequest(new { error="Bitte zuerst das Znuny-Passwort speichern." });
            var error = ValidateServer(server);
            var isPlaceholder = Uri.TryCreate(server.ApiUrl, UriKind.Absolute, out var endpointUri)
                && endpointUri.Host.Equals("SERVER", StringComparison.OrdinalIgnoreCase);
            if (error is not null || isPlaceholder)
                return Results.BadRequest(new { error=error ??
                    "Bitte zuerst die Znuny-API-URL auf den tatsächlichen Server ändern." });
            return Results.Ok(await tester.TrySessionAsync(server.ApiUrl,personal.Username,password,ct));
        }).RequireRateLimiting("znuny-test");
    }

    internal static string? ValidateServer(ServerSettings s)
    {
        if (!ValidateUrl(s.ApiUrl) || !ValidateUrl(s.WebUrl))
            return "Znuny-URLs müssen vollständige HTTPS-Adressen ohne Zugangsdaten, Query oder Fragment sein.";
        string[] routes = [
            s.TicketSearchRoute,s.TicketGetRouteTemplate,s.TicketUpdateRoute,
            s.AgentListRoute,s.TicketCreateRoute,s.DynamicFieldOptionsRoute
        ];
        if (routes.Any(x => x is null || x.Length is < 2 or > 160 ||
            !x.StartsWith('/') || x.StartsWith("//",StringComparison.Ordinal) ||
            x.Contains('?') || x.Contains('#') || x.Contains('\\') ||
            x.Contains("://",StringComparison.Ordinal) ||
            x.Split('/').Any(p => p is "." or "..")))
            return "Ungültige relative API-Route.";
        if (s.TicketSearchMethod is not ("GET" or "POST") ||
            s.TicketGetMethod is not ("GET" or "POST") ||
            s.TicketCreateMethod is not ("POST" or "GET") ||
            s.TicketSearchAuthMode != "Session" ||
            s.TicketGetAuthMode != "Session")
            return "HTTP-Methode oder Authentifizierung ungültig; nur Session-Authentifizierung unterstützt.";
        if (!s.TicketGetRouteTemplate.Contains("{TicketID}",StringComparison.Ordinal) ||
            !s.TicketUpdateRoute.Contains("{TicketID}",StringComparison.Ordinal) ||
            !s.DynamicFieldOptionsRoute.Contains("{FieldName}",StringComparison.Ordinal))
            return "Die API-Routenvorlagen benötigen TicketID/FieldName-Platzhalter.";
        if (s.SyncIntervalMinutes is < 5 or > 1440 ||
            s.CandidateSyncIntervalMinutes is < 3 or > 60 ||
            s.SearchLimit is < 10 or > 500 ||
            s.ArticleLimit is < 1 or > 100)
            return "Synchronisationsintervalle oder Such-/Artikellimits außerhalb der Classic-Grenzen.";
        string[] texts = [s.TicketCreateQueue,s.TicketCreateState,s.TicketCreatePriority,
            s.TicketCreateType,s.TicketCreateCustomerUser,s.TicketReplyTemplate,
            s.CostCenterFieldName,s.OrderFieldName,s.CostCenterOptions,s.OrderOptions];
        if (texts.Any(t => t is null || t.Length > 2000))
            return "Ticketsystem-Textwerte dürfen maximal 2.000 Zeichen haben.";
        return null;
    }

    private static bool ValidateUrl(string? input)
    {
        if (input is null || input.Length > 2048 ||
            !Uri.TryCreate(input,UriKind.Absolute,out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            uri.HostNameType == UriHostNameType.Unknown ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
            return false;
        // Local domain suffixes are valid for self-hosted Znuny (after DNS peer
        // validation), but localhost must never become a test target.
        if (uri.Host.Equals("localhost",StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase))
            return false;
        if (IPAddress.TryParse(uri.Host,out var ip))
            return IsRoutableTarget(ip);
        return true;
    }

    private static bool IsRoutableTarget(IPAddress ip) =>
        !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any) &&
        !ip.Equals(IPAddress.IPv6Any) && !ip.IsIPv6Multicast &&
        !ip.IsIPv6LinkLocal && !(ip.AddressFamily == AddressFamily.InterNetwork &&
          (ip.GetAddressBytes()[0] is 0 or 127 or >= 224 ||
           (ip.GetAddressBytes()[0] == 169 && ip.GetAddressBytes()[1] == 254)));

    private static string? ValidatePersonal(PersonalSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.Username) || s.Username.Length > 256 ||
            s.Username.Any(c=>char.IsControl(c)))
            return "Bitte einen gültigen Znuny-Benutzernamen angeben.";
        if (s.AgentId < 0 || s.CandidateUserId < 1)
            return "Ungültige Agenten-/Kandidaten-ID.";
        if (s.CandidateKeywords is null || s.CandidateKeywords.Length > 2000 ||
            s.CandidateExcludeKeywords is null || s.CandidateExcludeKeywords.Length > 2000)
            return "Filter- oder Ausschlusswörter sind zu lang.";
        return null;
    }

    private static async Task<ServerSettings> LoadServerAsync(NpgsqlConnection db,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT setting_value::text FROM system_settings WHERE setting_key=@key
            """,db);
        cmd.Parameters.AddWithValue("key",GlobalKey);
        var value = await cmd.ExecuteScalarAsync(ct) as string;
        return value is null ? new ServerSettings() :
            JsonSerializer.Deserialize<ServerSettings>(value,Json) ?? new ServerSettings();
    }

    private static async Task<PersonalSettings> LoadPersonalAsync(
        NpgsqlConnection db,Guid owner,CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT username,agent_id,candidate_user_id,candidate_keywords,
                candidate_exclude_keywords,include_owner,include_responsible,
                notify_new_assigned,notify_ticket_changes,show_all_candidate_pool
            FROM znuny_user_settings WHERE user_id=@owner
            """,db);
        cmd.Parameters.AddWithValue("owner",owner);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new PersonalSettings(
            reader.GetString(0),reader.GetInt32(1),reader.GetInt32(2),
            reader.GetString(3),reader.GetString(4),reader.GetBoolean(5),
            reader.GetBoolean(6),reader.GetBoolean(7),reader.GetBoolean(8),
            reader.GetBoolean(9)) : new PersonalSettings();
    }

    private static async Task<Guid?> OwnerAsync(HttpContext http,
        UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(http.User);
        return user is null || user.IsDisabled ? null : user.Id;
    }
    private static async Task<NpgsqlConnection> OpenAsync(IConfiguration config,CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);return db;
    }
    private static async Task<bool> ValidCsrfAsync(HttpContext http, IAntiforgery anti)
    {
        try { await anti.ValidateRequestAsync(http);return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult CsrfError() =>
        Results.BadRequest(new { error="Sicherheitstoken fehlt oder ist ungültig." });
}
