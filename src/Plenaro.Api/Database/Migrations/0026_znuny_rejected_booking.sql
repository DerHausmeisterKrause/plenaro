-- Definitively rejected remote writes are not ambiguous. Record the
-- rejection for audit and let the user correct the request before retrying.
-- Never clear "pending" after timeout, disconnection or remote HTTP 5xx.
ALTER TABLE znuny_ticket_time_bookings
  DROP CONSTRAINT znuny_ticket_time_bookings_status_check;
ALTER TABLE znuny_ticket_time_bookings
  ADD CONSTRAINT znuny_ticket_time_bookings_status_check
  CHECK(status IN ('pending','confirmed','rejected'));
ALTER TABLE znuny_ticket_time_bookings
  ADD COLUMN rejection_code varchar(100);
