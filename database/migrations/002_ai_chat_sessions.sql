-- Memoressa schema migration: 002_ai_chat_sessions.sql
-- Source of truth for PostgreSQL schema (not EF Core migrations).

﻿CREATE TABLE ai_chat_sessions (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ai_chat_sessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ai_chat_sessions_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE TABLE ai_chat_messages (
    "Id" uuid NOT NULL,
    "SessionId" uuid NOT NULL,
    "Role" character varying(16) NOT NULL,
    "Content" text NOT NULL,
    "MemoryId" uuid,
    "PhotoId" uuid,
    "MatchReasonKeysJson" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ai_chat_messages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ai_chat_messages_ai_chat_sessions_SessionId" FOREIGN KEY ("SessionId") REFERENCES ai_chat_sessions ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_ai_chat_messages_SessionId_CreatedAt" ON ai_chat_messages ("SessionId", "CreatedAt");

CREATE INDEX "IX_ai_chat_sessions_UserId_UpdatedAt" ON ai_chat_sessions ("UserId", "UpdatedAt");



