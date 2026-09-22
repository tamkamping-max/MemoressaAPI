-- Memoressa schema migration: 004_frame_package_external_id.sql
-- Source of truth for PostgreSQL schema (not EF Core migrations).

﻿ALTER TABLE frame_playback_packages ADD "ExternalId" character varying(128);

CREATE UNIQUE INDEX "IX_frame_playback_packages_DisplayDeviceId_ExternalId" ON frame_playback_packages ("DisplayDeviceId", "ExternalId");



