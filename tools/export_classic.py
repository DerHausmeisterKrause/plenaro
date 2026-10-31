#!/usr/bin/env python3
"""Read-only Plenaro Classic SQLite v1..v32 to sanitized web import JSON.

This tool never writes to the old database and never exports credentials.
Run locally on the machine holding TaskTool.db (Python 3.10+).
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import re
import sqlite3
from zoneinfo import ZoneInfo
from urllib.parse import urlsplit, urlunsplit, parse_qsl, urlencode

TZ = ZoneInfo("Europe/Berlin")
SAFE_LIMIT = 20000


def parse_local(value):
    if value is None or not str(value).strip():
        raise ValueError("missing local date")
    d = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    if d.tzinfo is not None:
        return d.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    # Local desktop clock times must not silently change in DST gaps/overlaps.
    d0, d1 = d.replace(tzinfo=TZ, fold=0), d.replace(tzinfo=TZ, fold=1)
    if d0.utcoffset() != d1.utcoffset():
        raise ValueError("ambiguous or nonexistent local DST time")
    return d0.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def parse_utc(value):
    if not value or not str(value).strip():
        raise ValueError("missing UTC timestamp")
    d = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    # Classic persisted UTC fields are UTC even when SQLite lacks offset.
    return d.replace(tzinfo=timezone.utc).isoformat().replace("+00:00", "Z") if d.tzinfo is None else d.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def safe_ticket_url(raw):
    """Preserve non-secret ticket links; drop URL userinfo, fragments and unknown query parameters."""
    if not raw:
        return ""
    try:
        parsed = urlsplit(str(raw).strip())
        if parsed.scheme not in ("http", "https") or not parsed.hostname:
            return ""
        host = parsed.hostname
        if ":" in host:
            host = "[" + host + "]"
        netloc = host + (f":{parsed.port}" if parsed.port else "")
        params = parse_qsl(parsed.query.replace(";", "&"), keep_blank_values=False)
        safe_params = [(key, value) for key, value in params
                       if key in ("Action", "TicketID", "TicketNumber", "ID")
                       and len(value) <= 128]
        return urlunsplit((parsed.scheme, netloc, parsed.path,
                           urlencode(safe_params), ""))[:2048]
    except (ValueError, TypeError):
        return ""


def main():
    parser = argparse.ArgumentParser(description="Plenaro Classic TaskTool.db -> web import JSON")
    parser.add_argument("database", type=Path, help="Path to a COPY of TaskTool.db")
    parser.add_argument("-s", "--settings", type=Path, help="Optional old settings.json (read-only)")
    parser.add_argument("-o", "--output", type=Path, default=Path("plenaro-import.json"))
    parser.add_argument("--source-id", help="Stable identifier for this desktop installation (no secrets)")
    args = parser.parse_args()
    if args.output.resolve() in {args.database.resolve(), args.settings.resolve() if args.settings else None}:
        parser.error("Output must not overwrite the source files.")
    if not args.database.is_file():
        parser.error("Database file does not exist.")
    settings = {}
    if args.settings:
        settings = json.loads(args.settings.read_text(encoding="utf-8-sig"))
        if not isinstance(settings, dict):
            parser.error("settings.json must be a JSON object.")
    source_id = args.source_id or str(settings.get("ClientInstanceId") or "").strip()
    if not source_id:
        # Stable across repeat exports from this location; use --source-id if moved.
        source_id = sha256(str(args.database.resolve()).encode("utf-8")).hexdigest()[:32]
    if not re.fullmatch(r"[A-Za-z0-9_-]{6,64}", source_id):
        parser.error("source-id must be 6-64 letters, digits, _ or -")

    con = sqlite3.connect(args.database.resolve().as_uri() + "?mode=ro", uri=True)
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA query_only=ON")
    tables = {row[0] for row in con.execute("SELECT name FROM sqlite_master WHERE type='table'")}
    if "tasks" not in tables or "work_days" not in tables:
        parser.error("Not a supported Plenaro Classic database: missing core tables.")
    version = con.execute("SELECT version FROM schema_version ORDER BY rowid DESC LIMIT 1").fetchone() if "schema_version" in tables else None
    if not version or not 1 <= int(version[0]) <= 32:
        parser.error("Unsupported SQLite schema version.")
    def records(name):
        if name not in tables:
            return []
        rows = con.execute("SELECT * FROM " + name).fetchmany(SAFE_LIMIT + 1)
        if len(rows) > SAFE_LIMIT:
            parser.error(f"Too many {name} rows; contact maintainer for bulk import.")
        return [dict(row) for row in rows]

    warnings = []
    tasks, segments, entries, markers = [], [], [], []
    metadata, bookings, monthly = [], [], []
    existing_ids = set()
    for row in records("tasks"):
        try:
            import uuid
            task_id = str(uuid.UUID(str(row["id"])))
            if task_id in existing_ids:
                raise ValueError("duplicate task id")
            title = str(row.get("title") or "").strip()
            if not title or len(title) > 255:
                raise ValueError("invalid title")
            status = {"Planned": "planned", "Running": "active", "Done": "completed",
                      "Cancelled": "cancelled", "Canceled": "cancelled"}.get(str(row.get("status")), None)
            if status is None:
                raise ValueError("unmapped status " + str(row.get("status")))
            planned = str(row["start_local"])[:10] if row.get("start_local") else None
            if planned:
                datetime.strptime(planned, "%Y-%m-%d")
                if not 2000 <= int(planned[:4]) <= 2099:
                    raise ValueError("planned day out of range")
            tags = [x.strip() for x in str(row.get("tags") or "").split(",") if x.strip()]
            if len(tags) > 10 or any(len(x) > 32 for x in tags):
                warnings.append(f"Task {task_id}: tags truncated for web limit")
                tags = [x[:32] for x in tags[:10]]
            priority = int(row["priority"]) if row.get("priority") is not None else None
            if priority is not None and priority not in range(1, 6):
                priority = None
            description = str(row.get("description") or "")[:10000]
            created_at = parse_utc(row["created_utc"])
            if not 2000 <= int(created_at[:4]) <= 2100:
                raise ValueError("creation date out of range")
            tasks.append({"legacyId": task_id, "title": title, "description": description,
                          "status": status, "priority": priority, "tags": tags, "plannedFor": planned,
                          "createdAt": created_at})
            existing_ids.add(task_id)
            # Historical identifiers/flags are archived, never used as live
            # Outlook links, Znuny actions or access permissions.
            metadata.append({
                "taskLegacyId": task_id,
                "ticketUrl": safe_ticket_url(row.get("ticket_url")),
                "isPinned": bool(row.get("is_pinned") or 0),
                "isZnunyAssigned": bool(row.get("is_znuny_assigned", 1)),
                "isPlenaroShared": bool(row.get("is_plenaro_shared") or 0),
                "ticketState": str(row.get("ticket_state") or "")[:128],
                "ticketStateType": str(row.get("ticket_state_type") or "")[:128],
                "ticketMinutesBooked": max(0, int(row.get("ticket_minutes_booked") or 0)),
                "ticketSecondsBooked": max(0, int(row.get("ticket_seconds_booked") or 0)),
                "ticketCreatedUtc": parse_utc(row["ticket_created_utc"]) if row.get("ticket_created_utc") else None,
                "ticketChangedUtc": parse_utc(row["ticket_changed_utc"]) if row.get("ticket_changed_utc") else None,
                "localActivityUtc": parse_utc(row["local_activity_utc"]) if row.get("local_activity_utc") else None,
                "outlookEntryId": str(row.get("outlook_entry_id") or "")[:1024],
            })
        except (ValueError, KeyError, TypeError) as exc:
            warnings.append(f"Task skipped: {str(row.get('id', '?'))[:40]}: {exc}")

    attendees = {}
    for row in records("task_segment_attendees"):
        key = int(row["segment_id"])
        attendees.setdefault(key, []).append(str(row.get("email") or ""))
    for row in records("task_segments"):
        try:
            task_id = str(row["task_id"])
            if task_id not in existing_ids:
                raise ValueError("parent task skipped")
            start, end = parse_local(row["start_local"]), parse_local(row["end_local"])
            duration = datetime.fromisoformat(end.replace("Z", "+00:00")) - datetime.fromisoformat(start.replace("Z", "+00:00"))
            if not 0 < duration.total_seconds() <= 24 * 3600:
                raise ValueError("invalid segment duration")
            emails = [x for x in attendees.get(int(row["id"]), []) if x]
            if len(emails) > 20 or any(len(x) > 254 for x in emails):
                raise ValueError("invalid attendees")
            segments.append({"legacyId": str(row["id"]), "taskLegacyId": task_id,
                             "startsAt": start, "endsAt": end,
                             "note": str(row.get("note") or "")[:1000], "participants": emails})
        except (ValueError, TypeError, KeyError) as exc:
            warnings.append(f"Segment skipped: {str(row.get('id','?'))[:40]}: {exc}")
    for row in records("time_logs"):
        try:
            task_id = str(row["task_id"])
            if task_id not in existing_ids:
                raise ValueError("parent task skipped")
            if not row.get("end_utc"):
                raise ValueError("running timer cannot be migrated as completed time")
            start, end = parse_utc(row["start_utc"]), parse_utc(row["end_utc"])
            duration = datetime.fromisoformat(end.replace("Z", "+00:00")) - datetime.fromisoformat(start.replace("Z", "+00:00"))
            if not 0 < duration.total_seconds() <= 7 * 24 * 3600:
                raise ValueError("invalid work time duration")
            entries.append({"legacyId": str(row["id"]), "taskLegacyId": task_id,
                            "startsAt": start, "endsAt": end, "note": str(row.get("note") or "")[:500]})
        except (ValueError, TypeError, KeyError) as exc:
            warnings.append(f"Time log skipped: {str(row.get('id','?'))[:40]}: {exc}")
    for row in records("work_days"):
        try:
            day = str(row["day"])[:10]
            datetime.strptime(day, "%Y-%m-%d")
            day_type = str(row.get("day_type") or "Normal")
            if day_type not in ("Normal", "UL", "AM"):
                raise ValueError("unmapped day marker")
            br, ho = bool(row.get("is_br")), bool(row.get("is_ho"))
            if day_type != "Normal" or br or ho:
                markers.append({"day": day, "dayType": day_type, "isBr": br, "isHo": ho})
        except (ValueError, TypeError, KeyError) as exc:
            warnings.append(f"Marker skipped: {str(row.get('day','?'))[:30]}: {exc}")
    for row in records("ticket_time_bookings"):
        try:
            task_id = str(row.get("task_id") or "")
            if task_id not in existing_ids:
                raise ValueError("parent task skipped")
            booking_id = str(row.get("id") or "").strip()
            if not booking_id or len(booking_id) > 80:
                raise ValueError("invalid booking ID")
            seconds = int(row.get("source_seconds") or 0)
            if seconds < 0 or seconds > 10**10:
                raise ValueError("invalid source seconds")
            status = str(row.get("status") or "Pending")
            if status not in ("Pending", "Succeeded", "Failed"):
                raise ValueError("unsupported ticket-booking status")
            bookings.append({
                "legacyId": booking_id,
                "taskLegacyId": task_id,
                "ticketId": str(row.get("ticket_id") or "")[:128],
                "ticketNumber": str(row.get("ticket_number") or "")[:128],
                "bookingId": str(row.get("booking_id") or "")[:128],
                "articleId": str(row.get("article_id") or "")[:128],
                "status": status,
                "sourceSeconds": seconds,
                "bookedMinutes": float(row.get("booked_minutes") or row.get("minutes") or 0),
                "bookedAtUtc": parse_utc(row.get("booked_at_utc")),
                "shortDescription": str(row.get("short_description") or "")[:500],
                "costCenter": str(row.get("cost_center") or "")[:256],
                "orderValue": str(row.get("order_value") or "")[:256],
                "note": str(row.get("note") or "")[:500],
            })
        except (ValueError, TypeError, KeyError, OverflowError) as exc:
            warnings.append(f"Ticket booking skipped: {str(row.get('id','?'))[:40]}: {exc}")
    for row in records("monthly_report_time_adjustments"):
        try:
            import uuid
            adjustment_id = str(uuid.UUID(str(row["id"])))
            month = str(row["month"])[:10]
            parsed = datetime.strptime(month, "%Y-%m-%d")
            if parsed.day != 1 or not 2000 <= parsed.year <= 2099:
                raise ValueError("invalid month")
            seconds = int(row["seconds"])
            if seconds <= 0 or seconds > 10**10:
                raise ValueError("invalid adjustment seconds")
            monthly.append({
                "legacyId": adjustment_id, "month": month, "seconds": seconds,
                "note": str(row.get("note") or "")[:500],
                "createdAt": parse_utc(row["created_utc"]),
            })
        except (ValueError, KeyError, TypeError) as exc:
            warnings.append(f"Monthly adjustment skipped: {str(row.get('id','?'))[:40]}: {exc}")
    con.close()
    lead = settings.get("ReminderLeadMinutes")
    prefs = {"reminderLeadMinutes": lead if isinstance(lead, int) and not isinstance(lead, bool) and 0 <= lead <= 1440 else None}
    # Explicit safe-settings allowlist. Unsupported settings are not copied or activated.
    optional_bools = [
        "ShowWeekendInWeekView", "HidePastTodayItems",
        "NotificationSoundEnabled", "NotifyOnNewAssignedTickets",
        "NotifyOnTicketChanges",
    ]
    legacy_settings = {key[0].lower() + key[1:]: settings[key] for key in optional_bools
                       if type(settings.get(key)) is bool}
    duration = settings.get("DefaultSegmentDurationMinutes")
    if type(duration) is int and 0 <= duration <= 1440:
        legacy_settings["defaultSegmentDurationMinutes"] = duration
    zone = settings.get("CalendarTimeZoneId")
    if isinstance(zone, str) and 1 <= len(zone) <= 64 and re.fullmatch(r"[a-zA-Z0-9_+/-]+", zone):
        legacy_settings["calendarTimeZoneId"] = zone
    if args.settings:
        warnings.append("Nur unkritische Einstellungen werden als INAKTIVE Classic-Referenz archiviert. Passwörter, Token, API-Keys und Integrationszugänge werden nicht exportiert.")
    warnings.append("Archivierte Ticketbuchungen werden NICHT erneut an Znuny gesendet und NICHT als neue Zeiterfassung abgerechnet.")
    warnings.append("Offene Timer, Anwesenheit/Pausen, Wiki-Caches und aktive Outlook-Verknüpfungen werden nicht übernommen.")
    document = {"formatVersion": 2, "sourceId": source_id, "schemaVersion": int(version[0]),
                "tasks": tasks, "segments": segments, "timeEntries": entries,
                "dayMarkers": markers, "settings": prefs, "warnings": warnings,
                "taskMetadata": metadata, "ticketBookings": bookings,
                "monthlyAdjustments": monthly, "legacySettings": legacy_settings}
    data = json.dumps(document, ensure_ascii=False, indent=2)
    if len(data.encode("utf-8")) > 12 * 1024 * 1024:
        parser.error("Converted export is over 12 MiB; split or use an import extension.")
    args.output.write_text(data, encoding="utf-8")
    print(f"Exported tasks={len(tasks)}, segments={len(segments)}, time logs={len(entries)}, "
          f"markers={len(markers)}; warnings={len(warnings)} -> {args.output}")
    print("Review the JSON before upload. This export still contains task titles and descriptions.")


if __name__ == "__main__":
    main()
