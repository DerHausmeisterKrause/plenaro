using Npgsql;
using Plenaro.Api.Database;

if (args.Contains("--healthcheck", StringComparer.Ordinal))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SchemaMigrator>();
var app = builder.Build();

var connectionString = app.Configuration.GetConnectionString("Plenaro");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings:Plenaro is required for database migrations.");

var migrator = app.Services.GetRequiredService<SchemaMigrator>();
// Fail closed: the app will not start if the schema is missing, corrupt or newer than the code.
await migrator.ApplyPendingAsync(connectionString, app.Logger, app.Lifetime.ApplicationStopping);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "plenaro-web",
    phase = 2
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
        logger.LogWarning("PostgreSQL readiness check failed: {SqlState}", ex.SqlState);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException)
    {
        logger.LogWarning("PostgreSQL readiness check timed out");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapFallbackToFile("index.html");
app.Run();
