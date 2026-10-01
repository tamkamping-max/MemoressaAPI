-- Optional activity-level privacy (aligns with upload privacyScope; App may read instead of inferring from members).
ALTER TABLE activity_albums ADD `PrivacyScope` integer NOT NULL DEFAULT 0;
