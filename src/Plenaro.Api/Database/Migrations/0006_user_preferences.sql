-- 0006: Individuelle Einstellungen sind ausschließlich dem angemeldeten Benutzer zugeordnet.
-- Globale Einstellungen verwenden bereits die Tabelle system_settings aus 0001.
CREATE TABLE user_preferences (
  user_id uuid PRIMARY KEY REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE,
  preferences jsonb NOT NULL DEFAULT '{}'::jsonb,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_preferences_object CHECK (jsonb_typeof(preferences) = 'object')
);
