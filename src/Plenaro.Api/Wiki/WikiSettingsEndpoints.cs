using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Wiki;

/// <summary>
/// The Classic WikiBrowser selects a configured source and opens its home URL.
/// In the web version the source is an administrator-approved FavoriteApp and
/// a per-user Favorite session. Only that existing authenticated proxy may
/// render the remote site; no arbitrary URL/iframe or new credential store.
/// </summary>
public static class WikiSettingsEndpoints
{
    public sealed record WikiPreference(Guid? FavoriteId);

    public static void MapPlenaroWiki(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/wiki").RequireAuthorization();

        group.MapGet("/settings",async(ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,IConfiguration config,
            CancellationToken ct)=>{
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT p.preferences->>'wikiFavoriteId'
                FROM user_preferences p WHERE p.user_id=@user
                """,db);
            cmd.Parameters.AddWithValue("user",user.Id);
            var value=await cmd.ExecuteScalarAsync(ct) as string;
            Guid? favorite=Guid.TryParse(value,out var id)?id:null;
            if(favorite is not null && !await IsAllowedFavorite(db,user.Id,id,ct))
                favorite=null;
            return Results.Ok(new WikiPreference(favorite));
        });

        group.MapPut("/settings",async(WikiPreference value,HttpContext http,
            IAntiforgery antiforgery,ClaimsPrincipal principal,
            UserManager<ApplicationUser> users,IConfiguration config,
            CancellationToken ct)=>{
            if(!await ValidCsrf(http,antiforgery))
                return Results.BadRequest(new{error="Sicherheitstoken fehlt oder ist ungültig."});
            var user=await users.GetUserAsync(principal);
            if(user is null||user.IsDisabled)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            if(value.FavoriteId is { } id &&
               !await IsAllowedFavorite(db,user.Id,id,ct))
                return Results.BadRequest(new{
                    error="Die Wiki-Quelle ist nicht freigegeben oder gehört nicht zu deinem Konto."});
            // Merge only this field: existing theme/weekStart and future
            // personal preferences are never overwritten by Wiki settings.
            await using var cmd=new NpgsqlCommand("""
                INSERT INTO user_preferences(user_id,preferences,updated_at)
                VALUES(@user,jsonb_build_object('wikiFavoriteId',@favorite),now())
                ON CONFLICT(user_id) DO UPDATE
                SET preferences=jsonb_set(
                    COALESCE(user_preferences.preferences,'{}'::jsonb),
                    '{wikiFavoriteId}',to_jsonb(@favorite::text),true),
                    updated_at=now()
                """,db);
            cmd.Parameters.AddWithValue("user",user.Id);
            cmd.Parameters.AddWithValue("favorite",value.FavoriteId?.ToString()??"");
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.NoContent();
        });
    }

    private static async Task<bool> IsAllowedFavorite(NpgsqlConnection db,
        Guid user,Guid favorite,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1 FROM user_favorites f
                JOIN favorite_apps a ON a.id=f.app_id
                WHERE f.id=@favorite AND f.user_id=@user
                  AND f.enabled AND a.enabled AND
                  (a.available_to_all OR EXISTS(
                      SELECT 1 FROM favorite_app_users acl
                      WHERE acl.app_id=a.id AND acl.user_id=@user)))
            """,db);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.AddWithValue("favorite",favorite);
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration config,
        CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }

    private static async Task<bool> ValidCsrf(HttpContext context,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(context);return true;}
        catch(AntiforgeryValidationException){return false;}
    }
}
