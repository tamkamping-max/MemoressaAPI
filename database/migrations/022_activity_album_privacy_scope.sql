ALTER TABLE activity_albums
    ADD COLUMN IF NOT EXISTS "PrivacyScope" integer NOT NULL DEFAULT 0;
