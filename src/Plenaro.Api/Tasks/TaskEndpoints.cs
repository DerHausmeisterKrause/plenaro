using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Tasks;

/// <summary>Local tasks are always scoped to the authenticated Identity user.</summary>
public static class TaskEndpoints
{
    private static readonly HashSet<string> ValidStatuses =
        ["planned", "active", "completed", "cancelled"];

    public sealed record TaskChange(
        string Title, string? Description, string Status, int? Priority,
        string[]? Tags, DateOnly? PlannedFor);
    public sealed record TaskStateChange(string Status);
    public sealed record TaskItem(
        Guid Id, string Title, string Description, string Status, int? Priority,
        string[] Tags, DateOnly? PlannedFor, DateTime CreatedAt, DateTime UpdatedAt,
        DateTime? CompletedAt);

    public static void MapPlenaroTasks(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/tasks").RequireAuthorization();

        group.MapGet("/", async (HttpContext http, string? q, string? status,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (status is not null && !ValidStatuses.Contains(status))
                return Results.BadRequest(new { error = "Ungültiger Statusfilter." });
            if (q?.Length > 120) return Results.BadRequest(new { error = "Suchbegriff ist zu lang." });

            await using var db = await OpenAsync(config, ct);
            await using var command = new NpgsqlCommand("""
                SELECT id,title,description,status,priority,tags,planned_for,
                       created_at,updated_at,completed_at
                FROM tasks
                WHERE owner_user_id=@owner
                  AND (@status IS NULL OR status=@status)
                  AND (@query IS NULL OR title ILIKE '%' || @query || '%'
                       OR description ILIKE '%' || @query || '%')
                ORDER BY CASE WHEN status='completed' THEN 1 ELSE 0 END,
                         planned_for ASC NULLS LAST, updated_at DESC
                LIMIT 200
                """, db);
            command.Parameters.AddWithValue("owner", owner.Value);
            command.Parameters.Add("status", NpgsqlDbType.Varchar).Value = (object?)status ?? DBNull.Value;
            command.Parameters.Add("query", NpgsqlDbType.Text).Value =
                string.IsNullOrWhiteSpace(q) ? DBNull.Value : q.Trim();
            return Results.Ok(await ReadTasksAsync(command, ct));
        });

        group.MapGet("/today", async (HttpContext http, DateOnly? date,
            UserManager<ApplicationUser> users, IConfiguration config, CancellationToken ct) =>
        {
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (date is null) return Results.BadRequest(new { error = "Lokales Datum erforderlich." });

            // Today: outstanding tasks scheduled on/before this date, in-progress
            // tasks, and tasks completed on this exact planning date. Completed
            // unscheduled tasks are shown in the full task list.
            await using var db = await OpenAsync(config, ct);
            await using var command = new NpgsqlCommand("""
                SELECT id,title,description,status,priority,tags,planned_for,
                       created_at,updated_at,completed_at
                FROM tasks WHERE owner_user_id=@owner
                  AND (
                    (status IN ('planned','active') AND
                       (planned_for <= @date OR status='active'))
                    OR (status='completed' AND planned_for=@date)
                  )
                ORDER BY CASE status WHEN 'active' THEN 0
                     WHEN 'planned' THEN 1 ELSE 2 END,
                     priority ASC NULLS LAST, updated_at DESC
                LIMIT 200
                """, db);
            command.Parameters.AddWithValue("owner", owner.Value);
            command.Parameters.Add("date", NpgsqlDbType.Date).Value = date.Value;
            return Results.Ok(await ReadTasksAsync(command, ct));
        });

        group.MapPost("/", async (HttpContext http, TaskChange request,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (!TryValidate(request, out var message, out var tags))
                return Results.BadRequest(new { error = message });
            var id = Guid.NewGuid();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                INSERT INTO tasks(id,owner_user_id,title,description,status,priority,
                                  tags,planned_for,completed_at)
                VALUES (@id,@owner,@title,@description,@status,@priority,@tags,@date,
                        CASE WHEN @status='completed' THEN now() ELSE NULL END)
                """, db);
            FillFields(cmd, request, tags);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Created($"/api/v1/tasks/{id}", new { id });
        });

        group.MapPut("/{id:guid}", async (Guid id, HttpContext http, TaskChange request,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (!TryValidate(request, out var message, out var tags))
                return Results.BadRequest(new { error = message });

            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                UPDATE tasks SET title=@title, description=@description, status=@status,
                    priority=@priority, tags=@tags, planned_for=@date,
                    completed_at=CASE WHEN @status='completed' AND status <> 'completed' THEN now()
                        WHEN @status='completed' THEN completed_at ELSE NULL END,
                    updated_at=now()
                WHERE id=@id AND owner_user_id=@owner
                """, db);
            FillFields(cmd, request, tags);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            return await cmd.ExecuteNonQueryAsync(ct) == 1
                ? Results.NoContent() : Results.NotFound();
        });

