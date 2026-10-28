using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Secrets;

public static class SecretEndpoints
{
    public sealed record SecretValueRequest(string Value);

    public static void MapPlenaroSecrets(this WebApplication app)
    {
        var userGroup = app.MapGroup("/api/v1/secrets/me").RequireAuthorization();
        var adminGroup = app.MapGroup("/api/v1/admin/secrets").RequireAuthorization("AdminOnly");

        userGroup.MapGet("/", async (HttpContext http, UserManager<ApplicationUser> users,
            SecretVault vault, CancellationToken ct) =>
        {
            if (!vault.IsConfigured) return NotConfigured();
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            return Results.Ok(new { names = await vault.ListNamesAsync("user", user.Id, ct) });
        });

        userGroup.MapPut("/{name}", async (string name, SecretValueRequest request, HttpContext http,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users, SecretVault vault, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!vault.IsConfigured) return NotConfigured();
            if (!SecretVault.ValidName(name) || request.Value is not { Length: >= 1 and <= 4096 })
                return Results.BadRequest(new { error = "Ungültiger Name oder Secret-Wert." });
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            await vault.SetAsync("user", user.Id, name, request.Value, ct);
            return Results.NoContent();
        });

        userGroup.MapDelete("/{name}", async (string name, HttpContext http, IAntiforgery antiforgery,
            UserManager<ApplicationUser> users, SecretVault vault, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!vault.IsConfigured) return NotConfigured();
            if (!SecretVault.ValidName(name)) return Results.BadRequest();
            var user = await users.GetUserAsync(http.User);
            if (user is null || user.IsDisabled) return Results.Unauthorized();
            await vault.DeleteAsync("user", user.Id, name, ct);
            return Results.NoContent();
        });

        adminGroup.MapGet("/", async (SecretVault vault, CancellationToken ct) =>
            !vault.IsConfigured ? NotConfigured() :
            Results.Ok(new { names = await vault.ListNamesAsync("global", null, ct) }));

        adminGroup.MapPut("/{name}", async (string name, SecretValueRequest request, HttpContext http,
            IAntiforgery antiforgery, SecretVault vault, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!vault.IsConfigured) return NotConfigured();
            if (!SecretVault.ValidName(name) || request.Value is not { Length: >= 1 and <= 4096 })
                return Results.BadRequest(new { error = "Ungültiger Name oder Secret-Wert." });
            await vault.SetAsync("global", null, name, request.Value, ct);
            return Results.NoContent();
        });

        adminGroup.MapDelete("/{name}", async (string name, HttpContext http,
            IAntiforgery antiforgery, SecretVault vault, CancellationToken ct) =>
        {
            if (!await ValidCsrf(http, antiforgery)) return CsrfError();
            if (!vault.IsConfigured) return NotConfigured();
            if (!SecretVault.ValidName(name)) return Results.BadRequest();
            await vault.DeleteAsync("global", null, name, ct);
            return Results.NoContent();
        });
    }

    private static IResult NotConfigured() => Results.Problem(
        "Verschlüsselter Secret-Speicher ist nicht eingerichtet.", statusCode: 503);

    private static async Task<bool> ValidCsrf(HttpContext context, IAntiforgery tokens)
    {
        try { await tokens.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() => Results.BadRequest(
        new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
