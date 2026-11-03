-- Each agent can use a personal reply header. Empty means use the
-- administrator's global default; no secrets or HTML stored here.
ALTER TABLE znuny_user_settings
    ADD COLUMN reply_template text NOT NULL DEFAULT '';
ALTER TABLE znuny_user_settings
    ADD CONSTRAINT ck_znuny_reply_template_length
    CHECK (char_length(reply_template) <= 10000);
