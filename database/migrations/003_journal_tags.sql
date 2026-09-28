-- Memoressa schema migration: 003_journal_tags.sql
-- Source of truth for PostgreSQL schema (not EF Core migrations).

﻿CREATE TABLE journal_tags (
    "Id" uuid NOT NULL,
    "OwnerUserId" uuid NOT NULL,
    "LabelKey" character varying(128) NOT NULL,
    "ColorArgb" integer NOT NULL,
    "IsCustom" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_journal_tags" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_journal_tags_user_accounts_OwnerUserId" FOREIGN KEY ("OwnerUserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_journal_tags_OwnerUserId" ON journal_tags ("OwnerUserId");



