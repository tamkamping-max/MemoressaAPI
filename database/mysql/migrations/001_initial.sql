-- Memoressa MySQL schema migration: 001_initial.sql
-- Source of truth for local MySQL schema (not EF Core migrations).

CREATE TABLE user_accounts (
    `Id` char(36) NOT NULL,
    `Email` varchar(320) NOT NULL,
    `PasswordHash` text,
    `Nickname` text,
    `AvatarUrl` text,
    `Generation` integer,
    `ProfileCityId` text,
    `BirthDate` datetime(6),
    `NotificationsEnabled` tinyint(1) NOT NULL,
    `OnboardingComplete` tinyint(1) NOT NULL,
    `Locale` varchar(16) NOT NULL DEFAULT 'en',
    `DeletionScheduledAt` datetime(6),
    `DeletedAt` datetime(6),
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_user_accounts` PRIMARY KEY (`Id`)
);

CREATE TABLE ai_analysis_jobs (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `Status` integer NOT NULL,
    `PhotoIdsJson` text NOT NULL,
    `ResultJson` text,
    `ErrorMessage` text,
    `CompletedAt` datetime(6),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_ai_analysis_jobs` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_ai_analysis_jobs_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE families (
    `Id` char(36) NOT NULL,
    `Name` varchar(200) NOT NULL,
    `OwnerUserId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_families` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_families_user_accounts_OwnerUserId` FOREIGN KEY (`OwnerUserId`) REFERENCES user_accounts (`Id`) ON DELETE RESTRICT
);

CREATE TABLE friends (
    `Id` char(36) NOT NULL,
    `OwnerUserId` char(36) NOT NULL,
    `FriendUserId` char(36),
    `Name` text NOT NULL,
    `AvatarUrl` text,
    `FrameLinked` tinyint(1) NOT NULL,
    `SharedMemoryCount` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_friends` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_friends_user_accounts_OwnerUserId` FOREIGN KEY (`OwnerUserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE notifications (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Message` text NOT NULL,
    `AvatarUrl` text,
    `IsRead` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_notifications` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_notifications_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE password_reset_tokens (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Token` varchar(512) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_password_reset_tokens` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_password_reset_tokens_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE refresh_tokens (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Token` varchar(512) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `RevokedAt` datetime(6),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_refresh_tokens` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_refresh_tokens_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE upload_sessions (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `MediaKind` integer NOT NULL,
    `FileName` text NOT NULL,
    `ContentType` text NOT NULL,
    `FileSizeBytes` bigint NOT NULL,
    `S3Key` text NOT NULL,
    `Status` integer NOT NULL,
    `PrivacyScope` integer NOT NULL,
    `SharedAlbumId` char(36),
    `ExpiresAt` datetime(6) NOT NULL,
    `ResultPhotoId` char(36),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_upload_sessions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_upload_sessions_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE user_ai_settings (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Key` varchar(128) NOT NULL,
    `Value` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_user_ai_settings` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_user_ai_settings_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE user_oauth_links (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Provider` integer NOT NULL,
    `ProviderUserId` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_user_oauth_links` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_user_oauth_links_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE family_members (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `Name` varchar(200) NOT NULL,
    `Nickname` text,
    `BirthDate` datetime(6),
    `Generation` integer NOT NULL,
    `Relationship` text,
    `AvatarUrl` text,
    `FaceRecognitionEnabled` tinyint(1) NOT NULL,
    `LinkedUserId` char(36),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_members` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_members_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE
);

CREATE TABLE family_memberships (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Role` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_memberships` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_memberships_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_family_memberships_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE TABLE family_moments (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `Name` text NOT NULL,
    `EventType` integer NOT NULL,
    `Date` datetime(6) NOT NULL,
    `Description` text,
    `IsAiDiscovered` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_moments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_moments_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE
);

CREATE TABLE memories (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CreatedByUserId` char(36) NOT NULL,
    `Title` varchar(500) NOT NULL,
    `Description` text,
    `Type` integer NOT NULL,
    `TextContent` text,
    `StartDate` datetime(6),
    `EndDate` datetime(6),
    `Location` text,
    `EventType` integer,
    `Generation` integer,
    `IsAiGenerated` tinyint(1) NOT NULL,
    `IsTodayHighlight` tinyint(1) NOT NULL,
    `BackgroundMusicId` text,
    `Visibility` integer NOT NULL,
    `WeatherSummary` text,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_memories` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_memories_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_memories_user_accounts_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES user_accounts (`Id`) ON DELETE RESTRICT
);

CREATE TABLE shared_albums (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `ExternalId` varchar(128) NOT NULL,
    `Name` text NOT NULL,
    `Subtitle` text,
    `AlbumType` integer NOT NULL,
    `IsOwn` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_shared_albums` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_shared_albums_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE
);

CREATE TABLE family_moment_members (
    `Id` char(36) NOT NULL,
    `FamilyMomentId` char(36) NOT NULL,
    `FamilyMemberId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_moment_members` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_moment_members_family_members_FamilyMemberId` FOREIGN KEY (`FamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_family_moment_members_family_moments_FamilyMomentId` FOREIGN KEY (`FamilyMomentId`) REFERENCES family_moments (`Id`) ON DELETE CASCADE
);

CREATE TABLE display_devices (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `BoundByUserId` char(36),
    `Name` varchar(200) NOT NULL,
    `QrCode` varchar(64) NOT NULL,
    `Status` integer NOT NULL,
    `CurrentMemoryId` char(36),
    `LastSeenAt` datetime(6),
    `SettingsJson` text,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_display_devices` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_display_devices_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_display_devices_memories_CurrentMemoryId` FOREIGN KEY (`CurrentMemoryId`) REFERENCES memories (`Id`) ON DELETE SET NULL
);

CREATE TABLE memory_members (
    `Id` char(36) NOT NULL,
    `MemoryId` char(36) NOT NULL,
    `FamilyMemberId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_memory_members` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_memory_members_family_members_FamilyMemberId` FOREIGN KEY (`FamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_memory_members_memories_MemoryId` FOREIGN KEY (`MemoryId`) REFERENCES memories (`Id`) ON DELETE CASCADE
);

CREATE TABLE today_highlight_cache (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CacheDate` date NOT NULL,
    `MemoryId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_today_highlight_cache` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_today_highlight_cache_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_today_highlight_cache_memories_MemoryId` FOREIGN KEY (`MemoryId`) REFERENCES memories (`Id`) ON DELETE CASCADE
);

CREATE TABLE photos (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `UploadedByUserId` char(36) NOT NULL,
    `LocalAssetPath` text,
    `S3Key` text,
    `ThumbnailS3Key` text,
    `RemoteUrl` text,
    `ThumbnailUrl` text,
    `TakenAt` datetime(6),
    `Location` text,
    `Description` text,
    `EventId` text,
    `Generation` integer,
    `IsHidden` tinyint(1) NOT NULL,
    `IsDuplicate` tinyint(1) NOT NULL,
    `IsSimilar` tinyint(1) NOT NULL,
    `IsBlurry` tinyint(1) NOT NULL,
    `IsScreenshot` tinyint(1) NOT NULL,
    `IsAiInferred` tinyint(1) NOT NULL,
    `Visibility` integer NOT NULL,
    `PrivacyScope` integer NOT NULL,
    `SharedAlbumId` char(36),
    `FileSizeBytes` bigint,
    `ContentType` text,
    `AiAnalysisJson` text,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photos` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photos_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photos_shared_albums_SharedAlbumId` FOREIGN KEY (`SharedAlbumId`) REFERENCES shared_albums (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_photos_user_accounts_UploadedByUserId` FOREIGN KEY (`UploadedByUserId`) REFERENCES user_accounts (`Id`) ON DELETE RESTRICT
);

CREATE TABLE shared_album_access (
    `Id` char(36) NOT NULL,
    `SharedAlbumId` char(36) NOT NULL,
    `UserId` char(36),
    `FriendId` char(36),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_shared_album_access` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_shared_album_access_shared_albums_SharedAlbumId` FOREIGN KEY (`SharedAlbumId`) REFERENCES shared_albums (`Id`) ON DELETE CASCADE
);

CREATE TABLE frame_commands (
    `Id` char(36) NOT NULL,
    `DisplayDeviceId` char(36) NOT NULL,
    `IssuedByUserId` char(36),
    `CommandType` integer NOT NULL,
    `Status` integer NOT NULL,
    `PayloadJson` json NOT NULL,
    `DeliveredAt` datetime(6),
    `AcknowledgedAt` datetime(6),
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_frame_commands` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_frame_commands_display_devices_DisplayDeviceId` FOREIGN KEY (`DisplayDeviceId`) REFERENCES display_devices (`Id`) ON DELETE CASCADE
);

CREATE TABLE frame_playback_packages (
    `Id` char(36) NOT NULL,
    `DisplayDeviceId` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `Title` text NOT NULL,
    `PackageJson` json NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `SortOrder` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_frame_playback_packages` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_frame_playback_packages_display_devices_DisplayDeviceId` FOREIGN KEY (`DisplayDeviceId`) REFERENCES display_devices (`Id`) ON DELETE CASCADE
);

CREATE TABLE family_moment_photos (
    `Id` char(36) NOT NULL,
    `FamilyMomentId` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_moment_photos` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_moment_photos_family_moments_FamilyMomentId` FOREIGN KEY (`FamilyMomentId`) REFERENCES family_moments (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_family_moment_photos_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE memory_photos (
    `Id` char(36) NOT NULL,
    `MemoryId` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `SortOrder` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_memory_photos` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_memory_photos_memories_MemoryId` FOREIGN KEY (`MemoryId`) REFERENCES memories (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_memory_photos_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE memory_videos (
    `Id` char(36) NOT NULL,
    `MemoryId` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `SortOrder` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_memory_videos` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_memory_videos_memories_MemoryId` FOREIGN KEY (`MemoryId`) REFERENCES memories (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_memory_videos_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE photo_ai_inferences (
    `Id` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `SuggestedMemberId` char(36),
    `SuggestedMemberName` text,
    `Status` integer NOT NULL,
    `Confidence` double NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_ai_inferences` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_ai_inferences_family_members_SuggestedMemberId` FOREIGN KEY (`SuggestedMemberId`) REFERENCES family_members (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_photo_ai_inferences_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE photo_ai_tags (
    `Id` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `Tag` varchar(128) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_ai_tags` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_ai_tags_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE photo_members (
    `Id` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `FamilyMemberId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_members` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_members_family_members_FamilyMemberId` FOREIGN KEY (`FamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_members_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE TABLE frame_comments (
    `Id` char(36) NOT NULL,
    `PackageId` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Message` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_frame_comments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_frame_comments_frame_playback_packages_PackageId` FOREIGN KEY (`PackageId`) REFERENCES frame_playback_packages (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_frame_comments_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_ai_analysis_jobs_FamilyId` ON ai_analysis_jobs (`FamilyId`);

CREATE INDEX `IX_ai_analysis_jobs_UserId` ON ai_analysis_jobs (`UserId`);

CREATE INDEX `IX_display_devices_CurrentMemoryId` ON display_devices (`CurrentMemoryId`);

CREATE INDEX `IX_display_devices_FamilyId` ON display_devices (`FamilyId`);

CREATE UNIQUE INDEX `IX_display_devices_QrCode` ON display_devices (`QrCode`);

CREATE INDEX `IX_families_OwnerUserId` ON families (`OwnerUserId`);

CREATE INDEX `IX_family_members_FamilyId` ON family_members (`FamilyId`);

CREATE INDEX `IX_family_memberships_FamilyId` ON family_memberships (`FamilyId`);

CREATE UNIQUE INDEX `IX_family_memberships_FamilyId_UserId` ON family_memberships (`FamilyId`, `UserId`);

CREATE INDEX `IX_family_memberships_UserId` ON family_memberships (`UserId`);

CREATE INDEX `IX_family_moment_members_FamilyMemberId` ON family_moment_members (`FamilyMemberId`);

CREATE UNIQUE INDEX `IX_family_moment_members_FamilyMomentId_FamilyMemberId` ON family_moment_members (`FamilyMomentId`, `FamilyMemberId`);

CREATE UNIQUE INDEX `IX_family_moment_photos_FamilyMomentId_PhotoId` ON family_moment_photos (`FamilyMomentId`, `PhotoId`);

CREATE INDEX `IX_family_moment_photos_PhotoId` ON family_moment_photos (`PhotoId`);

CREATE INDEX `IX_family_moments_FamilyId` ON family_moments (`FamilyId`);

CREATE INDEX `IX_frame_commands_DisplayDeviceId_Status` ON frame_commands (`DisplayDeviceId`, `Status`);

CREATE INDEX `IX_frame_comments_PackageId` ON frame_comments (`PackageId`);

CREATE INDEX `IX_frame_comments_UserId` ON frame_comments (`UserId`);

CREATE INDEX `IX_frame_playback_packages_DisplayDeviceId` ON frame_playback_packages (`DisplayDeviceId`);

CREATE INDEX `IX_friends_OwnerUserId` ON friends (`OwnerUserId`);

CREATE INDEX `IX_memories_CreatedByUserId` ON memories (`CreatedByUserId`);

CREATE INDEX `IX_memories_FamilyId` ON memories (`FamilyId`);

CREATE INDEX `IX_memories_StartDate` ON memories (`StartDate`);

CREATE INDEX `IX_memory_members_FamilyMemberId` ON memory_members (`FamilyMemberId`);

CREATE UNIQUE INDEX `IX_memory_members_MemoryId_FamilyMemberId` ON memory_members (`MemoryId`, `FamilyMemberId`);

CREATE UNIQUE INDEX `IX_memory_photos_MemoryId_PhotoId` ON memory_photos (`MemoryId`, `PhotoId`);

CREATE INDEX `IX_memory_photos_PhotoId` ON memory_photos (`PhotoId`);

CREATE UNIQUE INDEX `IX_memory_videos_MemoryId_PhotoId` ON memory_videos (`MemoryId`, `PhotoId`);

CREATE INDEX `IX_memory_videos_PhotoId` ON memory_videos (`PhotoId`);

CREATE INDEX `IX_notifications_UserId` ON notifications (`UserId`);

CREATE UNIQUE INDEX `IX_password_reset_tokens_Token` ON password_reset_tokens (`Token`);

CREATE INDEX `IX_password_reset_tokens_UserId` ON password_reset_tokens (`UserId`);

CREATE INDEX `IX_photo_ai_inferences_PhotoId` ON photo_ai_inferences (`PhotoId`);

CREATE INDEX `IX_photo_ai_inferences_SuggestedMemberId` ON photo_ai_inferences (`SuggestedMemberId`);

CREATE INDEX `IX_photo_ai_tags_PhotoId` ON photo_ai_tags (`PhotoId`);

CREATE INDEX `IX_photo_ai_tags_Tag` ON photo_ai_tags (`Tag`);

CREATE INDEX `IX_photo_members_FamilyMemberId` ON photo_members (`FamilyMemberId`);

CREATE UNIQUE INDEX `IX_photo_members_PhotoId_FamilyMemberId` ON photo_members (`PhotoId`, `FamilyMemberId`);

CREATE INDEX `IX_photos_FamilyId` ON photos (`FamilyId`);

CREATE INDEX `IX_photos_SharedAlbumId` ON photos (`SharedAlbumId`);

CREATE INDEX `IX_photos_TakenAt` ON photos (`TakenAt`);

CREATE INDEX `IX_photos_UploadedByUserId` ON photos (`UploadedByUserId`);

CREATE UNIQUE INDEX `IX_refresh_tokens_Token` ON refresh_tokens (`Token`);

CREATE INDEX `IX_refresh_tokens_UserId` ON refresh_tokens (`UserId`);

CREATE INDEX `IX_shared_album_access_SharedAlbumId` ON shared_album_access (`SharedAlbumId`);

CREATE UNIQUE INDEX `IX_shared_albums_FamilyId_ExternalId` ON shared_albums (`FamilyId`, `ExternalId`);

CREATE UNIQUE INDEX `IX_today_highlight_cache_FamilyId_CacheDate` ON today_highlight_cache (`FamilyId`, `CacheDate`);

CREATE INDEX `IX_today_highlight_cache_MemoryId` ON today_highlight_cache (`MemoryId`);

CREATE INDEX `IX_upload_sessions_FamilyId` ON upload_sessions (`FamilyId`);

CREATE INDEX `IX_upload_sessions_UserId` ON upload_sessions (`UserId`);

CREATE UNIQUE INDEX `IX_user_accounts_Email` ON user_accounts (`Email`);

CREATE UNIQUE INDEX `IX_user_ai_settings_UserId_Key` ON user_ai_settings (`UserId`, `Key`);

CREATE UNIQUE INDEX `IX_user_oauth_links_Provider_ProviderUserId` ON user_oauth_links (`Provider`, `ProviderUserId`);

CREATE INDEX `IX_user_oauth_links_UserId` ON user_oauth_links (`UserId`);
