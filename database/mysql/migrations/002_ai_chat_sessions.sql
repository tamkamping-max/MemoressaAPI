-- Memoressa MySQL schema migration: 002_ai_chat_sessions.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

CREATE TABLE ai_chat_sessions (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ai_chat_sessions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ai_chat_sessions_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE ai_chat_messages (
    `Id` char(36) NOT NULL,
    `SessionId` char(36) NOT NULL,
    `Role` varchar(16) NOT NULL,
    `Content` text NOT NULL,
    `MemoryId` char(36),
    `PhotoId` char(36),
    `MatchReasonKeysJson` text,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ai_chat_messages` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ai_chat_messages_ai_chat_sessions_SessionId` FOREIGN KEY (`SessionId`) REFERENCES ai_chat_sessions (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_ai_chat_messages_SessionId_CreatedAt` ON ai_chat_messages (`SessionId`, `CreatedAt`);

CREATE INDEX `IX_ai_chat_sessions_UserId_UpdatedAt` ON ai_chat_sessions (`UserId`, `UpdatedAt`);
