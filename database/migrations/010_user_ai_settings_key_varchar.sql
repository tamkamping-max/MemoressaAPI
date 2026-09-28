-- Memoressa schema migration: 010_user_ai_settings_key_varchar.sql (PostgreSQL: optional alignment)

ALTER TABLE user_ai_settings ALTER COLUMN "Key" TYPE character varying(128);
