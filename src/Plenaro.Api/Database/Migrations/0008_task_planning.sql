-- Phase 2: add an optional planning date to existing owner-isolated tasks.
ALTER TABLE tasks ADD COLUMN planned_for date;
CREATE INDEX ix_tasks_owner_planned ON tasks(owner_user_id, planned_for);