        group.MapPost("/{id:guid}/status", async (Guid id, HttpContext http, TaskStateChange request,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            if (request.Status is null || !ValidStatuses.Contains(request.Status))
                return Results.BadRequest(new { error = "Ungültiger Aufgabenstatus." });
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand("""
                UPDATE tasks SET
                    completed_at=CASE WHEN @status='completed' AND status <> 'completed' THEN now()
                        WHEN @status='completed' THEN completed_at ELSE NULL END,
                    status=@status,updated_at=now()
                WHERE id=@id AND owner_user_id=@owner
                """, db);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            cmd.Parameters.AddWithValue("status", request.Status);
            return await cmd.ExecuteNonQueryAsync(ct) == 1
                ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http,
            IAntiforgery antiforgery, UserManager<ApplicationUser> users,
            IConfiguration config, CancellationToken ct) =>
        {
            if (!await ValidCsrfAsync(http, antiforgery)) return CsrfError();
            var owner = await OwnerAsync(http, users);
            if (owner is null) return Results.Unauthorized();
            await using var db = await OpenAsync(config, ct);
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM tasks WHERE id=@id AND owner_user_id=@owner", db);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("owner", owner.Value);
            return await cmd.ExecuteNonQueryAsync(ct) == 1
                ? Results.NoContent() : Results.NotFound();
        });
    }

    private static async Task<Guid?> OwnerAsync(HttpContext context,
        UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(context.User);
        return user is null || user.IsDisabled ? null : user.Id;
    }

    private static bool TryValidate(TaskChange task, out string message, out string[] tags)
    {
        tags = [];
        message = "";
        if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Trim().Length > 255)
            message = "Titel muss zwischen 1 und 255 Zeichen lang sein.";
        else if ((task.Description?.Length ?? 0) > 10000)
            message = "Beschreibung darf höchstens 10.000 Zeichen enthalten.";
        else if (task.Status is null || !ValidStatuses.Contains(task.Status))
            message = "Ungültiger Aufgabenstatus.";
        else if (task.Priority is not null && (task.Priority < 1 || task.Priority > 5))
            message = "Priorität muss zwischen 1 und 5 liegen.";
        else if ((task.Tags?.Length ?? 0) > 10)
            message = "Maximal 10 Tags erlaubt.";
        if (message.Length > 0) return false;
        tags = (task.Tags ?? []).Select(t => t?.Trim() ?? "")
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (tags.Any(t => t.Length > 32) || tags.Length > 10)
        {
            message = "Jeder Tag darf maximal 32 Zeichen enthalten.";
            return false;
        }
        return true;
    }

    private static void FillFields(NpgsqlCommand cmd, TaskChange task, string[] tags)
    {
        cmd.Parameters.AddWithValue("title", task.Title.Trim());
        cmd.Parameters.AddWithValue("description", task.Description ?? "");
        cmd.Parameters.AddWithValue("status", task.Status);
        cmd.Parameters.Add("priority", NpgsqlDbType.Smallint).Value =
            task.Priority.HasValue ? checked((short)task.Priority.Value) : DBNull.Value;
        cmd.Parameters.Add("tags", NpgsqlDbType.Array | NpgsqlDbType.Text).Value = tags;
        cmd.Parameters.Add("date", NpgsqlDbType.Date).Value =
            task.PlannedFor.HasValue ? task.PlannedFor.Value : DBNull.Value;
    }

    private static async Task<NpgsqlConnection> OpenAsync(IConfiguration config, CancellationToken ct)
    {
        var connection = new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await connection.OpenAsync(ct);
        return connection;
    }

    private static async Task<List<TaskItem>> ReadTasksAsync(NpgsqlCommand command, CancellationToken ct)
    {
        var items = new List<TaskItem>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            items.Add(new TaskItem(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetInt16(4),
                reader.GetFieldValue<string[]>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                reader.GetDateTime(7), reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9)));
        return items;
    }

    private static async Task<bool> ValidCsrfAsync(HttpContext context, IAntiforgery tokens)
    {
        try { await tokens.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult CsrfError() => Results.BadRequest(
        new { error = "Sicherheitstoken fehlt oder ist ungültig." });
}
