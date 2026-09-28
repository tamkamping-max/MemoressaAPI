-- Memoressa MySQL schema migration: 010_user_ai_settings_key_varchar.sql
-- Fixes Error 1170: BLOB/TEXT column 'Key' used in key specification without a key length

ALTER TABLE user_ai_settings MODIFY `Key` varchar(128) NOT NULL;
