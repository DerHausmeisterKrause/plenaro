-- A favorite is now a self-owned HTTPS upstream, not an administrator
-- approval request. Preserve existing bookmark IDs and encrypted secrets.
ALTER TABLE favorite_apps
  ADD COLUMN owner_user_id uuid REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE;
-- Preserve already admin-approved intranet origins during migration, without
-- allowing users to retarget that trust to a new internal server.
ALTER TABLE favorite_apps
  ADD COLUMN allow_private_upstream boolean NOT NULL DEFAULT false;
CREATE INDEX ix_favorite_apps_personal_owner
  ON favorite_apps(owner_user_id) WHERE owner_user_id IS NOT NULL;

-- Wiki credentials are NOT bookmarks. A legacy vault secret remains bound
-- to its original AAD name until that user explicitly replaces the password.
CREATE TABLE wiki_user_connections (
  id uuid PRIMARY KEY,
  user_id uuid NOT NULL UNIQUE REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  username varchar(180) NOT NULL,
  legacy_secret_name varchar(64)
);
INSERT INTO wiki_user_connections(id,user_id,username,legacy_secret_name)
SELECT f.id,f.user_id,f.username,
  'fav.' || replace(f.id::text,'-','') || '.password'
FROM user_favorites f
JOIN favorite_apps a ON a.id=f.app_id AND a.slug='wiki'
ON CONFLICT(user_id) DO NOTHING;

-- Wiki is now a first-class integration, never a visible personal favorite.
DELETE FROM user_favorites f USING favorite_apps a
WHERE f.app_id=a.id AND a.slug='wiki';

-- Each existing approved-site bookmark becomes a personal upstream with an
-- immutable unique proxy alias. Keep bookmark IDs, order, paths and passwords.
DO $$
DECLARE entry record; new_app_id uuid;
BEGIN
  FOR entry IN
    SELECT f.id,f.user_id,f.name,a.upstream_url
    FROM user_favorites f
    JOIN favorite_apps a ON a.id=f.app_id
    WHERE a.slug<>'wiki'
  LOOP
    new_app_id=gen_random_uuid();
    INSERT INTO favorite_apps
      (id,slug,name,upstream_url,enabled,available_to_all,
       owner_user_id,allow_private_upstream)
    VALUES(new_app_id,'site-'||replace(entry.id::text,'-',''),
      entry.name,entry.upstream_url,true,false,entry.user_id,true);
    UPDATE user_favorites SET app_id=new_app_id WHERE id=entry.id;
  END LOOP;
END $$;
