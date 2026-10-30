-- Local task-segment reminders. Never stores work attendance or break data.
-- A read receipt is tied to a specific start time, so rescheduling creates fresh reminders.
CREATE TABLE user_reminder_settings (
    owner_user_id uuid PRIMARY KEY REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    enabled boolean NOT NULL DEFAULT true,
    lead_minutes smallint NOT NULL DEFAULT 2 CHECK (lead_minutes BETWEEN 0 AND 1440),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE task_reminder_receipts (
    owner_user_id uuid NOT NULL REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
    segment_id uuid NOT NULL REFERENCES task_segments(id) ON DELETE CASCADE,
    starts_at timestamptz NOT NULL,
    kind varchar(8) NOT NULL CHECK (kind IN ('lead', 'start')),
    read_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (owner_user_id, segment_id, starts_at, kind)
);

CREATE INDEX ix_segment_owner_reminders ON task_segments(owner_user_id, starts_at);
