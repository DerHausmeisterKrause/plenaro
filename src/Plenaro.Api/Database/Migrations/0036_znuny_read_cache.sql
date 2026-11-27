-- Only central-service responses may be persisted in these shared caches.
-- Attachments contain metadata only, never attachment file contents.
CREATE TABLE znuny_ticket_detail_cache (
    ticket_id varchar(80) PRIMARY KEY REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
    config_key text NOT NULL,
    changed_at timestamptz,
    fetched_at timestamptz NOT NULL,
    requested_at timestamptz,
    retry_after timestamptz,
    payload jsonb
);
CREATE INDEX ix_znuny_ticket_detail_requested ON znuny_ticket_detail_cache(requested_at)
    WHERE requested_at IS NOT NULL;
CREATE TABLE znuny_shared_read_cache (
    kind varchar(16) PRIMARY KEY CHECK(kind IN ('agents','options')),
    config_key text NOT NULL,
    fetched_at timestamptz NOT NULL,
    payload jsonb NOT NULL
);
-- Resetting the central source must clear shared reference data as well.
CREATE FUNCTION plenaro_znuny_read_cache_source_changed() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.source_api_url IS DISTINCT FROM OLD.source_api_url OR
       NEW.service_username IS DISTINCT FROM OLD.service_username THEN
        DELETE FROM znuny_ticket_detail_cache;
        DELETE FROM znuny_shared_read_cache;
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER znuny_read_cache_source_changed AFTER UPDATE ON znuny_sync_control
    FOR EACH ROW EXECUTE FUNCTION plenaro_znuny_read_cache_source_changed();
