-- Per-user reusable photo tag labels (tags sheet). Does not remove tags already on photos.

CREATE TABLE user_photo_tag_library (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Tag" varchar(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_user_photo_tag_library" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_user_photo_tag_library_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_user_photo_tag_library_UserId" ON user_photo_tag_library ("UserId");
CREATE UNIQUE INDEX "IX_user_photo_tag_library_UserId_Tag" ON user_photo_tag_library ("UserId", "Tag");
