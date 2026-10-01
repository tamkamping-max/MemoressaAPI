-- Memoressa MySQL schema migration: 012_upload_compressed_uses_full.sql

ALTER TABLE upload_sessions ADD `CompressedUsesFullOriginal` tinyint(1) NOT NULL DEFAULT 0;
