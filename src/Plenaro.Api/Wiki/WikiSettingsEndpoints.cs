using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Favorites;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Wiki;

/// <summary>
/// One admin-controlled Wiki server for everybody. The only personal settings
/// are each user's username and AES-GCM encrypted password. The existing
/// FavoriteProxy handles all browser sessions without ever creating bookmarks.
/// </summary>
public static class WikiSettingsEndpoints
{
    private const string Slug="wiki";
    private const string ConfigKey="wiki.browser";
    private const string PasswordKey="wiki.password";
    public sealed record WikiAdminInput(string Name,string BaseUrl,string HomePath);
    public sealed record WikiAdminInfo(bool Configured,string Name,string BaseUrl,string HomePath);
    public sealed record WikiUserInfo(bool Configured,string Name,string HomePath,
        string Username,bool HasPassword,bool Ready,Guid? FavoriteId);
    public sealed record WikiLoginInput(string Username,string? Password);

    private sealed record WikiApp(Guid Id,string Name,string UpstreamUrl,bool Enabled,bool All);

    public static void MapPlenaroWiki(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/wiki").RequireAuthorization();
        var admin=group.MapGroup("/admin").RequireAuthorization("AdminOnly");

        group.MapGet("/settings",async(ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,IConfiguration config,
            SecretVault vault,CancellationToken ct)=>{
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            var wiki=await Wiki(db,ct);
            if(wiki is null||!wiki.Enabled||!wiki.All)
                return Results.Ok(new WikiUserInfo(false,"","/","",false,false,null));
            var home=await Home(db,ct);
            var personal=await Personal(db,user.Id,ct);
            var hasPassword=personal is not null&&vault.IsConfigured&&
                (await vault.ListNamesAsync("user",user.Id,ct))
                    .Contains(PasswordKey);
            if(!hasPassword&&personal is {LegacySecretName: {Length:>0} legacy}&&vault.IsConfigured)
                hasPassword=(await vault.ListNamesAsync("user",user.Id,ct)).Contains(legacy);
            var ready=personal is not null&&personal.Value.AutoLogin&&
                !string.IsNullOrWhiteSpace(personal.Value.Username)&&hasPassword;
            return Results.Ok(new WikiUserInfo(true,wiki.Name,home,
                personal?.Username??"",hasPassword,ready,
                ready?personal?.Id:null));
        });

        group.MapPut("/settings",async(WikiLoginInput input,
            HttpContext http,IAntiforgery anti,ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,IConfiguration config,
            SecretVault vault,FavoriteProxy proxy,CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(string.IsNullOrWhiteSpace(input.Username)||
               input.Username.Trim().Length>180||
               input.Username.Any(char.IsControl)||
               input.Password is {Length:>4096})
                return Results.BadRequest(new{error="Bitte gültigen Wiki-Benutzernamen eingeben."});
            if(!vault.IsConfigured)return Results.Problem(
                "Verschlüsselter Passwortspeicher nicht eingerichtet.",statusCode:503);
            await using var db=await Open(config,ct);
            var wiki=await Wiki(db,ct);
            if(wiki is null||!wiki.Enabled||!wiki.All)
                return Results.Conflict(new{error="Der Administrator hat noch keinen Wiki-Server für alle freigegeben."});
            var previous=await Personal(db,user.Id,ct);
            var names=(await vault.ListNamesAsync("user",user.Id,ct)).ToHashSet();
            var existingSecret=names.Contains(PasswordKey)||
                (previous is {LegacySecretName: {Length:>0} legacy}&&names.Contains(legacy));
            if(string.IsNullOrEmpty(input.Password)&&!existingSecret)
                return Results.BadRequest(new{error="Ein Wiki-Passwort ist erforderlich."});
            var connectionId=previous?.Id??Guid.NewGuid();
            await using(var cmd=new NpgsqlCommand("""
                INSERT INTO wiki_user_connections(id,user_id,username,legacy_secret_name)
                VALUES(@id,@user,@username,@legacy)
                ON CONFLICT(user_id) DO UPDATE SET username=EXCLUDED.username
                RETURNING id
                """,db)){
                cmd.Parameters.AddWithValue("id",connectionId);
                cmd.Parameters.AddWithValue("user",user.Id);
                cmd.Parameters.AddWithValue("username",input.Username.Trim());
                cmd.Parameters.AddWithValue("legacy",NpgsqlDbType.Varchar).Value=
                    (object?)previous?.LegacySecretName??DBNull.Value;
                connectionId=(Guid)(await cmd.ExecuteScalarAsync(ct)
                    ?? throw new InvalidOperationException("Wiki-Anmeldung konnte nicht gespeichert werden."));
            }
            if(!string.IsNullOrEmpty(input.Password)){
                await vault.SetAsync("user",user.Id,PasswordKey,input.Password,ct);
                if(previous?.LegacySecretName is {Length:>0} legacySecret)
                    await vault.DeleteAsync("user",user.Id,legacySecret,ct);
            }else if(!names.Contains(PasswordKey)&&previous is {LegacySecretName:{Length:>0} legacyName}){
                // Move existing encrypted favorite password by decrypting and
                // re-encrypting with the correct new AAD, never copying raw
                // ciphertext to a different secret name.
                var prior=await vault.GetForIntegrationAsync("user",user.Id,
                    legacyName,ct);
                if(!string.IsNullOrEmpty(prior))
                    await vault.SetAsync("user",user.Id,PasswordKey,prior,ct);
            }
            proxy.RevokeFavorite(user.Id,connectionId);
            return Results.NoContent();
        });

        // Reuses the proven FavoriteProxy, but requires this user's
        // Confluence credentials to establish a VERIFIED server-side session.
        // No fallback to another user's session or an interactive login form.
        group.MapPost("/start",async(HttpContext http,IAntiforgery anti,
            ClaimsPrincipal principal,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            if(!FavoriteProxy.IsTrustedStartOrigin(http.Request,config))
                return Results.BadRequest(new{error="Ungültige Browser-Origin."});
            await using var db=await Open(config,ct);
            var wiki=await Wiki(db,ct);
            if(wiki is null||!wiki.Enabled||!wiki.All)
                return Results.Conflict(new{error="Der Wiki-Server ist nicht für alle freigegeben."});
            var personal=await Personal(db,user.Id,ct);
            if(personal is null||
               string.IsNullOrWhiteSpace(personal.Value.Username)||!vault.IsConfigured)
                return Results.Conflict(new{
                    error="Bitte deinen Wiki-Benutzernamen und dein Passwort unter Einstellungen → Wiki hinterlegen."});
            var secretNames=(await vault.ListNamesAsync("user",user.Id,ct)).ToHashSet();
            var hasPassword=secretNames.Contains(PasswordKey);
            if(!hasPassword&&personal.Value.LegacySecretName is {Length:>0} legacy &&
               secretNames.Contains(legacy)){
                var oldPassword=await vault.GetForIntegrationAsync("user",user.Id,legacy,ct);
                if(!string.IsNullOrEmpty(oldPassword)){
                    await vault.SetAsync("user",user.Id,PasswordKey,oldPassword,ct);
                    hasPassword=true;
                }
            }
            if(!hasPassword)
                return Results.Conflict(new{
                    error="Bitte deinen Wiki-Benutzernamen und dein Passwort unter Einstellungen → Wiki hinterlegen."});
            var favorite=new FavoritesEndpoints.FavoriteInfo(
                personal.Value.Id,wiki.Id,wiki.Name,wiki.Name,"wiki",
                await Home(db,ct),personal.Value.Username,
                true,true,0,true);
            try{
                var url=await proxy.StartAsync(user.Id,favorite,
                    wiki.UpstreamUrl,vault,ct,requireWikiAutoLogin:true);
                return Results.Ok(new{url});
            }catch(OperationCanceledException) when(!ct.IsCancellationRequested){
                return Results.Problem("Wiki antwortet nicht.",statusCode:504);
            }catch(HttpRequestException){
                return Results.Problem("Wiki ist nicht erreichbar.",statusCode:502);
            }catch(InvalidOperationException){
                return Results.UnprocessableEntity(new{
                    error="Automatische Wiki-Anmeldung fehlgeschlagen. Bitte deine persönlichen Wiki-Zugangsdaten prüfen. CAPTCHA, MFA und SSO werden nicht automatisch umgangen."});
            }
        }).RequireRateLimiting("znuny-native-start");

        group.MapDelete("/settings",async(HttpContext http,IAntiforgery anti,
            ClaimsPrincipal principal,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            var wiki=await Wiki(db,ct);
            if(wiki is null)return Results.NoContent();
            var personal=await Personal(db,user.Id,ct);
            if(personal is null)return Results.NoContent();
            proxy.RevokeFavorite(user.Id,personal.Value.Id);
            if(vault.IsConfigured){
                await vault.DeleteAsync("user",user.Id,PasswordKey,ct);
                if(personal.Value.LegacySecretName is {Length:>0} old)
                    await vault.DeleteAsync("user",user.Id,old,ct);
            }
            await using var cmd=new NpgsqlCommand("""
                DELETE FROM wiki_user_connections WHERE id=@id AND user_id=@user
                """,db);
            cmd.Parameters.AddWithValue("id",personal.Value.Id);
            cmd.Parameters.AddWithValue("user",user.Id);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });

        admin.MapGet("/",async(IConfiguration config,CancellationToken ct)=>{
            await using var db=await Open(config,ct);
            var wiki=await Wiki(db,ct);
            return Results.Ok(new WikiAdminInfo(wiki is not null&&wiki.Enabled&&wiki.All,
                wiki?.Name??"",wiki?.UpstreamUrl??"",await Home(db,ct)));
        });

        admin.MapPut("/",async(WikiAdminInput input,HttpContext http,
            IAntiforgery anti,IConfiguration config,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(http,anti))return CsrfError();
            if(!ValidAdminInput(input,config,out var normalized,out var error))
                return Results.BadRequest(new{error});
            if(!proxy.BaseConfigured)
                return Results.Conflict(new{error="Der Favoriten-Proxy ist noch nicht eingerichtet."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            Guid appId;
            await using(var cmd=new NpgsqlCommand("""
                INSERT INTO favorite_apps(id,name,slug,upstream_url,enabled,available_to_all)
                VALUES(@id,@name,'wiki',@url,true,true)
                ON CONFLICT(slug) DO UPDATE SET
                    name=EXCLUDED.name,upstream_url=EXCLUDED.upstream_url,
                    enabled=true,available_to_all=true
                RETURNING id
                """,db,tx)){
                cmd.Parameters.AddWithValue("id",Guid.NewGuid());
                cmd.Parameters.AddWithValue("name",normalized.Name);
                cmd.Parameters.AddWithValue("url",normalized.BaseUrl);
                appId=(Guid)(await cmd.ExecuteScalarAsync(ct)
                    ?? throw new InvalidOperationException("Wiki-Server konnte nicht gespeichert werden."));
            }
            await using(var cmd=new NpgsqlCommand("""
                INSERT INTO system_settings(setting_key,setting_value,updated_at)
                VALUES(@key,@value,now())
                ON CONFLICT(setting_key) DO UPDATE SET
                    setting_value=EXCLUDED.setting_value,updated_at=now()
                """,db,tx)){
                cmd.Parameters.AddWithValue("key",ConfigKey);
                cmd.Parameters.AddWithValue("value",NpgsqlDbType.Jsonb,
                    JsonSerializer.Serialize(new {homePath=normalized.HomePath}));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            // Wiki credentials are stored independently of personal
            // favorites; changing the server does not touch user bookmarks.
            await tx.CommitAsync(ct);
            proxy.RevokeApp(appId);
            return Results.NoContent();
        });
    }

    private static bool ValidAdminInput(WikiAdminInput input,IConfiguration config,
        out WikiAdminInput normalized,out string error)
    {
        normalized=input;
        error="Ungültiger Wiki-Name, HTTPS-Server oder Startpfad.";
        if(input.Name is null||input.BaseUrl is null||input.HomePath is null||
           input.Name.Trim().Length is <2 or >120||
           input.HomePath.Length is <1 or >2048 ||
           !input.HomePath.StartsWith('/')||input.HomePath.StartsWith("//")||
           input.HomePath.Contains('\\')||input.HomePath.Contains('#')||
           input.HomePath.Contains("://")||input.HomePath.Any(char.IsControl)||
           !Uri.TryCreate(input.BaseUrl,UriKind.Absolute,out var uri)||
           uri.Scheme!=Uri.UriSchemeHttps ||
           uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0 ||
           uri.AbsolutePath!="/"||uri.HostNameType!=UriHostNameType.Dns ||
           string.Equals(uri.Host,
               new Uri(config["PLENARO_PUBLIC_ORIGIN"]??"https://invalid.example").Host,
               StringComparison.OrdinalIgnoreCase))
            return false;
        normalized=new WikiAdminInput(input.Name.Trim(),
            uri.GetLeftPart(UriPartial.Authority)+"/",input.HomePath);
        return true;
    }

    private static async Task<WikiApp?> Wiki(NpgsqlConnection db,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT id,name,upstream_url,enabled,available_to_all
            FROM favorite_apps WHERE slug='wiki'
            """,db);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?new WikiApp(reader.GetGuid(0),
            reader.GetString(1),reader.GetString(2),
            reader.GetBoolean(3),reader.GetBoolean(4)):null;
    }

    private static async Task<string> Home(NpgsqlConnection db,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT setting_value->>'homePath' FROM system_settings
            WHERE setting_key=@key
            """,db);
        cmd.Parameters.AddWithValue("key",ConfigKey);
        return (await cmd.ExecuteScalarAsync(ct) as string) is {Length:>0} p?p:"/";
    }

    private static async Task<(Guid Id,string Username,bool AutoLogin,
        string? LegacySecretName)?> Personal(
        NpgsqlConnection db,Guid user,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT id,username,legacy_secret_name FROM wiki_user_connections
            WHERE user_id=@user
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)?
            (reader.GetGuid(0),reader.GetString(1),true,
             reader.IsDBNull(2)?null:reader.GetString(2)):null;
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration config,
        CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }

    private static async Task<bool> ValidCsrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }

    private static IResult CsrfError()=>Results.BadRequest(
        new{error="Sicherheitstoken fehlt oder ist ungültig."});
}
