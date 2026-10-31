-- Only admin-approved agent bindings in znuny_approved_agent_bindings are authoritative.
-- Legacy self-declared znuny_user_settings.agent_id was never an access check;
-- remove it so future API code cannot accidentally use it as identity.
-- Existing approved mappings are retained without modification.
ALTER TABLE znuny_user_settings DROP COLUMN agent_id;
