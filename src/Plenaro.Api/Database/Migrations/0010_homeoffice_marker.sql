-- Optional home-office marker independently of UL/AM/BR.
ALTER TABLE day_markers ADD COLUMN is_ho boolean NOT NULL DEFAULT false;
ALTER TABLE day_markers DROP CONSTRAINT IF EXISTS day_markers_check;
ALTER TABLE day_markers ADD CONSTRAINT ck_day_markers_not_empty
    CHECK (day_type <> 'Normal' OR is_br OR is_ho);
