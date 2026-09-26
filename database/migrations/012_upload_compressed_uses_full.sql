-- Memoressa schema migration: 012_upload_compressed_uses_full.sql

ALTER TABLE upload_sessions ADD "CompressedUsesFullOriginal" boolean NOT NULL DEFAULT false;
