-- User-specific day markers and leave/BR budgets. No time punch or break tracking.
-- NULL budgets mean "not configured"; zero is an explicitly exhausted allowance.
CREATE TABLE day_marker_budgets (
    user_id uuid PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    annual_ul_days smallint CHECK (annual_ul_days BETWEEN 0 AND 366),
    monthly_br_days smallint CHECK (monthly_br_days BETWEEN 0 AND 31),
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE day_markers (
    user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
    day date NOT NULL,
    day_type varchar(6) NOT NULL DEFAULT 'Normal'
        CHECK (day_type IN ('Normal', 'UL', 'AM')),
    is_br boolean NOT NULL DEFAULT false,
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY(user_id, day),
    CHECK (day_type <> 'Normal' OR is_br)
);

CREATE INDEX ix_day_markers_day ON day_markers (user_id, day);
