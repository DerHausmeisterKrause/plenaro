-- Exactly one instance-wide ticket cache. No user-by-user Znuny sync.
-- Metadata may be sensitive; API access is gated by an *admin-approved* agent binding.
CREATE TABLE znuny_cached_tickets (
  ticket_id varchar(80) PRIMARY KEY,
  ticket_number varchar(100) NOT NULL DEFAULT '',
  title varchar(500) NOT NULL DEFAULT '',
  queue varchar(200) NOT NULL DEFAULT '',
  state varchar(100) NOT NULL DEFAULT '',
  state_type varchar(100) NOT NULL DEFAULT '',
  owner_agent_id integer,
  responsible_agent_id integer,
  created_at timestamptz,
  changed_at timestamptz,
  synced_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_znuny_ticket_owner ON znuny_cached_tickets(owner_agent_id,synced_at DESC);
CREATE INDEX ix_znuny_ticket_responsible ON znuny_cached_tickets(responsible_agent_id,synced_at DESC);
CREATE INDEX ix_znuny_ticket_changed ON znuny_cached_tickets(changed_at DESC);
-- Browser supplied agent_id is *not* an authorization proof.
CREATE TABLE znuny_approved_agent_bindings (
  user_id uuid PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  agent_id integer NOT NULL CHECK (agent_id > 0),
  approved_at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE znuny_sync_control (
  id boolean PRIMARY KEY DEFAULT true CHECK(id),
  enabled boolean NOT NULL DEFAULT false,
  service_username varchar(256) NOT NULL DEFAULT '',
  source_api_url varchar(2048),
  initial_lookback_days integer NOT NULL DEFAULT 365 CHECK (initial_lookback_days BETWEEN 7 AND 3650),
  delta_interval_minutes integer NOT NULL DEFAULT 15 CHECK (delta_interval_minutes BETWEEN 3 AND 1440),
  open_interval_minutes integer NOT NULL DEFAULT 360 CHECK (open_interval_minutes BETWEEN 15 AND 10080),
  max_requests_per_cycle integer NOT NULL DEFAULT 200 CHECK (max_requests_per_cycle BETWEEN 20 AND 1000),
  -- Empty bootstrap_cursor means initial bootstrap has not begun. A cursor
  -- value progresses one day at a time and is durable across restarts.
  bootstrap_cursor timestamptz,
  bootstrap_end timestamptz,
  bootstrap_finished_at timestamptz,
  delta_watermark timestamptz,
  last_delta_at timestamptz,
  last_open_at timestamptz,
  open_cursor timestamptz,
  last_error varchar(300),
  last_attempt_at timestamptz,
  updated_at timestamptz NOT NULL DEFAULT now()
);
INSERT INTO znuny_sync_control(id) VALUES(true);
