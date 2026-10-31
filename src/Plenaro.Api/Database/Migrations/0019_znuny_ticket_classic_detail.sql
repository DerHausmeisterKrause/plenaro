-- Desktop-compatible personal task notes and timer for a cached Znuny ticket.
-- The Znuny authoritative ticket is NEVER modified here. Scoped by local user.
CREATE TABLE znuny_ticket_workspaces (
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  description varchar(10000) NOT NULL DEFAULT '',
  ticket_url varchar(2048) NOT NULL DEFAULT '',
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY(user_id,ticket_id)
);

CREATE TABLE znuny_ticket_time_entries (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  started_at timestamptz NOT NULL,
  ended_at timestamptz,
  CHECK (ended_at IS NULL OR ended_at > started_at)
);
CREATE UNIQUE INDEX ix_znuny_ticket_single_running_timer ON znuny_ticket_time_entries(user_id)
  WHERE ended_at IS NULL;
CREATE INDEX ix_znuny_ticket_time_entries ON znuny_ticket_time_entries(user_id,ticket_id,started_at);

CREATE TABLE znuny_ticket_time_adjustments (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  delta_minutes integer NOT NULL CHECK (delta_minutes BETWEEN -1440 AND 1440 AND delta_minutes<>0),
  note varchar(500) NOT NULL DEFAULT '',
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_znuny_ticket_adjustments ON znuny_ticket_time_adjustments(user_id,ticket_id,created_at);

-- Fully editable Classic plan: participants are informational until Microsoft
-- calendar invitations are implemented.
ALTER TABLE znuny_ticket_segments
 ADD COLUMN participants text[] NOT NULL DEFAULT ARRAY[]::text[];
