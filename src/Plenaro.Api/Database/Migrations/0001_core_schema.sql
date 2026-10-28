-- 0001: Core data model. No users or demo data are created.
-- owner_user_id will reference ASP.NET Identity users when Phase 3 adds the auth schema.
CREATE TABLE tasks (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL,
    title varchar(255) NOT NULL CHECK (length(btrim(title)) > 0),
    description text NOT NULL DEFAULT '',
    status varchar(24) NOT NULL DEFAULT 'planned'
        CHECK (status IN ('planned', 'active', 'completed', 'cancelled')),
    priority smallint CHECK (priority BETWEEN 1 AND 5),
    tags text[] NOT NULL DEFAULT ARRAY[]::text[],
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    CONSTRAINT uq_tasks_id_owner UNIQUE (id, owner_user_id)
);

CREATE TABLE task_segments (
    id uuid PRIMARY KEY,
    task_id uuid NOT NULL,
    owner_user_id uuid NOT NULL,
    starts_at timestamptz NOT NULL,
    ends_at timestamptz NOT NULL,
    note text NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_segment_period CHECK (ends_at > starts_at),
    CONSTRAINT fk_segment_task_owner FOREIGN KEY (task_id, owner_user_id)
        REFERENCES tasks (id, owner_user_id) ON DELETE CASCADE
);

CREATE TABLE time_entries (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL,
    task_id uuid,
    started_at timestamptz NOT NULL,
    ended_at timestamptz,
    entry_type varchar(16) NOT NULL DEFAULT 'work'
        CHECK (entry_type IN ('work', 'break')),
    note text NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_time_period CHECK (ended_at IS NULL OR ended_at > started_at),
    -- Keep owner_user_id when referenced task is deleted (PostgreSQL 15+).
    CONSTRAINT fk_time_task_owner FOREIGN KEY (task_id, owner_user_id)
        REFERENCES tasks (id, owner_user_id) ON DELETE SET NULL (task_id)
);

CREATE TABLE system_settings (
    setting_key varchar(128) PRIMARY KEY,
    setting_value jsonb NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);
