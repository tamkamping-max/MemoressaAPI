-- Memoressa MySQL schema migration: 007_live_photo_quota_bytes.sql

ALTER TABLE photos ADD `OriginalStillFileSizeBytes` bigint;
ALTER TABLE photos ADD `LivePhotoVideoFileSizeBytes` bigint NOT NULL DEFAULT 0;

ALTER TABLE upload_sessions ADD `OriginalStillFileSizeBytes` bigint;
ALTER TABLE upload_sessions ADD `LivePhotoVideoFileSizeBytes` bigint NOT NULL DEFAULT 0;
