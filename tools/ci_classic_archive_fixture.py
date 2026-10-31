#!/usr/bin/env python3
"""Synthetic CI fixture: extend v1 Classic smoke payload with v2 archive rows."""
import json
import sys

doc = json.load(sys.stdin)
legacy = doc["tasks"][0]["legacyId"]
doc.update({
    "formatVersion": 2,
    "sourceId": "ci-classic-archive-v2",
    "taskMetadata": [{
        "taskLegacyId": legacy,
        "ticketUrl": "https://tickets.example.invalid/index.pl?Action=AgentTicketZoom&TicketID=55",
        "isPinned": True, "isZnunyAssigned": True, "isPlenaroShared": False,
        "ticketState": "open", "ticketStateType": "open",
        "ticketMinutesBooked": 30, "ticketSecondsBooked": 1800,
        "outlookEntryId": "inert-legacy-id",
    }],
    "ticketBookings": [{
        "legacyId": "legacy-booking-2", "taskLegacyId": legacy,
        "ticketId": "55", "ticketNumber": "2026#55",
        "bookingId": "booking-120", "articleId": "article-4",
        "status": "Succeeded", "sourceSeconds": 1800,
        "bookedMinutes": 30.0, "bookedAtUtc": "2026-01-02T10:00:00Z",
        "shortDescription": "Altbuchung", "costCenter": "IT",
        "orderValue": "", "note": "historical only",
    }],
    "monthlyAdjustments": [{
        "legacyId": "e2b7b912-fb50-455f-bd8b-798a719cdd83",
        "month": "2026-01-01", "seconds": 5400,
        "note": "Alte Monatskorrektur", "createdAt": "2026-01-30T18:00:00Z",
    }],
    "legacySettings": {
        "showWeekendInWeekView": True, "hidePastTodayItems": False,
        "notificationSoundEnabled": True,
        "defaultSegmentDurationMinutes": 45,
        "calendarTimeZoneId": "Europe/Berlin",
    },
})
print(json.dumps(doc, ensure_ascii=False))
