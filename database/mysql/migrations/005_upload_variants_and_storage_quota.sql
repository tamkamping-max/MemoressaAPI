-- Memoressa MySQL schema migration: 005_upload_variants_and_storage_quota.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

ALTER TABLE user_accounts ADD `CloudStorageUsedBytes` bigint NOT NULL DEFAULT 0;

ALTER TABLE upload_sessions ADD `S3KeyFull` text;
ALTER TABLE upload_sessions ADD `S3KeyThumbnail` text;
ALTER TABLE upload_sessions ADD `TakenAt` datetime(6);
