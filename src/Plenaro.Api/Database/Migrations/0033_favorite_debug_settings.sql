-- Per-administrator, opt-in diagnostics for the existing favorite proxy.
-- A row cannot grant admin permissions: endpoints enforce role authorization.
-- No captured credentials, URLs, payloads or HTTP headers are persisted.
CREATE TABLE favorite_debug_settings (
  user_id uuid PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  enabled boolean NOT NULL DEFAULT false,
  updated_at timestamptz NOT NULL DEFAULT now()
);
