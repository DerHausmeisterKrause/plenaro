using Npgsql;

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
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "ok",
    service = "plenaro-web",
    phase = 2
}));

app.MapGet("/health/ready", async (IConfiguration config, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    var connectionString = config.GetConnectionString("Plenaro");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value) == 1
            ? Results.Ok(new { status = "ready", database = "connected" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (NpgsqlException ex)
    {
        logger.LogWarning(ex, "PostgreSQL readiness check failed");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (TimeoutException ex)
    {
        logger.LogWarning(ex, "PostgreSQL readiness check timed out");
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});

// Browser-Client-Routing: z. B. /today direkt oder nach Browser-Refresh.
// Hier werden ausschließlich die öffentlichen Platzhalteransichten ausgeliefert.
// Personenbezogene /api-Routen werden erst nach Authentifizierung ergänzt.
app.MapFallbackToFile("index.html");

app.Run();
