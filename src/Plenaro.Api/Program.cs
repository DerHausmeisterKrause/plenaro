using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Plenaro.Api.Database;
using Plenaro.Api.Identity;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { Environment.ExitCode = 1; }
    catch (TaskCanceledException) { Environment.ExitCode = 1; }
    return;
}

var bootstrapAdmin = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
var setupToken = args.Contains("--setup-token", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(
    args.Where(a => a != "--bootstrap-admin" && a != "--setup-token").ToArray());

var connectionString = builder.Configuration.GetConnectionString("Plenaro");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Plenaro is required.");

builder.Services.AddSingleton<SchemaMigrator>();
builder.Services.AddDbContext<PlenaroIdentityDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddDataProtection()
    .SetApplicationName("Plenaro.Web")
    .PersistKeysToDbContext<PlenaroIdentityDbContext>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<PlenaroIdentityDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Plenaro.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // HTTPS sets Secure. For the existing HTTP-only development LAN the cookie works
    // without Secure; do not use that deployment mode outside a trusted lab network.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(2));

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Plenaro.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

var app = builder.Build();
var migrator = app.Services.GetRequiredService<SchemaMigrator>();
await migrator.ApplyPendingAsync(connectionString, app.Logger, app.Lifetime.ApplicationStopping);

if (bootstrapAdmin)
{
    Environment.ExitCode = await AdminBootstrap.RunAsync(
        app.Services, connectionString, Console.In, Console.Out);
    return;
}
if (setupToken)
{
    Environment.ExitCode = await InitialSetup.IssueAsync(connectionString);
    return;
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "plenaro-web",
    phase = 3
}));

app.MapGet("/health/ready", async (
    IConfiguration config, SchemaMigrator schema, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var cs = config.GetConnectionString("Plenaro");
    if (string.IsNullOrWhiteSpace(cs))
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    try
    {
        return await schema.IsReadyAsync(cs, cancellationToken)
            ? Results.Ok(new { status = "ready", database = "connected", schema = "current" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (NpgsqlException ex)
    {
        logger.LogWarning("PostgreSQL readiness failed: {SqlState}", ex.SqlState);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException)
    {
        logger.LogWarning("PostgreSQL readiness timed out");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPlenaroAuth();
app.MapFallbackToFile("index.html");
app.Run();
