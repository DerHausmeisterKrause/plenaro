-- Candidate user IDs are assigned only through admin user management.
-- Previously self-managed candidate IDs cannot be trusted as admin approvals.
-- Existing user-set values are cleared once; admins must explicitly approve again.
ALTER TABLE znuny_user_settings
    ALTER COLUMN candidate_user_id DROP DEFAULT,
    ALTER COLUMN candidate_user_id DROP NOT NULL;

-- NULL is a deliberate absence of approval; do not silently fall back to agent 1.
ALTER TABLE znuny_user_settings
    DROP CONSTRAINT IF EXISTS znuny_user_settings_candidate_user_id_check;
ALTER TABLE znuny_user_settings
    ADD CONSTRAINT znuny_user_settings_candidate_user_id_check
    CHECK (candidate_user_id IS NULL OR candidate_user_id > 0);

-- Reject legacy self-service assignments rather than grandfathering in permissions.
UPDATE znuny_user_settings SET candidate_user_id=NULL, updated_at=now()
WHERE candidate_user_id IS NOT NULL;
