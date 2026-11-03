-- Znuny / OTRS personal connection profile. No passwords, session IDs or API keys
-- are persisted in this table. Secrets live in the existing AES-GCM vault.
-- Shared server endpoints and routes use system_settings('tickets.znuny').
CREATE TABLE znuny_user_settings (
    user_id uuid PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    username varchar(256) NOT NULL DEFAULT '',
    agent_id integer NOT NULL DEFAULT 0 CHECK (agent_id BETWEEN 0 AND 2147483647),
    candidate_user_id integer NOT NULL DEFAULT 1 CHECK (candidate_user_id BETWEEN 1 AND 2147483647),
    candidate_keywords varchar(2000) NOT NULL DEFAULT '',
    candidate_exclude_keywords varchar(2000) NOT NULL DEFAULT '',
    include_owner boolean NOT NULL DEFAULT true,
    include_responsible boolean NOT NULL DEFAULT true,
    notify_new_assigned boolean NOT NULL DEFAULT true,
    notify_ticket_changes boolean NOT NULL DEFAULT true,
    show_all_candidate_pool boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now()
);
