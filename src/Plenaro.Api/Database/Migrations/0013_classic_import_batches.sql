-- Versioned import provenance. A batch owns exactly the rows it created;
-- previously existing tasks, markers and user preferences are never replaced.
CREATE TABLE classic_import_batches (
    id uuid PRIMARY KEY,
    owner_user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    source_id varchar(64) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    previous_lead smallint,
    changed_lead smallint,
    settings_imported_at timestamptz,
    settings_had_row boolean NOT NULL DEFAULT false,
    CHECK (previous_lead IS NULL OR previous_lead BETWEEN 0 AND 1440),
    CHECK (changed_lead IS NULL OR changed_lead BETWEEN 0 AND 1440)
);
CREATE INDEX ix_classic_batches_owner ON classic_import_batches(owner_user_id,created_at DESC);

CREATE TABLE classic_import_items (
    owner_user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    source_id varchar(64) NOT NULL,
    item_kind varchar(12) NOT NULL CHECK (item_kind IN ('task','segment','time','marker')),
    source_key varchar(80) NOT NULL,
    target_id uuid NOT NULL,
    batch_id uuid NOT NULL REFERENCES classic_import_batches(id) ON DELETE CASCADE,
    snapshot_hash char(32) NOT NULL,
    PRIMARY KEY(owner_user_id,source_id,item_kind,source_key)
);
CREATE INDEX ix_classic_items_batch ON classic_import_items(batch_id);
