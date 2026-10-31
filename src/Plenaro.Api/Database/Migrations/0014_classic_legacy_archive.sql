-- Phase 5: Preserve remaining Classic fields as an inert historical archive.
-- These records do NOT activate Znuny, Outlook or automatic ticket posting.
ALTER TABLE classic_import_items DROP CONSTRAINT classic_import_items_item_kind_check;
ALTER TABLE classic_import_items ADD CONSTRAINT classic_import_items_item_kind_check
    CHECK (item_kind IN ('task','segment','time','marker','metadata','booking','monthly'));

ALTER TABLE classic_import_batches
    ADD COLUMN legacy_settings jsonb NOT NULL DEFAULT '{}'::jsonb
    CHECK (jsonb_typeof(legacy_settings) = 'object');

CREATE TABLE classic_task_metadata (
    owner_user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    task_id uuid NOT NULL,
    legacy_data jsonb NOT NULL CHECK (jsonb_typeof(legacy_data) = 'object'),
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(owner_user_id, task_id),
    FOREIGN KEY (task_id,owner_user_id) REFERENCES tasks(id,owner_user_id) ON DELETE CASCADE
);

CREATE TABLE classic_ticket_bookings (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL,
    task_id uuid NOT NULL,
    legacy_data jsonb NOT NULL CHECK (jsonb_typeof(legacy_data) = 'object'),
    created_at timestamptz NOT NULL DEFAULT now(),
    FOREIGN KEY (task_id,owner_user_id) REFERENCES tasks(id,owner_user_id) ON DELETE CASCADE
);
CREATE INDEX ix_classic_booking_task ON classic_ticket_bookings(owner_user_id,task_id);

CREATE TABLE classic_monthly_adjustments (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    month date NOT NULL,
    seconds bigint NOT NULL CHECK (seconds > 0),
    note varchar(500) NOT NULL DEFAULT '',
    created_at timestamptz NOT NULL,
    imported_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_classic_monthly_owner ON classic_monthly_adjustments(owner_user_id,month);
