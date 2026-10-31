using System.Globalization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using NpgsqlTypes;
using Plenaro.Api.Identity;

namespace Plenaro.Api.Znuny;

/// <summary>
/// Local planning ONLY. No changes are sent to Znuny. Every read/write
/// rechecks the user's admin-approved agent ID against the current ticket
/// owner/responsible, or the public unassigned/default-agent pool.
/// </summary>
public static class ZnunyTicketPlanningEndpoints
{
    public sealed record SegmentInput(DateTimeOffset StartsAt,DateTimeOffset EndsAt,
        string Note="");

    private const string Visibility = """
        FROM znuny_cached_tickets t
        INNER JOIN znuny_approved_agent_bindings b ON b.user_id=@user
        CROSS JOIN znuny_sync_control c
        WHERE (
          t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id OR
          ((t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,c.candidate_default_agent_id))
           AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,c.candidate_default_agent_id)))
        )
        """;

    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/tickets/agenda",async (string? date,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            if(!DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var day))
                return Results.BadRequest(new {error="Datum im Format JJJJ-MM-TT angeben."});
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var start=TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue),zone);
            var end=TimeZoneInfo.ConvertTimeToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue),zone);
            var user=await OwnUser(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT s.id,s.ticket_id,t.ticket_number,t.title,t.queue,t.state,
                    t.state_type,t.owner_agent_id,t.responsible_agent_id,
                    s.starts_at,s.ends_at,s.note
                FROM znuny_ticket_segments s
                JOIN znuny_cached_tickets t ON t.ticket_id=s.ticket_id
                JOIN znuny_approved_agent_bindings b ON b.user_id=s.user_id AND b.user_id=@user
                CROSS JOIN znuny_sync_control c
                WHERE s.user_id=@user AND s.starts_at<@end AND s.ends_at>@start
                AND lower(t.state_type)<>'closed'
                AND (
                  t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id OR
                  ((t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,c.candidate_default_agent_id))
                   AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,c.candidate_default_agent_id)))
                )
                ORDER BY s.starts_at,s.id LIMIT 500
                """,db);
            cmd.Parameters.AddWithValue("user",user.Value);
            cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=start;
            cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=end;
            var agenda=new List<object>();
            await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))
                agenda.Add(new {
                    id=r.GetGuid(0),ticketId=r.GetString(1),
                    ticketNumber=r.GetString(2),title=r.GetString(3),
                    queue=r.GetString(4),state=r.GetString(5),stateType=r.GetString(6),
                    ownerId=r.IsDBNull(7)?(int?)null:r.GetInt32(7),
                    responsibleId=r.IsDBNull(8)?(int?)null:r.GetInt32(8),
                    startsAt=new DateTimeOffset(r.GetDateTime(9),TimeSpan.Zero),
                    endsAt=new DateTimeOffset(r.GetDateTime(10),TimeSpan.Zero),
                    note=r.GetString(11)
                });
            return Results.Ok(agenda);
        });

        group.MapGet("/tickets/{ticketId}/segments",async(string ticketId,HttpContext http,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct) =>
        {
            var user=await OwnUser(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand("""
                SELECT s.id,s.starts_at,s.ends_at,s.note
                FROM znuny_ticket_segments s
                JOIN znuny_cached_tickets t ON t.ticket_id=s.ticket_id
                JOIN znuny_approved_agent_bindings b ON b.user_id=s.user_id AND b.user_id=@user
                CROSS JOIN znuny_sync_control c
                WHERE s.ticket_id=@ticket AND s.user_id=@user
                AND lower(t.state_type)<>'closed'
                AND (
                  t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id OR
                  ((t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,c.candidate_default_agent_id))
                   AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,c.candidate_default_agent_id)))
                )
                ORDER BY s.starts_at,s.id LIMIT 200
                """,db);
            cmd.Parameters.AddWithValue("ticket",ticketId);
            cmd.Parameters.AddWithValue("user",user.Value);
            var rows=new List<object>();
            await using var r=await cmd.ExecuteReaderAsync(ct);
            while(await r.ReadAsync(ct))
                rows.Add(new {id=r.GetGuid(0),
                    startsAt=new DateTimeOffset(r.GetDateTime(1),TimeSpan.Zero),
                    endsAt=new DateTimeOffset(r.GetDateTime(2),TimeSpan.Zero),
                    note=r.GetString(3)});
            return Results.Ok(rows);
        });

        group.MapPost("/tickets/{ticketId}/segments",async(string ticketId,SegmentInput input,
            HttpContext http,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct) =>
        {
            if(!await ValidCsrf(http,anti))return Results.BadRequest(new {error="CSRF-Token ungültig."});
            var user=await OwnUser(http,users);
            if(user is null)return Results.Unauthorized();
            if(input.StartsAt>=input.EndsAt ||
                input.EndsAt-input.StartsAt>TimeSpan.FromDays(7) ||
                input.Note is null || input.Note.Length>2000)
                return Results.BadRequest(new {error="Zeitraum oder Notiz ungültig."});
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand($"""
                INSERT INTO znuny_ticket_segments(id,user_id,ticket_id,starts_at,ends_at,note)
                SELECT @id,@user,t.ticket_id,@start,@end,@note
                {Visibility}
                AND t.ticket_id=@ticket AND lower(t.state_type)<>'closed'
                """,db);
            var id=Guid.NewGuid();
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("user",user.Value);
            cmd.Parameters.AddWithValue("ticket",ticketId);
            cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=input.StartsAt.UtcDateTime;
            cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=input.EndsAt.UtcDateTime;
            cmd.Parameters.AddWithValue("note",input.Note.Trim());
            return await cmd.ExecuteNonQueryAsync(ct)==1
                ? Results.Created($"/api/v1/znuny/tickets/{Uri.EscapeDataString(ticketId)}/segments/{id}",new {id})
                : Results.NotFound();
        });

        group.MapDelete("/tickets/{ticketId}/segments/{id:guid}",async(string ticketId,Guid id,
            HttpContext http,IAntiforgery anti,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct) =>
        {
            if(!await ValidCsrf(http,anti))return Results.BadRequest(new {error="CSRF-Token ungültig."});
            var user=await OwnUser(http,users);
            if(user is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var cmd=new NpgsqlCommand($"""
                DELETE FROM znuny_ticket_segments s
                WHERE s.id=@id AND s.ticket_id=@ticket AND s.user_id=@user
                AND EXISTS(
                    SELECT 1 {Visibility} AND t.ticket_id=s.ticket_id
                )
                """,db);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("ticket",ticketId);
            cmd.Parameters.AddWithValue("user",user.Value);
            return await cmd.ExecuteNonQueryAsync(ct)==1
                ? Results.NoContent():Results.NotFound();
        });
    }

    private static async Task<Guid?> OwnUser(HttpContext http,UserManager<ApplicationUser> users)
    {
        var user=await users.GetUserAsync(http.User);
        return user is null || user.IsDisabled ? null : user.Id;
    }

    private static async Task<bool> ValidCsrf(HttpContext http,IAntiforgery anti)
    {
        try{await anti.ValidateRequestAsync(http);return true;}
        catch(AntiforgeryValidationException){return false;}
    }

    private static async Task<NpgsqlConnection> Open(IConfiguration config,CancellationToken ct)
    {
        var db=new NpgsqlConnection(config.GetConnectionString("Plenaro"));
        await db.OpenAsync(ct);
        return db;
    }
}
