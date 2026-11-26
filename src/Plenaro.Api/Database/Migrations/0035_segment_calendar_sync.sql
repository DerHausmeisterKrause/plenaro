-- Durable per-owner calendar outbox. Existing segments are NOT mass-mailed on migration.
-- A new save or explicit Outlook retry queues the currently persisted segment.
CREATE TABLE segment_calendar_sync (
    kind varchar(8) NOT NULL CHECK(kind IN ('task','ticket')),
    segment_id uuid NOT NULL,
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    event_id text,
    revision bigint NOT NULL DEFAULT 1,
    status varchar(16) NOT NULL DEFAULT 'pending'
        CHECK(status IN ('pending','syncing','synced','error','removed')),
    last_error text NOT NULL DEFAULT '',
    create_attempted boolean NOT NULL DEFAULT false,
    next_attempt timestamptz NOT NULL DEFAULT now(),
    lease_until timestamptz,
    lease_token uuid,
    PRIMARY KEY(kind,segment_id,user_id)
);
CREATE INDEX ix_segment_calendar_due ON segment_calendar_sync(next_attempt)
    WHERE status IN ('pending','syncing','error');
-- Keep deletion intent even if the parent task or ticket is deleted.
CREATE FUNCTION plenaro_segment_calendar_deleted() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME='task_segments' THEN
        UPDATE segment_calendar_sync SET revision=revision+1,status='pending',
            next_attempt=now(),last_error=''
        WHERE kind='task' AND segment_id=OLD.id AND user_id=OLD.owner_user_id;
    ELSE
        UPDATE segment_calendar_sync SET revision=revision+1,status='pending',
            next_attempt=now(),last_error=''
        WHERE kind='ticket' AND segment_id=OLD.id AND user_id=OLD.user_id;
    END IF;
    RETURN OLD;
END;
$$;
CREATE TRIGGER task_segment_calendar_deleted AFTER DELETE ON task_segments
    FOR EACH ROW EXECUTE FUNCTION plenaro_segment_calendar_deleted();
CREATE TRIGGER ticket_segment_calendar_deleted AFTER DELETE ON znuny_ticket_segments
    FOR EACH ROW EXECUTE FUNCTION plenaro_segment_calendar_deleted();
