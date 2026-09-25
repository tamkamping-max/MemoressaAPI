-- Memoressa schema migration: 009_activity_albums.sql

CREATE TABLE activity_albums (
    "Id" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "ExternalId" character varying(128) NOT NULL,
    "Title" character varying(500) NOT NULL,
    "Type" integer NOT NULL,
    "Status" integer NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date,
    "Location" character varying(500),
    "CreatorUserId" uuid NOT NULL,
    "CoverPhotoId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_activity_albums" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_activity_albums_families_FamilyId" FOREIGN KEY ("FamilyId") REFERENCES families ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_activity_albums_user_accounts_CreatorUserId" FOREIGN KEY ("CreatorUserId") REFERENCES user_accounts ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_activity_albums_photos_CoverPhotoId" FOREIGN KEY ("CoverPhotoId") REFERENCES photos ("Id") ON DELETE SET NULL
);

CREATE UNIQUE INDEX "IX_activity_albums_FamilyId_ExternalId" ON activity_albums ("FamilyId", "ExternalId");
CREATE INDEX "IX_activity_albums_FamilyId_Status" ON activity_albums ("FamilyId", "Status");
CREATE INDEX "IX_activity_albums_CreatorUserId" ON activity_albums ("CreatorUserId");

CREATE TABLE activity_agenda_items (
    "Id" uuid NOT NULL,
    "ActivityAlbumId" uuid NOT NULL,
    "ExternalId" character varying(128),
    "Title" character varying(500) NOT NULL,
    "StartDate" date NOT NULL,
    "EndDate" date,
    "Location" character varying(500),
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_activity_agenda_items" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_activity_agenda_items_activity_albums_ActivityAlbumId" FOREIGN KEY ("ActivityAlbumId") REFERENCES activity_albums ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_activity_agenda_items_ActivityAlbumId" ON activity_agenda_items ("ActivityAlbumId");

CREATE TABLE activity_album_family_members (
    "Id" uuid NOT NULL,
    "ActivityAlbumId" uuid NOT NULL,
    "FamilyMemberId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_activity_album_family_members" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_activity_album_family_members_activity_albums_ActivityAlbumId" FOREIGN KEY ("ActivityAlbumId") REFERENCES activity_albums ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_activity_album_family_members_family_members_FamilyMemberId" FOREIGN KEY ("FamilyMemberId") REFERENCES family_members ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_activity_album_family_members_ActivityAlbumId_FamilyMemberId" ON activity_album_family_members ("ActivityAlbumId", "FamilyMemberId");

CREATE TABLE activity_album_friends (
    "Id" uuid NOT NULL,
    "ActivityAlbumId" uuid NOT NULL,
    "FriendId" uuid,
    "FriendReference" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_activity_album_friends" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_activity_album_friends_activity_albums_ActivityAlbumId" FOREIGN KEY ("ActivityAlbumId") REFERENCES activity_albums ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_activity_album_friends_friends_FriendId" FOREIGN KEY ("FriendId") REFERENCES friends ("Id") ON DELETE SET NULL
);

CREATE UNIQUE INDEX "IX_activity_album_friends_ActivityAlbumId_FriendReference" ON activity_album_friends ("ActivityAlbumId", "FriendReference");

CREATE TABLE activity_album_photos (
    "Id" uuid NOT NULL,
    "ActivityAlbumId" uuid NOT NULL,
    "PhotoId" uuid NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_activity_album_photos" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_activity_album_photos_activity_albums_ActivityAlbumId" FOREIGN KEY ("ActivityAlbumId") REFERENCES activity_albums ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_activity_album_photos_photos_PhotoId" FOREIGN KEY ("PhotoId") REFERENCES photos ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_activity_album_photos_ActivityAlbumId_PhotoId" ON activity_album_photos ("ActivityAlbumId", "PhotoId");
CREATE INDEX "IX_activity_album_photos_PhotoId" ON activity_album_photos ("PhotoId");

ALTER TABLE upload_sessions ADD "ActivityAlbumId" uuid;
ALTER TABLE upload_sessions ADD CONSTRAINT "FK_upload_sessions_activity_albums_ActivityAlbumId" FOREIGN KEY ("ActivityAlbumId") REFERENCES activity_albums ("Id") ON DELETE SET NULL;
