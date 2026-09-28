-- Custom photo audience: friends (App PUT friendIds; full replace per photo).

CREATE TABLE photo_friends (
    "Id" uuid NOT NULL,
    "PhotoId" uuid NOT NULL,
    "FriendId" uuid NULL,
    "FriendReference" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_photo_friends" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_photo_friends_friends_FriendId" FOREIGN KEY ("FriendId") REFERENCES friends ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_photo_friends_photos_PhotoId" FOREIGN KEY ("PhotoId") REFERENCES photos ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_photo_friends_PhotoId_FriendReference" ON photo_friends ("PhotoId", "FriendReference");
CREATE INDEX "IX_photo_friends_FriendId" ON photo_friends ("FriendId");
