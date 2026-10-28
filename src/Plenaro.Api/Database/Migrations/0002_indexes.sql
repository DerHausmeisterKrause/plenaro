-- 0002: Indexes for owner-isolated queries, scheduling and time reports.
CREATE INDEX ix_tasks_owner_status ON tasks (owner_user_id, status);
CREATE INDEX ix_tasks_owner_updated ON tasks (owner_user_id, updated_at DESC);
CREATE INDEX ix_segments_owner_start ON task_segments (owner_user_id, starts_at);
CREATE INDEX ix_time_entries_owner_start ON time_entries (owner_user_id, started_at DESC);
CREATE INDEX ix_time_entries_task ON time_entries (task_id)
    WHERE task_id IS NOT NULL;
