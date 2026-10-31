-- Personal planning of centrally cached Znuny tickets, never an external
-- Znuny operation. Each segment belongs to the local Plenaro account.
CREATE TABLE znuny_ticket_segments (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  starts_at timestamptz NOT NULL,
  ends_at timestamptz NOT NULL,
  note varchar(2000) NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now(),
  CHECK (ends_at > starts_at),
  CHECK (ends_at <= starts_at + interval '7 days')
);
CREATE INDEX ix_znuny_ticket_segments_day ON znuny_ticket_segments(user_id,starts_at,ends_at);
CREATE INDEX ix_znuny_ticket_segments_ticket ON znuny_ticket_segments(user_id,ticket_id);

-- Defaults in a Znuny/OTRS installation commonly belong to agent 1
-- (root@localhost). Some installations use a different ID; admins configure it.
ALTER TABLE znuny_sync_control
  ADD COLUMN candidate_default_agent_id integer NOT NULL DEFAULT 1
    CHECK(candidate_default_agent_id > 0);
