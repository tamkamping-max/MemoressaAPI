-- Photo custom audience: friends (PUT friendIds full replace; Guid ids from uploader friends list).

CREATE TABLE photo_friends (
    "Id" uuid NOT NULL,
    "PhotoId" uuid NOT NULL,
    "FriendId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_photo_friends" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_photo_friends_friends_FriendId" FOREIGN KEY ("FriendId") REFERENCES friends ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_photo_friends_photos_PhotoId" FOREIGN KEY ("PhotoId") REFERENCES photos ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_photo_friends_PhotoId_FriendId" ON photo_friends ("PhotoId", "FriendId");
CREATE INDEX "IX_photo_friends_FriendId" ON photo_friends ("FriendId");
