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
        string Note="",string[]? Participants=null);

    private const string Visibility = """
        FROM znuny_cached_tickets t
        INNER JOIN znuny_approved_agent_bindings b ON b.user_id=@user
        LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
        WHERE (
          t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id OR
          (p.candidate_user_id IS NOT NULL AND (t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,p.candidate_user_id))
           AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,p.candidate_user_id)))
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
                JOIN znuny_approved_agent_bindings b ON b.user_id=@user
                JOIN "AspNetUsers" account ON account."Id"=b.user_id
                WHERE s.starts_at<@end AND s.ends_at>@start
                AND lower(t.state_type)<>'closed'
                AND (
                  s.user_id=@user OR
                  (account."NormalizedEmail" IS NOT NULL AND EXISTS (
                    SELECT 1 FROM unnest(s.participants) AS participant(email)
                    WHERE upper(trim(participant.email))=account."NormalizedEmail"
                  ))
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
                SELECT s.id,s.starts_at,s.ends_at,s.note,s.participants
                FROM znuny_ticket_segments s
                JOIN znuny_cached_tickets t ON t.ticket_id=s.ticket_id
                JOIN znuny_approved_agent_bindings b ON b.user_id=s.user_id AND b.user_id=@user
                LEFT JOIN znuny_user_settings p ON p.user_id=b.user_id
                WHERE s.ticket_id=@ticket AND s.user_id=@user
                AND lower(t.state_type)<>'closed'
                AND (
                  t.owner_agent_id=b.agent_id OR t.responsible_agent_id=b.agent_id OR
                  (p.candidate_user_id IS NOT NULL AND (t.owner_agent_id IS NULL OR t.owner_agent_id IN (0,p.candidate_user_id))
                   AND (t.responsible_agent_id IS NULL OR t.responsible_agent_id IN (0,p.candidate_user_id)))
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
                    note=r.GetString(3),participants=r.GetFieldValue<string[]>(4)});
            return Results.Ok(rows);
        });

        // A 06:00-18:00 calendar strip of the signed-in user's own bookings.
        // The ticket authorization follows the same approved agent/candidate
        // rules as ticket segments. Never disclose other users' meetings.
        group.MapGet("/tickets/{ticketId}/availability", async(string ticketId,
            string? date,HttpContext http,UserManager<ApplicationUser> users,
            IConfiguration config,CancellationToken ct) =>
        {
            if(!DateOnly.TryParseExact(date,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var day) || day.Year is <2000 or >2099)
                return Results.BadRequest(new{error="Datum im Format JJJJ-MM-TT erforderlich."});
            var owner=await OwnUser(http,users);
            if(owner is null)return Results.Unauthorized();
            await using var db=await Open(config,ct);
            await using var authorized=new NpgsqlCommand($"""
                SELECT EXISTS(SELECT 1 {Visibility} AND t.ticket_id=@ticket)
                """,db);
            authorized.Parameters.AddWithValue("user",owner.Value);
            authorized.Parameters.AddWithValue("ticket",ticketId);
            if(await authorized.ExecuteScalarAsync(ct) is not true)
                return Results.NotFound();
            var zone=TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
            var from=TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(6,0)),zone);
            var to=TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(new TimeOnly(18,0)),zone);
            await using var cmd=new NpgsqlCommand("""
                SELECT id,starts_at,ends_at FROM task_segments
                WHERE owner_user_id=@user AND starts_at<@end AND ends_at>@start
                UNION ALL
                SELECT id,starts_at,ends_at FROM znuny_ticket_segments
                WHERE user_id=@user AND starts_at<@end AND ends_at>@start
                ORDER BY starts_at LIMIT 500
                """,db);
            cmd.Parameters.AddWithValue("user",owner.Value);
            cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=from;
            cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=to;
            var intervals=new List<object>();
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct))
                intervals.Add(new {
                    id=reader.GetGuid(0),
                    startsAt=new DateTimeOffset(reader.GetDateTime(1),TimeSpan.Zero),
                    endsAt=new DateTimeOffset(reader.GetDateTime(2),TimeSpan.Zero)
                });
            return Results.Ok(new{intervals,hasMore=intervals.Count==500});
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
                input.Note is null || input.Note.Length>2000 ||
                InvalidParticipants(input.Participants))
                return Results.BadRequest(new {error="Zeitraum oder Notiz ungültig."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await LockOwnerAsync(db,tx,user.Value,ct);
            if(!await CanWriteAsync(db,tx,user.Value,ticketId,null,ct))
                return Results.NotFound();
            if(await OverlapsAsync(db,tx,user.Value,input.StartsAt,input.EndsAt,null,ct))
                return Results.Conflict(new{error="Du hast bereits ein anderes Plenaro-Zeitsegment in diesem Zeitraum."});
            await using var cmd=new NpgsqlCommand($"""
                INSERT INTO znuny_ticket_segments(id,user_id,ticket_id,starts_at,ends_at,note,participants)
                SELECT @id,@user,t.ticket_id,@start,@end,@note,@participants
                {Visibility}
                AND t.ticket_id=@ticket AND lower(t.state_type)<>'closed'
                """,db,tx);
            var id=Guid.NewGuid();
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("user",user.Value);
            cmd.Parameters.AddWithValue("ticket",ticketId);
            cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=input.StartsAt.UtcDateTime;
            cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=input.EndsAt.UtcDateTime;
            cmd.Parameters.AddWithValue("note",input.Note.Trim());
            cmd.Parameters.Add("participants",NpgsqlDbType.Array|NpgsqlDbType.Text).Value=
                input.Participants??Array.Empty<string>();
            if(await cmd.ExecuteNonQueryAsync(ct)!=1)return Results.NotFound();
            await tx.CommitAsync(ct);
            return Results.Created($"/api/v1/znuny/tickets/{Uri.EscapeDataString(ticketId)}/segments/{id}",new{id});
        });

        // Exact authorization matches create/delete; editing is also scoped
        // by the segment's persisted owner and immutable ticket ID.
        group.MapPut("/tickets/{ticketId}/segments/{id:guid}",async(string ticketId,Guid id,
            SegmentInput input,HttpContext http,IAntiforgery anti,
            UserManager<ApplicationUser> users,IConfiguration config,CancellationToken ct)=>
        {
            if(!await ValidCsrf(http,anti))
                return Results.BadRequest(new {error="CSRF-Token ungültig."});
            var user=await OwnUser(http,users);
            if(user is null)return Results.Unauthorized();
            if(input.StartsAt>=input.EndsAt || input.EndsAt-input.StartsAt>TimeSpan.FromDays(7) ||
                input.Note is null || input.Note.Length>2000 ||
                InvalidParticipants(input.Participants))
                return Results.BadRequest(new {error="Zeitraum, Notiz oder Teilnehmer ungültig."});
            await using var db=await Open(config,ct);
            await using var tx=await db.BeginTransactionAsync(ct);
            await LockOwnerAsync(db,tx,user.Value,ct);
            if(!await CanWriteAsync(db,tx,user.Value,ticketId,id,ct))
                return Results.NotFound();
            if(await OverlapsAsync(db,tx,user.Value,input.StartsAt,input.EndsAt,id,ct))
                return Results.Conflict(new{error="Du hast bereits ein anderes Plenaro-Zeitsegment in diesem Zeitraum."});
            await using var cmd=new NpgsqlCommand($"""
                UPDATE znuny_ticket_segments s SET starts_at=@start,ends_at=@end,
                    note=@note,participants=@participants
                WHERE s.id=@id AND s.ticket_id=@ticket AND s.user_id=@user
                    AND EXISTS(SELECT 1 {Visibility} AND t.ticket_id=s.ticket_id
                    AND lower(t.state_type)<>'closed')
                RETURNING s.id
                """,db,tx);
            cmd.Parameters.AddWithValue("id",id);
            cmd.Parameters.AddWithValue("user",user.Value);
            cmd.Parameters.AddWithValue("ticket",ticketId);
            cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=input.StartsAt.UtcDateTime;
            cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=input.EndsAt.UtcDateTime;
            cmd.Parameters.AddWithValue("note",input.Note.Trim());
            cmd.Parameters.Add("participants",NpgsqlDbType.Array|NpgsqlDbType.Text).Value=
                input.Participants??Array.Empty<string>();
            if(await cmd.ExecuteScalarAsync(ct) is null)return Results.NotFound();
            await tx.CommitAsync(ct);
            return Results.NoContent();
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

    // Same per-user lock as TaskWorkEndpoints, so simultaneous writes in both
    // modules cannot silently double-book the user's calendar.
    private static async Task LockOwnerAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid user,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@owner::text, 0))",db,tx);
        cmd.Parameters.AddWithValue("owner",user);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // Check the private ticket/segment scope before querying any conflicts;
    // foreign ticket IDs must always return 404, never a leaked 409.
    private static async Task<bool> CanWriteAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid user,string ticketId,Guid? segmentId,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand($"""
            SELECT EXISTS(SELECT 1 {Visibility}
                AND t.ticket_id=@ticket AND lower(t.state_type)<>'closed'
                AND (@segment IS NULL OR EXISTS (
                    SELECT 1 FROM znuny_ticket_segments s
                    WHERE s.id=@segment AND s.ticket_id=t.ticket_id AND s.user_id=@user)))
            """,db,tx);
        cmd.Parameters.AddWithValue("user",user);
        cmd.Parameters.AddWithValue("ticket",ticketId);
        cmd.Parameters.Add("segment",NpgsqlDbType.Uuid).Value=segmentId.HasValue?segmentId.Value:DBNull.Value;
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<bool> OverlapsAsync(NpgsqlConnection db,NpgsqlTransaction tx,
        Guid user,DateTimeOffset start,DateTimeOffset end,Guid? exclude,CancellationToken ct)
    {
        await using var cmd=new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1 FROM task_segments
                WHERE owner_user_id=@owner AND starts_at<@end AND ends_at>@start
                UNION ALL
                SELECT 1 FROM znuny_ticket_segments
                WHERE user_id=@owner AND starts_at<@end AND ends_at>@start
                  AND (@exclude IS NULL OR id<>@exclude)
            )
            """,db,tx);
        cmd.Parameters.AddWithValue("owner",user);
        cmd.Parameters.Add("start",NpgsqlDbType.TimestampTz).Value=start.UtcDateTime;
        cmd.Parameters.Add("end",NpgsqlDbType.TimestampTz).Value=end.UtcDateTime;
        cmd.Parameters.Add("exclude",NpgsqlDbType.Uuid).Value=exclude.HasValue?exclude.Value:DBNull.Value;
        return await cmd.ExecuteScalarAsync(ct) is true;
    }

    private static bool InvalidParticipants(string[]? participants) =>
        participants is {Length:>20} || participants is not null &&
        participants.Any(p=>p is null || p.Length>254 || p.Any(char.IsControl) ||
            !p.Contains('@') || p.Contains(' '));

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
