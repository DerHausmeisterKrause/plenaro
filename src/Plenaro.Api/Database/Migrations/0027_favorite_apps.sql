CREATE TABLE favorite_apps (id uuid PRIMARY KEY, slug varchar(40) NOT NULL UNIQUE, name varchar(120) NOT NULL, upstream_url text NOT NULL, enabled boolean NOT NULL DEFAULT true, available_to_all boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now());
CREATE TABLE favorite_app_users (app_id uuid NOT NULL REFERENCES favorite_apps(id) ON DELETE CASCADE, user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE, PRIMARY KEY(app_id,user_id));
CREATE TABLE user_favorites (id uuid PRIMARY KEY, user_id uuid NOT NULL REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE, app_id uuid NOT NULL REFERENCES favorite_apps(id) ON DELETE CASCADE, name varchar(120) NOT NULL, path varchar(2048) NOT NULL DEFAULT '/', username varchar(180) NOT NULL DEFAULT '', auto_login boolean NOT NULL DEFAULT false, enabled boolean NOT NULL DEFAULT true, sort_order integer NOT NULL DEFAULT 0, created_at timestamptz NOT NULL DEFAULT now());
CREATE INDEX ix_user_favorites_owner ON user_favorites(user_id,sort_order,id);
-- Host-only browser cookies mean one active account per app origin/user.
CREATE UNIQUE INDEX ux_user_favorites_user_app ON user_favorites(user_id,app_id);
