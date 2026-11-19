-- Reserve the actual booked (15-minute-rounded) Znuny time against the
-- authorized AGENT, not just the Plenaro user or the single ticket.
-- Preserve the original booking date and agent identity when an approval
-- binding changes later. Past records are backfilled with current approved
-- bindings where available; orphan historical rows remain unattributed.
ALTER TABLE znuny_ticket_time_bookings
    ADD COLUMN agent_id integer,
    ADD COLUMN booking_day date;

UPDATE znuny_ticket_time_bookings b
SET agent_id = a.agent_id
FROM znuny_approved_agent_bindings a
WHERE a.user_id = b.user_id;

UPDATE znuny_ticket_time_bookings
SET booking_day = (created_at AT TIME ZONE 'Europe/Berlin')::date;

ALTER TABLE znuny_ticket_time_bookings
    ALTER COLUMN booking_day SET NOT NULL;

ALTER TABLE znuny_ticket_time_bookings
    ADD CONSTRAINT znuny_ticket_booking_agent_positive
        CHECK (agent_id IS NULL OR agent_id > 0);

-- Rejected bookings do not use capacity. Pending bookings MUST reserve
-- capacity because a timeout may mean Znuny already accepted them.
CREATE INDEX ix_znuny_booking_agent_day_active
ON znuny_ticket_time_bookings(agent_id,booking_day)
WHERE status IN ('confirmed','pending');
