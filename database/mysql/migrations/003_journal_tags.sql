-- Memoressa MySQL schema migration: 003_journal_tags.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

CREATE TABLE journal_tags (
    `Id` char(36) NOT NULL,
    `OwnerUserId` char(36) NOT NULL,
    `LabelKey` varchar(128) NOT NULL,
    `ColorArgb` integer NOT NULL,
    `IsCustom` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_journal_tags` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_journal_tags_user_accounts_OwnerUserId` FOREIGN KEY (`OwnerUserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_journal_tags_OwnerUserId` ON journal_tags (`OwnerUserId`);
