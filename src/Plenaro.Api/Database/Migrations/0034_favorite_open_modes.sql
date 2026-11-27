ALTER TABLE user_favorites ADD COLUMN open_mode varchar(12) NOT NULL DEFAULT 'proxy'
    CHECK(open_mode IN ('proxy','direct','external'));
