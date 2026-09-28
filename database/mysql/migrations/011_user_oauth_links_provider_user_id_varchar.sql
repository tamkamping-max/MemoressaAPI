-- Memoressa MySQL schema migration: 011_user_oauth_links_provider_user_id_varchar.sql

ALTER TABLE user_oauth_links MODIFY `ProviderUserId` varchar(256) NOT NULL;
