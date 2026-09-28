-- Memoressa schema migration: 008_today_memories_cache.sql

CREATE TABLE today_memories_cache (
    "Id" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "CacheDate" date NOT NULL,
    "Strategy" integer NOT NULL,
    "ItemsJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_today_memories_cache" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_today_memories_cache_families_FamilyId" FOREIGN KEY ("FamilyId") REFERENCES families ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_today_memories_cache_FamilyId_CacheDate" ON today_memories_cache ("FamilyId", "CacheDate");
