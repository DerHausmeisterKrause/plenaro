using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Plenaro.Api.Database;
using Plenaro.Api.Identity;
using Plenaro.Api.Settings;
using Plenaro.Api.Secrets;
using Plenaro.Api.Graph;
using Plenaro.Api.Tasks;
using Plenaro.Api.DayMarkers;
using Plenaro.Api.Reports;

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

var requireHttps = builder.Configuration.GetValue<bool>("Security:RequireHttps");
var entra = new EntraConfiguration(requireHttps);
builder.Services.AddSingleton(entra);
// Always trust forwarded headers from exactly our own NGINX container, never from
// arbitrary LAN clients. Correct client IPs are essential for login rate limits.
var trustedProxy = builder.Configuration["Security:TrustedProxyIp"];
if (!IPAddress.TryParse(trustedProxy, out var proxyIp))
    throw new InvalidOperationException("NGINX requires Security:TrustedProxyIp.");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownProxies.Add(proxyIp);
});

var connectionString = builder.Configuration.GetConnectionString("Plenaro");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Plenaro is required.");

builder.Services.AddSingleton<SchemaMigrator>();
builder.Services.AddSingleton<SecretVault>();
builder.Services.AddHttpClient("Plenaro.Graph", http =>
{
    http.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<GraphCalendarService>();
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

{
    builder.Services.AddAuthentication().AddOpenIdConnect(EntraConfiguration.Scheme, options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{(entra.Enabled ? entra.TenantId : "organizations")}/v2.0";
        options.ClientId = entra.Enabled ? entra.ClientId : "00000000-0000-0000-0000-000000000001";
        options.ClientSecret = entra.Enabled ? entra.ClientSecret : "not-enabled";
        options.CallbackPath = "/signin-entra";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = false; // No Graph/refresh tokens in the identity cookie.
        options.GetClaimsFromUserInfoEndpoint = false;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = true;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        // Optional delegated calendar read on first sign-in; no separate login required
        // for users whose local account is already explicitly linked.
        options.Scope.Add("User.Read");
        options.Scope.Add("Calendars.Read");
        options.Scope.Add("offline_access");
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidAudience = entra.Enabled ? entra.ClientId : "00000000-0000-0000-0000-000000000001";
        options.Events = new OpenIdConnectEvents
        {
            OnTokenResponseReceived = context =>
            {
                // This is request-scoped memory, NOT AuthenticationProperties/cookie.
                context.HttpContext.Items["plenaro.graph.refresh"] = context.TokenEndpointResponse?.RefreshToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                if (context.Principal is null || !entra.Enabled) return;
                // Linking is a distinct, explicitly approved operation. Only a
                // normal Entra login may refresh the linked user's Graph token.
                if (context.Properties?.Items.ContainsKey("linkUserId") == true) return;
                var tid = context.Principal.FindFirst("tid")?.Value;
                var oid = context.Principal.FindFirst("oid")?.Value;
                if (!Guid.TryParse(tid, out var tenantId) ||
                    !Guid.TryParse(oid, out var objectId) ||
                    !string.Equals(tenantId.ToString("D"), entra.TenantId, StringComparison.Ordinal))
                    return;

                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var existing = await users.FindByLoginAsync(EntraConfiguration.Provider,
                    $"{tenantId:D}:{objectId:D}");
                if (existing is null || existing.IsDisabled || await users.IsLockedOutAsync(existing))
                    return; // Brand-new link: no calendar access before explicit user approval.
                var token = context.HttpContext.Items["plenaro.graph.refresh"] as string;
                if (string.IsNullOrEmpty(token)) return;
                var vault = context.HttpContext.RequestServices.GetRequiredService<SecretVault>();
                if (vault.IsConfigured)
                    await vault.SetAsync("user", existing.Id, GraphCalendarService.RefreshSecretName,
                        token, context.HttpContext.RequestAborted);
            },
            OnRemoteFailure = context =>
            {
                // No exception strings, tokens or sensitive claim details in URLs/logs.
                context.HandleResponse();
                context.Response.Redirect("/login?entra=failed");
                return Task.CompletedTask;
            }
        };
    });
}

{
    builder.Services.AddAuthentication().AddOAuth(GraphCalendarService.Scheme, options =>
    {
        options.ClientId = entra.Enabled ? entra.ClientId : "00000000-0000-0000-0000-000000000001";
        options.ClientSecret = entra.Enabled ? entra.ClientSecret : "not-enabled";
        options.AuthorizationEndpoint =
            $"https://login.microsoftonline.com/{(entra.Enabled ? entra.TenantId : "organizations")}/oauth2/v2.0/authorize";
        options.TokenEndpoint =
            $"https://login.microsoftonline.com/{(entra.Enabled ? entra.TenantId : "organizations")}/oauth2/v2.0/token";
        options.CallbackPath = "/signin-graph";
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.UsePkce = true;
        options.SaveTokens = false; // Access/refresh tokens NEVER go in auth cookies.
        options.Scope.Clear();
        options.Scope.Add("User.Read");
        options.Scope.Add("Calendars.Read");
        options.Scope.Add("offline_access");
        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                if (context.Properties.Items.TryGetValue("graphLocalUser", out var localId) != true ||
                    !Guid.TryParse(localId, out var userId) ||
                    string.IsNullOrWhiteSpace(context.AccessToken) ||
                    string.IsNullOrWhiteSpace(context.RefreshToken))
                {
                    context.Fail("Graph consent state or tokens missing");
                    return;
                }
                var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(userId.ToString("D"));
                if (user is null || user.IsDisabled)
                {
                    context.Fail("No active linked local user");
                    return;
                }

                // Ask Microsoft Graph for the authenticated object ID. Do not use
                // email/UPN or decode unvalidated access-token JWTs for account binding.
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://graph.microsoft.com/v1.0/me?$select=id");
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(request,
                    context.HttpContext.RequestAborted);
                if (!response.IsSuccessStatusCode)
                {
                    context.Fail("Cannot validate Graph account");
                    return;
                }
                using var body = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(context.HttpContext.RequestAborted),
                    cancellationToken: context.HttpContext.RequestAborted);
                var graphId = body.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() : null;
                if (!Guid.TryParse(graphId, out var objectId))
                {
                    context.Fail("Invalid Graph account");
                    return;
                }
                var expectedKey = $"{entra.TenantId}:{objectId:D}";
                var linked = (await users.GetLoginsAsync(user)).Any(login =>
                    login.LoginProvider == EntraConfiguration.Provider &&
                    string.Equals(login.ProviderKey, expectedKey, StringComparison.Ordinal));
                if (!linked)
                {
                    context.Fail("Graph account differs from the linked Entra identity");
                    return;
                }

                var vault = context.HttpContext.RequestServices.GetRequiredService<SecretVault>();
                if (!vault.IsConfigured)
                {
                    context.Fail("Encrypted token store unavailable");
                    return;
                }
                await vault.SetAsync("user", user.Id, GraphCalendarService.RefreshSecretName,
                    context.RefreshToken, context.HttpContext.RequestAborted);
            },
            // OAuth handler must NOT write a temporary external login cookie: the
            // refresh token is already encrypted at rest and no token is browser-visible.
            OnTicketReceived = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/week?graph=connected");
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/week?graph=failed");
                return Task.CompletedTask;
            }
        };
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Plenaro.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // HTTPS sets Secure. For the existing HTTP-only development LAN the cookie works
    // without Secure; do not use that deployment mode outside a trusted lab network.
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
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
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireAuthenticatedUser().RequireRole("Admin"));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "Plenaro.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = requireHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
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
await entra.InitializeAsync(app.Services.GetRequiredService<SecretVault>(), app.Lifetime.ApplicationStopping);

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

app.UseForwardedHeaders();

if (requireHttps)
{
    // The Docker healthcheck connects directly over loopback; no other HTTP entry
    // point is permitted in secure mode (including API requests and static pages).
    app.Use(async (http, next) =>
    {
        var internalHealthcheck = http.Request.Path.StartsWithSegments("/health") &&
            http.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote);
        if (!http.Request.IsHttps && !internalHealthcheck)
        {
            http.Response.StatusCode = StatusCodes.Status426UpgradeRequired;
            return;
        }
        await next();
    });
    app.UseHsts();
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
    phase = 4
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
app.MapPlenaroEntra();
app.MapPlenaroEntraAdmin();
app.MapAdminUsers();
app.MapPlenaroSettings();
app.MapPlenaroSecrets();
app.MapPlenaroGraph();
app.MapPlenaroTasks();
app.MapPlenaroTaskWork();
app.MapPlenaroDayMarkers();
app.MapPlenaroReports();
app.MapFallbackToFile("index.html");
app.Run();
