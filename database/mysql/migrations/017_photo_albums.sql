-- Photo albums (experience groups): tags, description, comments at album level.

CREATE TABLE photo_albums (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CreatedByUserId` char(36) NOT NULL,
    `Description` text NULL,
    `Visibility` int NOT NULL,
    `CoverPhotoId` char(36) NULL,
    `PhotoSetFingerprint` varchar(64) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_albums` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_albums_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_albums_user_accounts_CreatedByUserId` FOREIGN KEY (`CreatedByUserId`) REFERENCES user_accounts (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_photo_albums_FamilyId` ON photo_albums (`FamilyId`);
CREATE UNIQUE INDEX `IX_photo_albums_FamilyId_PhotoSetFingerprint` ON photo_albums (`FamilyId`, `PhotoSetFingerprint`);

CREATE TABLE photo_album_photos (
    `Id` char(36) NOT NULL,
    `PhotoAlbumId` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `SortOrder` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_album_photos` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_album_photos_photo_albums_PhotoAlbumId` FOREIGN KEY (`PhotoAlbumId`) REFERENCES photo_albums (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_album_photos_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE UNIQUE INDEX `IX_photo_album_photos_PhotoAlbumId_PhotoId` ON photo_album_photos (`PhotoAlbumId`, `PhotoId`);
CREATE INDEX `IX_photo_album_photos_PhotoId` ON photo_album_photos (`PhotoId`);

CREATE TABLE photo_album_user_tags (
    `Id` char(36) NOT NULL,
    `PhotoAlbumId` char(36) NOT NULL,
    `Tag` varchar(128) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_album_user_tags` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_album_user_tags_photo_albums_PhotoAlbumId` FOREIGN KEY (`PhotoAlbumId`) REFERENCES photo_albums (`Id`) ON DELETE CASCADE
);

CREATE UNIQUE INDEX `IX_photo_album_user_tags_PhotoAlbumId_Tag` ON photo_album_user_tags (`PhotoAlbumId`, `Tag`);

CREATE TABLE photo_album_members (
    `Id` char(36) NOT NULL,
    `PhotoAlbumId` char(36) NOT NULL,
    `FamilyMemberId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_album_members` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_album_members_photo_albums_PhotoAlbumId` FOREIGN KEY (`PhotoAlbumId`) REFERENCES photo_albums (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_album_members_family_members_FamilyMemberId` FOREIGN KEY (`FamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE CASCADE
);

CREATE UNIQUE INDEX `IX_photo_album_members_PhotoAlbumId_FamilyMemberId` ON photo_album_members (`PhotoAlbumId`, `FamilyMemberId`);

CREATE TABLE photo_album_comments (
    `Id` char(36) NOT NULL,
    `PhotoAlbumId` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `Message` text NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_album_comments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_album_comments_photo_albums_PhotoAlbumId` FOREIGN KEY (`PhotoAlbumId`) REFERENCES photo_albums (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_album_comments_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_photo_album_comments_PhotoAlbumId` ON photo_album_comments (`PhotoAlbumId`);
CREATE INDEX `IX_photo_album_comments_UserId` ON photo_album_comments (`UserId`);
CREATE INDEX `IX_photo_album_comments_CreatedAt` ON photo_album_comments (`CreatedAt`);
