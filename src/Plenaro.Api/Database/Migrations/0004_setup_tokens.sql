-- One-time browser setup authorization
CREATE TABLE plenaro_setup_tokens (
 id integer PRIMARY KEY CHECK (id = 1),
 token_hash bytea NOT NULL,
 expires_at timestamptz NOT NULL
);
