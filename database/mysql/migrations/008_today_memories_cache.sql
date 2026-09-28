-- Memoressa MySQL schema migration: 008_today_memories_cache.sql

CREATE TABLE today_memories_cache (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CacheDate` date NOT NULL,
    `Strategy` int NOT NULL,
    `ItemsJson` longtext NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_today_memories_cache` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_today_memories_cache_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE
);

CREATE UNIQUE INDEX `IX_today_memories_cache_FamilyId_CacheDate` ON today_memories_cache (`FamilyId`, `CacheDate`);
