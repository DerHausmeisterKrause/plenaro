-- Permanently prevent a deleted Entra identity from re-registering itself on
-- the next Microsoft sign-in. Only the verified tid:oid provider key is stored;
-- no access token, email or personal profile is retained.
CREATE TABLE IF NOT EXISTS entra_deleted_identities (
    provider_key varchar(128) PRIMARY KEY,
    deleted_at timestamptz NOT NULL DEFAULT now()
);
