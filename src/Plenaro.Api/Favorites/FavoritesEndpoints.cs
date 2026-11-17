using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;
using Plenaro.Api.Secrets;

namespace Plenaro.Api.Favorites;

/// <summary>Admin-approved HTTPS applications and personal Classic-style favorites.</summary>
public static class FavoritesEndpoints
{
    public sealed record AppInput(string Name,string Slug,string UpstreamUrl,
        bool Enabled,bool AvailableToAll,Guid[]? AllowedUsers);
    public sealed record PersonalSiteInput(string Name,string Url,string Username,
        bool AutoLogin,bool Enabled,int SortOrder,string? Password,
        bool ClearPassword=false);
    public sealed record FavoriteOrderInput(Guid[] Ids);
    public sealed record FavoriteInput(Guid AppId,string Name,string Path,
        string Username,bool AutoLogin,bool Enabled,int SortOrder,string? Password,bool ClearPassword=false);
    public sealed record AppInfo(Guid Id,string Name,string Slug,bool Enabled,
        bool AvailableToAll,string? UpstreamUrl,Guid[]? AllowedUsers);
    public sealed record FavoriteInfo(Guid Id,Guid AppId,string Name,string AppName,
        string Slug,string Path,string Username,bool AutoLogin,bool Enabled,
        int SortOrder,bool HasPassword,string? Url=null);

