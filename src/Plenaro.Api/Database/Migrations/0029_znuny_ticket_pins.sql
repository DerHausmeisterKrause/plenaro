-- User-specific pinned Znuny tickets. Stored independently of the
-- central ticket sync, so pinning does not modify tickets in OTRS.
CREATE TABLE znuny_ticket_pins (
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  pinned_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY(user_id,ticket_id)
);
CREATE INDEX ix_znuny_ticket_pins_user_recent
  ON znuny_ticket_pins(user_id,pinned_at DESC);
