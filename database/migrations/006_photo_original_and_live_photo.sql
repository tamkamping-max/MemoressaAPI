-- Memoressa schema migration: 006_photo_original_and_live_photo.sql

ALTER TABLE photos ADD "S3KeyFull" text;
ALTER TABLE photos ADD "OriginalFileName" character varying(260);
ALTER TABLE photos ADD "OriginalContentType" character varying(128);
ALTER TABLE photos ADD "IsLivePhoto" boolean NOT NULL DEFAULT false;
ALTER TABLE photos ADD "LivePhotoVideoS3Key" text;
ALTER TABLE photos ADD "LivePhotoVideoFileName" character varying(260);
ALTER TABLE photos ADD "LivePhotoVideoContentType" character varying(128);

ALTER TABLE upload_sessions ADD "OriginalFileName" character varying(260);
ALTER TABLE upload_sessions ADD "OriginalContentType" character varying(128);
ALTER TABLE upload_sessions ADD "IsLivePhoto" boolean NOT NULL DEFAULT false;
ALTER TABLE upload_sessions ADD "S3KeyLivePhotoVideo" text;
ALTER TABLE upload_sessions ADD "LivePhotoVideoFileName" character varying(260);
ALTER TABLE upload_sessions ADD "LivePhotoVideoContentType" character varying(128);