    public static void MapPlenaroFavorites(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/favorites").RequireAuthorization();
        var admin=app.MapGroup("/api/v1/admin/favorite-apps")
            .RequireAuthorization("AdminOnly");

        // Personal website favorites are first-class and do NOT require a
        // pre-approved admin application. Each site gets its own isolated
        // random proxy slug; its origin is immutable to other users.
        group.MapPost("/personal",async(PersonalSiteInput input,HttpContext ctx,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            var id=Guid.NewGuid();
            var appId=Guid.NewGuid();
            var slug="site-"+id.ToString("N");
            if(!ValidPersonalSite(input,slug,config,out var origin,out var path,
                out var error))return Results.BadRequest(new{error});
            if(input.Password is {Length:>0}&&!vault.IsConfigured)
                return Results.Problem("Verschlüsselter Speicher ist nicht eingerichtet.",statusCode:503);
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await using(var count=new NpgsqlCommand("""
                SELECT COUNT(*) FROM user_favorites
                WHERE user_id=@uid
                """,db,tx)){
                count.Parameters.AddWithValue("uid",user.Id);
                if(Convert.ToInt32(await count.ExecuteScalarAsync(ct))>=40)
                    return Results.Conflict(new{error="Maximal 40 persönliche Webseiten pro Benutzer."});
            }
            await using(var appCmd=new NpgsqlCommand("""
                INSERT INTO favorite_apps(id,name,slug,upstream_url,
                    enabled,available_to_all,owner_user_id)
                VALUES(@app,@name,@slug,@origin,true,false,@user)
                """,db,tx)){
                appCmd.Parameters.AddWithValue("app",appId);
                appCmd.Parameters.AddWithValue("name",input.Name.Trim());
                appCmd.Parameters.AddWithValue("slug",slug);
                appCmd.Parameters.AddWithValue("origin",origin);
                appCmd.Parameters.AddWithValue("user",user.Id);
                await appCmd.ExecuteNonQueryAsync(ct);
            }
            await using(var favCmd=new NpgsqlCommand("""
                INSERT INTO user_favorites(id,user_id,app_id,name,path,
                    username,auto_login,enabled,sort_order)
                VALUES(@id,@user,@app,@name,@path,@username,@auto,@enabled,@ord)
                """,db,tx)){
                favCmd.Parameters.AddWithValue("id",id);
                favCmd.Parameters.AddWithValue("user",user.Id);
                favCmd.Parameters.AddWithValue("app",appId);
                favCmd.Parameters.AddWithValue("name",input.Name.Trim());
                favCmd.Parameters.AddWithValue("path",path);
                favCmd.Parameters.AddWithValue("username",input.Username.Trim());
                favCmd.Parameters.AddWithValue("auto",input.AutoLogin);
                favCmd.Parameters.AddWithValue("enabled",input.Enabled);
                favCmd.Parameters.AddWithValue("ord",input.SortOrder);
                await favCmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            if(!string.IsNullOrEmpty(input.Password))
                await vault.SetAsync("user",user.Id,PasswordName(id),input.Password,ct);
            return Results.Ok(new{id});
        });

        group.MapPut("/personal/order",async(FavoriteOrderInput input,
            HttpContext ctx,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            if(input.Ids is null||input.Ids.Length>40||
               input.Ids.Distinct().Count()!=input.Ids.Length)
                return Results.BadRequest(new{error="Ungültige Sortierreihenfolge."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await using var check=new NpgsqlCommand("""
                SELECT id FROM user_favorites WHERE user_id=@user
                ORDER BY id FOR UPDATE
                """,db,tx);
            check.Parameters.AddWithValue("user",user.Id);
            var owned=new HashSet<Guid>();
            await using(var cursor=await check.ExecuteReaderAsync(ct)){
                while(await cursor.ReadAsync(ct))owned.Add(cursor.GetGuid(0));
            }
            if(owned.Count!=input.Ids.Length||!input.Ids.All(owned.Contains))
                return Results.BadRequest(new{error="Sortierung enthält fremde oder fehlende Favoriten."});
            for(var i=0;i<input.Ids.Length;i++){
                await using var update=new NpgsqlCommand("""
                    UPDATE user_favorites SET sort_order=@order
                    WHERE id=@id AND user_id=@user
                    """,db,tx);
                update.Parameters.AddWithValue("order",i);
                update.Parameters.AddWithValue("id",input.Ids[i]);
                update.Parameters.AddWithValue("user",user.Id);
                await update.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });

        group.MapPut("/personal/{id:guid}",async(Guid id,PersonalSiteInput input,
            HttpContext ctx,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            if(input.Password is {Length:>0}&&!vault.IsConfigured)
                return Results.Problem("Verschlüsselter Speicher ist nicht eingerichtet.",statusCode:503);
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            Guid appId;
            string slug;
            await using(var lookup=new NpgsqlCommand("""
                SELECT a.id,a.slug FROM user_favorites f
                JOIN favorite_apps a ON a.id=f.app_id
                WHERE f.id=@id AND f.user_id=@user AND a.owner_user_id=@user
                FOR UPDATE OF f,a
                """,db,tx)){
                lookup.Parameters.AddWithValue("id",id);
                lookup.Parameters.AddWithValue("user",user.Id);
                await using var cursor=await lookup.ExecuteReaderAsync(ct);
                if(!await cursor.ReadAsync(ct))return Results.NotFound();
                appId=cursor.GetGuid(0);slug=cursor.GetString(1);
            }
            if(!ValidPersonalSite(input,slug,config,out var origin,out var path,
                out var error))return Results.BadRequest(new{error});
            await using(var appCmd=new NpgsqlCommand("""
                UPDATE favorite_apps SET name=@name,upstream_url=@url
                WHERE id=@app AND owner_user_id=@user
                """,db,tx)){
                appCmd.Parameters.AddWithValue("app",appId);
                appCmd.Parameters.AddWithValue("user",user.Id);
                appCmd.Parameters.AddWithValue("name",input.Name.Trim());
                appCmd.Parameters.AddWithValue("url",origin);
                await appCmd.ExecuteNonQueryAsync(ct);
            }
            await using(var cmd=new NpgsqlCommand("""
                UPDATE user_favorites SET name=@name,path=@path,username=@username,
                    auto_login=@auto,enabled=@enabled,sort_order=@sort
                WHERE id=@id AND user_id=@user AND app_id=@app
                """,db,tx)){
                cmd.Parameters.AddWithValue("id",id);
                cmd.Parameters.AddWithValue("user",user.Id);
                cmd.Parameters.AddWithValue("app",appId);
                cmd.Parameters.AddWithValue("name",input.Name.Trim());
                cmd.Parameters.AddWithValue("path",path);
                cmd.Parameters.AddWithValue("username",input.Username.Trim());
                cmd.Parameters.AddWithValue("auto",input.AutoLogin);
                cmd.Parameters.AddWithValue("enabled",input.Enabled);
                cmd.Parameters.AddWithValue("sort",input.SortOrder);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            if(input.ClearPassword&&vault.IsConfigured)
                await vault.DeleteAsync("user",user.Id,PasswordName(id),ct);
            else if(input.Password is {Length:>0})
                await vault.SetAsync("user",user.Id,PasswordName(id),input.Password,ct);
            proxy.RevokeFavorite(user.Id,id);
            return Results.NoContent();
        });

        group.MapDelete("/personal/{id:guid}",async(Guid id,
            HttpContext ctx,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                DELETE FROM favorite_apps a
                USING user_favorites f
                WHERE f.id=@id AND f.user_id=@user AND f.app_id=a.id
                    AND a.owner_user_id=@user
                RETURNING a.id
                """,db);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("user",user.Id);
            var deleted=await cmd.ExecuteScalarAsync(ct);
            if(deleted is not Guid appId)return Results.NotFound();
            proxy.RevokeApp(appId);
            proxy.RevokeFavorite(user.Id,id);
            if(vault.IsConfigured)
                await vault.DeleteAsync("user",user.Id,PasswordName(id),ct);
            return Results.NoContent();
        });

        group.MapGet("/apps",async(HttpContext ctx,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct)=>{
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT a.id,a.name,a.slug FROM favorite_apps a
                WHERE a.enabled AND (a.available_to_all OR
                  EXISTS (SELECT 1 FROM favorite_app_users access
                    WHERE access.app_id=a.id AND access.user_id=@uid))
                ORDER BY a.name
                """,db);
            cmd.Parameters.AddWithValue("uid",user.Id);
            var list=new List<object>();
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
                list.Add(new{id=reader.GetGuid(0),name=reader.GetString(1),
                    slug=reader.GetString(2)});
            return Results.Ok(list);
        });

        group.MapGet("/",async(HttpContext ctx,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,CancellationToken ct)=>{
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            var data=await LoadFavorites(db,user.Id,ct);
            // A presence bit is enough: encrypted passwords stay exclusively
            // on the server and are never returned to clients.
            var secretNames=vault.IsConfigured?
                (await vault.ListNamesAsync("user",user.Id,ct)).ToHashSet():
                new HashSet<string>();
            return Results.Ok(data.Select(x=>x with{
                HasPassword=secretNames.Contains(PasswordName(x.Id))}));
        });

        group.MapPost("/",async(FavoriteInput input,HttpContext ctx,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            if(!ValidFavorite(input,out var error))return Results.BadRequest(new{error});
            if(input.Password is { Length: >0 }&&!vault.IsConfigured)
                return Results.Problem("Verschlüsselter Speicher ist nicht eingerichtet.",statusCode:503);
            await using var db=await Open(config,ct);
            if(!await AppAllowed(db,user.Id,input.AppId,ct))
                return Results.Forbid();
            var id=Guid.NewGuid();
            try{
            await using(var cmd=new NpgsqlCommand("""
                INSERT INTO user_favorites(id,user_id,app_id,name,path,username,
                    auto_login,enabled,sort_order)
                VALUES(@id,@user,@app,@name,@path,@username,@auto,@enabled,@ord)
                """,db)){
                BindFavorite(cmd,id,user.Id,input);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            }catch(PostgresException e) when(e.SqlState==PostgresErrorCodes.UniqueViolation){
                return Results.Conflict(new{
                    error="Für diese Anwendung existiert bereits ein Favorit. "+
                        "Pro Benutzer ist eine getrennte Anmeldung je Anwendung möglich."
                });
            }
            if(!string.IsNullOrEmpty(input.Password))
                await vault.SetAsync("user",user.Id,PasswordName(id),input.Password,ct);
            return Results.Ok(new{id});
        });

        group.MapPut("/{id:guid}",async(Guid id,FavoriteInput input,
            HttpContext ctx,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            if(!ValidFavorite(input,out var error))return Results.BadRequest(new{error});
            if(input.Password is { Length: >0 }&&!vault.IsConfigured)
                return Results.Problem("Verschlüsselter Speicher ist nicht eingerichtet.",statusCode:503);
            await using var db=await Open(config,ct);
            if(!await AppAllowed(db,user.Id,input.AppId,ct))return Results.Forbid();
            await using(var cmd=new NpgsqlCommand("""
                UPDATE user_favorites SET app_id=@app,name=@name,path=@path,
                    username=@username,auto_login=@auto,enabled=@enabled,
                    sort_order=@ord WHERE id=@id AND user_id=@user AND app_id=@app
                """,db)){
                BindFavorite(cmd,id,user.Id,input);
                if(await cmd.ExecuteNonQueryAsync(ct)==0)return Results.NotFound();
            }
            if(input.ClearPassword&&vault.IsConfigured)
                await vault.DeleteAsync("user",user.Id,PasswordName(id),ct);
            else if(input.Password is { Length: >0 })
                await vault.SetAsync("user",user.Id,PasswordName(id),input.Password,ct);
            proxy.RevokeFavorite(user.Id,id);
            return Results.NoContent();
        });

        group.MapDelete("/{id:guid}",async(Guid id,HttpContext ctx,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                DELETE FROM user_favorites WHERE id=@id AND user_id=@uid
                """,db);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("uid",user.Id);
            if(await cmd.ExecuteNonQueryAsync(ct)==0)return Results.NotFound();
            proxy.RevokeFavorite(user.Id,id);
            if(vault.IsConfigured)
                await vault.DeleteAsync("user",user.Id,PasswordName(id),ct);
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/start",async(Guid id,HttpContext ctx,
            IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,SecretVault vault,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            var user=await CurrentUser(ctx,users);
            if(user is null)return Results.Unauthorized();
            if(!proxy.BaseConfigured)return Results.Conflict(new{
                error="Wildcard-Domain für Favoriten noch nicht konfiguriert."});
            if(!FavoriteProxy.IsTrustedStartOrigin(ctx.Request,config))
                return Results.BadRequest(new{error="Ungültige Browser-Origin."});
            await using var db=await Open(config,ct);
            var favorites=await LoadFavorites(db,user.Id,ct);
            var favorite=favorites.FirstOrDefault(x=>x.Id==id && x.Enabled);
            if(favorite is null)return Results.NotFound();
            var upstream=await Upstream(db,user.Id,favorite.AppId,ct);
            if(upstream is null)return Results.Forbid();
            try{
                var url=await proxy.StartAsync(user.Id,favorite,upstream,vault,ct);
                return Results.Ok(new{url});
            }catch(HttpRequestException){
                return Results.Problem("Freigegebene Webseite nicht erreichbar.",statusCode:502);
            }catch(OperationCanceledException) when(!ct.IsCancellationRequested){
                return Results.Problem("Webseite antwortet nicht.",statusCode:504);
            }catch(InvalidOperationException){
                return Results.Problem("Favoriten-Sitzung konnte nicht gestartet werden.",statusCode:503);
            }
        }).RequireRateLimiting("znuny-native-start");

        admin.MapGet("/",async(IConfiguration config,CancellationToken ct)=>{
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT a.id,a.name,a.slug,a.upstream_url,a.enabled,a.available_to_all,
                  ARRAY(SELECT user_id FROM favorite_app_users
                    WHERE app_id=a.id ORDER BY user_id)
                FROM favorite_apps a ORDER BY a.name
                """,db);
            var data=new List<AppInfo>();
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
                data.Add(new(reader.GetGuid(0),reader.GetString(1),reader.GetString(2),
                    reader.GetBoolean(4),reader.GetBoolean(5),reader.GetString(3),
                    reader.GetFieldValue<Guid[]>(6)));
            return Results.Ok(data);
        });

        admin.MapPost("/",async(AppInput input,HttpContext ctx,
            IAntiforgery anti,IConfiguration config,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            if(!ValidApp(input,config,out var error))
                return Results.BadRequest(new{error});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            var id=Guid.NewGuid();
            await using(var cmd=new NpgsqlCommand("""
                INSERT INTO favorite_apps(id,name,slug,upstream_url,enabled,available_to_all)
                VALUES(@id,@name,@slug,@url,@enabled,@all)
                """,db,tx)){
                BindApp(cmd,id,input);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await SetAcl(db,tx,id,input.AllowedUsers,ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new{id});
        });

        admin.MapPut("/{id:guid}",async(Guid id,AppInput input,
            HttpContext ctx,IAntiforgery anti,IConfiguration config,
            FavoriteProxy proxy,CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            if(!ValidApp(input,config,out var error))
                return Results.BadRequest(new{error});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await using(var cmd=new NpgsqlCommand("""
                UPDATE favorite_apps SET name=@name,slug=@slug,
                    enabled=@enabled,available_to_all=@all
                WHERE id=@id AND upstream_url=@url
                """,db,tx)){
                BindApp(cmd,id,input);
                if(await cmd.ExecuteNonQueryAsync(ct)==0)return Results.NotFound();
            }
            await SetAcl(db,tx,id,input.AllowedUsers,ct);
            await tx.CommitAsync(ct);
            proxy.RevokeApp(id);
            return Results.NoContent();
        });

        admin.MapDelete("/{id:guid}",async(Guid id,HttpContext ctx,
            IAntiforgery anti,IConfiguration config,FavoriteProxy proxy,
            CancellationToken ct)=>{
            if(!await ValidCsrf(ctx,anti))return CsrfError();
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            // App deletion also clears encrypted user credentials tied to
            // bookmarks that the FK cascade will remove.
            await using(var secrets=new NpgsqlCommand("""
                DELETE FROM encrypted_secrets secret
                USING user_favorites favorite
                WHERE favorite.app_id=@app AND
                    secret.scope='user' AND
                    secret.owner_user_id=favorite.user_id AND
                    secret.secret_name='fav.' ||
                        replace(favorite.id::text,'-','') || '.password'
                """,db,tx)){
                secrets.Parameters.AddWithValue("app",id);
                await secrets.ExecuteNonQueryAsync(ct);
            }
            await using(var cmd=new NpgsqlCommand(
                "DELETE FROM favorite_apps WHERE id=@id",db,tx)){
                cmd.Parameters.AddWithValue("id",id);
                if(await cmd.ExecuteNonQueryAsync(ct)==0)return Results.NotFound();
            }
            await tx.CommitAsync(ct);
            proxy.RevokeApp(id);
            return Results.NoContent();
        });
    }

    internal static string PasswordName(Guid id)=>"fav."+id.ToString("N")+".password";

    private static async Task<ApplicationUser?> CurrentUser(HttpContext ctx,
        UserManager<ApplicationUser> users){
        var user=await users.GetUserAsync(ctx.User);
        return user is { IsDisabled:false } ? user:null;
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration configuration,
        CancellationToken ct){
        var db=new NpgsqlConnection(configuration.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }

    internal static async Task<bool> AppAllowed(NpgsqlConnection db,Guid user,
        Guid appId,CancellationToken ct){
        await using var cmd=new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM favorite_apps a WHERE a.id=@app
              AND a.enabled AND (a.available_to_all OR
              EXISTS(SELECT 1 FROM favorite_app_users access
                WHERE access.app_id=a.id AND access.user_id=@uid)))
            """,db);
        cmd.Parameters.AddWithValue("app",appId);
        cmd.Parameters.AddWithValue("uid",user);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<string?> Upstream(NpgsqlConnection db,Guid user,
        Guid app,CancellationToken ct){
        await using var cmd=new NpgsqlCommand("""
            SELECT upstream_url FROM favorite_apps a WHERE a.id=@app
              AND a.enabled AND (a.available_to_all OR
              EXISTS(SELECT 1 FROM favorite_app_users access
                  WHERE access.app_id=a.id AND access.user_id=@uid))
            """,db);
        cmd.Parameters.AddWithValue("app",app);
        cmd.Parameters.AddWithValue("uid",user);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    internal static async Task<List<FavoriteInfo>> LoadFavorites(
        NpgsqlConnection db,Guid user,CancellationToken ct){
        await using var cmd=new NpgsqlCommand("""
            SELECT f.id,f.app_id,f.name,a.name,a.slug,f.path,f.username,
                f.auto_login,f.enabled,f.sort_order
            FROM user_favorites f JOIN favorite_apps a ON a.id=f.app_id
            WHERE f.user_id=@uid AND a.enabled AND
              (a.available_to_all OR EXISTS(
              SELECT 1 FROM favorite_app_users access
              WHERE access.app_id=a.id AND access.user_id=@uid))
            ORDER BY f.sort_order,f.created_at,f.id
            """,db);
        cmd.Parameters.AddWithValue("uid",user);
        var favorites=new List<FavoriteInfo>();
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
            favorites.Add(new(reader.GetGuid(0),reader.GetGuid(1),
                reader.GetString(2),reader.GetString(3),reader.GetString(4),
                reader.GetString(5),reader.GetString(6),reader.GetBoolean(7),
                reader.GetBoolean(8),reader.GetInt32(9),false));
        return favorites;
    }

    private static bool ValidPersonalSite(PersonalSiteInput input,string slug,
        IConfiguration config,out string origin,out string path,out string error)
    {
        origin="";path="";
        error="Bitte eine gültige HTTPS-Webadresse mit DNS-Host eingeben.";
        if(input.Name is null||input.Name.Trim().Length is <2 or >120||
           input.Url is null||input.Url.Length is < 9 or >2048||
           input.Username is null||input.Username.Length>180||
           input.Username.Any(char.IsControl)||input.SortOrder is <0 or >10000||
           input.Password is {Length:>4096}||
           (input.ClearPassword&&!string.IsNullOrEmpty(input.Password))||
           (input.AutoLogin&&string.IsNullOrWhiteSpace(input.Username)))
            return false;
        if(!Uri.TryCreate(input.Url.Trim(),UriKind.Absolute,out var url)||
           url.Scheme!=Uri.UriSchemeHttps||url.HostNameType!=UriHostNameType.Dns||
           !string.IsNullOrEmpty(url.UserInfo)||url.Fragment.Length>0)
            return false;
        origin=url.GetLeftPart(UriPartial.Authority)+"/";
        path=url.PathAndQuery;
        if(path.Length is <1 or >2048||path.StartsWith("//")||
           path.Contains('\\')||path.Contains("://")||
           path.Any(char.IsControl))return false;
        // Preserve the existing self-proxy, DNS and reserved-host checks.
        // User-provided DNS still gets checked again at TCP connect time.
        return ValidApp(new AppInput(input.Name,slug,origin,true,false,null),
            config,out error);
    }

    private static bool ValidFavorite(FavoriteInput value,out string error){
        error="Favorit enthält ungültige Angaben.";
        if(value.Name is null||value.Name.Trim().Length is <1 or >120||
            value.Username is null||value.Username.Length>180||
            value.Path is null||value.Path.Length is <1 or >2048||
            !value.Path.StartsWith('/')||value.Path.StartsWith("//")||
            value.Path.Contains('\\')||value.Path.Any(char.IsControl)||
            value.Path.Contains('#')||value.Path.Contains("://")||
            value.SortOrder is <0 or >10000||
            (value.Password is { Length: >4096 } ||
             (value.ClearPassword && !string.IsNullOrEmpty(value.Password))))return false;
        if(value.AutoLogin&&string.IsNullOrWhiteSpace(value.Username)){
            error="Automatische Anmeldung erfordert einen Benutzernamen.";
            return false;
        }
        return true;
    }

    private static bool ValidApp(AppInput input,IConfiguration config,out string error){
        error="Ungültiger Name, Slug oder HTTPS-Zielserver.";
        if(input.Name is null||input.Name.Trim().Length is <2 or >120||
            input.Slug is null||!Regex.IsMatch(input.Slug,
                "^[a-z][a-z0-9-]{1,39}$",RegexOptions.CultureInvariant)||
            input.Slug.EndsWith('-')||
            input.Slug is "portal" or "tickets" or "admin" or "auth" or "api"||
            !Uri.TryCreate(input.UpstreamUrl,UriKind.Absolute,out var upstream)||
            upstream.Scheme!=Uri.UriSchemeHttps||
            upstream.UserInfo.Length>0||upstream.Query.Length>0||
            upstream.Fragment.Length>0||upstream.AbsolutePath!="/"||
            upstream.HostNameType!=UriHostNameType.Dns)return false;
        var domain=config["PLENARO_FAVORITES_BASE_DOMAIN"]?.Trim().TrimEnd('.');
        var template=config["PLENARO_FAVORITES_HOST_TEMPLATE"];
        var shared=FavoritePathRouting.SharedOrigin(
            config["PLENARO_FAVORITES_PATH_ORIGIN"],
            config["PLENARO_PUBLIC_ORIGIN"],config["PLENARO_ZNUNY_PROXY_ORIGIN"]);
        var origin=shared is not null
            ? FavoritePathRouting.AppOrigin(shared,input.Slug)
            : FavoriteHostNames.PreferredOrigin(input.Slug,domain,template);
        if(origin is null)return false;
        var main=OriginHost(config["PLENARO_PUBLIC_ORIGIN"]);
        var znuny=OriginHost(config["PLENARO_ZNUNY_PROXY_ORIGIN"]);
        // Reject collisions for BOTH new and old aliases during migration.
        if(origin.Host.Equals(main,StringComparison.OrdinalIgnoreCase)||
            origin.Host.Equals(znuny,StringComparison.OrdinalIgnoreCase)||
            (!string.IsNullOrEmpty(domain) &&
                (input.Slug+"."+domain).Equals(main,StringComparison.OrdinalIgnoreCase))||
            (!string.IsNullOrEmpty(domain) &&
                (input.Slug+"."+domain).Equals(znuny,StringComparison.OrdinalIgnoreCase)))
            return false;
        // Reject self-proxying through either alias family (and the main
        // app or Znuny) so administrators cannot create recursive proxies.
        if((!string.IsNullOrEmpty(domain) &&
                upstream.Host.Equals(domain,StringComparison.OrdinalIgnoreCase))||
            FavoriteHostNames.IsReservedHost(upstream.Host,domain,template)||
            (shared is not null && upstream.Host.Equals(shared.Host,StringComparison.OrdinalIgnoreCase))||
            upstream.Host.Equals(main,StringComparison.OrdinalIgnoreCase)||
            upstream.Host.Equals(znuny,StringComparison.OrdinalIgnoreCase))
            return false;
        if(input.AllowedUsers is { Length: >1000 })return false;
        return true;
    }
    private static string OriginHost(string? raw)=>
        Uri.TryCreate(raw,UriKind.Absolute,out var url)?url.Host:"";

    private static void BindFavorite(NpgsqlCommand cmd,Guid id,Guid user,
        FavoriteInput value){
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.AddWithValue("app",value.AppId);
        cmd.Parameters.AddWithValue("name",value.Name.Trim());
        cmd.Parameters.AddWithValue("path",value.Path);
        cmd.Parameters.AddWithValue("username",value.Username.Trim());
        cmd.Parameters.AddWithValue("auto",value.AutoLogin);
        cmd.Parameters.AddWithValue("enabled",value.Enabled);
        cmd.Parameters.AddWithValue("ord",value.SortOrder);
    }
    private static void BindApp(NpgsqlCommand cmd,Guid id,AppInput value){
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("name",value.Name.Trim());
        cmd.Parameters.AddWithValue("slug",value.Slug);
        cmd.Parameters.AddWithValue("url",new Uri(value.UpstreamUrl).AbsoluteUri);
        cmd.Parameters.AddWithValue("enabled",value.Enabled);
        cmd.Parameters.AddWithValue("all",value.AvailableToAll);
    }
    private static async Task SetAcl(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid appId,Guid[]? users,CancellationToken ct){
        await using(var clear=new NpgsqlCommand(
            "DELETE FROM favorite_app_users WHERE app_id=@app",db,tx)){
            clear.Parameters.AddWithValue("app",appId);
            await clear.ExecuteNonQueryAsync(ct);
        }
        foreach(var user in (users??Array.Empty<Guid>()).Distinct()){
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO favorite_app_users(app_id,user_id)
                VALUES(@app,@uid) ON CONFLICT DO NOTHING
                """,db,tx);
            cmd.Parameters.AddWithValue("app",appId);
            cmd.Parameters.AddWithValue("uid",user);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
    private static async Task<bool> ValidCsrf(HttpContext ctx,IAntiforgery anti){
        try{await anti.ValidateRequestAsync(ctx);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
    private static IResult CsrfError()=>Results.BadRequest(new{
        error="Sicherheitstoken fehlt oder ist ungültig."
    });
}
