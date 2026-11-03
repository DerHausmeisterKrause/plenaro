-- Task-bound time tracking only; no attendance, breaks or working-day accounting.
-- The unique partial index prevents simultaneous running timers for one local user.
CREATE UNIQUE INDEX ix_time_entries_one_active_per_user
    ON time_entries(owner_user_id)
    WHERE ended_at IS NULL AND task_id IS NOT NULL AND entry_type='work';

CREATE TABLE task_time_adjustments (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL,
    task_id uuid NOT NULL,
    delta_minutes integer NOT NULL CHECK (delta_minutes BETWEEN -1440 AND 1440 AND delta_minutes <> 0),
    note varchar(500) NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_adjustment_task_owner FOREIGN KEY (task_id, owner_user_id)
        REFERENCES tasks(id, owner_user_id) ON DELETE CASCADE
);
CREATE INDEX ix_adjustments_task ON task_time_adjustments(owner_user_id,task_id,created_at DESC);

ALTER TABLE task_segments ADD COLUMN participants text[] NOT NULL DEFAULT ARRAY[]::text[];
