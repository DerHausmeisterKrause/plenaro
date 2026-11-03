-- Exact-second editor and safe Znuny time booking journal.
-- Never infer remote success from a local HTTP timeout.
CREATE TABLE znuny_ticket_time_corrections (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  delta_seconds bigint NOT NULL CHECK (delta_seconds<>0 AND delta_seconds BETWEEN -86400 AND 86400),
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_znuny_ticket_time_corrections
  ON znuny_ticket_time_corrections(user_id,ticket_id);

CREATE TABLE znuny_ticket_time_bookings (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  ticket_id varchar(80) NOT NULL REFERENCES znuny_cached_tickets(ticket_id) ON DELETE CASCADE,
  source_seconds bigint NOT NULL CHECK (source_seconds>0),
  booked_minutes integer NOT NULL CHECK (booked_minutes>0 AND booked_minutes%15=0),
  cost_center varchar(200) NOT NULL DEFAULT '',
  order_value varchar(200) NOT NULL DEFAULT '',
  short_description varchar(500) NOT NULL DEFAULT 'Zeitbuchung',
  note varchar(1000) NOT NULL DEFAULT '',
  status varchar(16) NOT NULL CHECK (status IN ('pending','confirmed')),
  article_id varchar(100),
  created_at timestamptz NOT NULL DEFAULT now(),
  confirmed_at timestamptz
);
CREATE UNIQUE INDEX ix_znuny_ticket_one_pending_booking
  ON znuny_ticket_time_bookings(user_id,ticket_id) WHERE status='pending';
CREATE INDEX ix_znuny_ticket_time_booking_list
  ON znuny_ticket_time_bookings(user_id,ticket_id,created_at DESC);
