-- Memoressa schema migration: 005_upload_variants_and_storage_quota.sql

ALTER TABLE user_accounts ADD "CloudStorageUsedBytes" bigint NOT NULL DEFAULT 0;

ALTER TABLE upload_sessions ADD "S3KeyFull" text;
ALTER TABLE upload_sessions ADD "S3KeyThumbnail" text;
ALTER TABLE upload_sessions ADD "TakenAt" timestamp with time zone;
