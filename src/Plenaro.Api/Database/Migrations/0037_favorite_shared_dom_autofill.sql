ALTER TABLE user_favorites ADD COLUMN shared_dom_autofill boolean NOT NULL DEFAULT false
    CHECK (NOT shared_dom_autofill OR (auto_login AND open_mode='proxy'));
