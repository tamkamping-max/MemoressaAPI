-- Memoressa MySQL schema migration: 004_frame_package_external_id.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

ALTER TABLE frame_playback_packages ADD `ExternalId` varchar(128);

CREATE UNIQUE INDEX `IX_frame_playback_packages_DisplayDeviceId_ExternalId` ON frame_playback_packages (`DisplayDeviceId`, `ExternalId`);
