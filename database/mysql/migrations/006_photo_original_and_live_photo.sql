-- Memoressa MySQL schema migration: 006_photo_original_and_live_photo.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

ALTER TABLE photos ADD `S3KeyFull` longtext;
ALTER TABLE photos ADD `OriginalFileName` varchar(260);
ALTER TABLE photos ADD `OriginalContentType` varchar(128);
ALTER TABLE photos ADD `IsLivePhoto` tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE photos ADD `LivePhotoVideoS3Key` longtext;
ALTER TABLE photos ADD `LivePhotoVideoFileName` varchar(260);
ALTER TABLE photos ADD `LivePhotoVideoContentType` varchar(128);

ALTER TABLE upload_sessions ADD `OriginalFileName` varchar(260);
ALTER TABLE upload_sessions ADD `OriginalContentType` varchar(128);
ALTER TABLE upload_sessions ADD `IsLivePhoto` tinyint(1) NOT NULL DEFAULT 0;
ALTER TABLE upload_sessions ADD `S3KeyLivePhotoVideo` longtext;
ALTER TABLE upload_sessions ADD `LivePhotoVideoFileName` varchar(260);
ALTER TABLE upload_sessions ADD `LivePhotoVideoContentType` varchar(128);
