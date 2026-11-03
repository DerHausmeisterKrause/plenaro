-- Account deletion is not an access ban. Entra OIDC is allowed to create a
-- fresh unprivileged local Plenaro account after the old one was deleted.
-- Clear ALL historic revocations, including accounts deleted with release #39.
-- The old table does not have external references and is no longer used.
DROP TABLE IF EXISTS entra_deleted_identities;
