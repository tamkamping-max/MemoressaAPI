-- Photo user tags (App tag chips; full replace on PUT) and photo comments.

CREATE TABLE photo_user_tags (
    "Id" uuid NOT NULL,
    "PhotoId" uuid NOT NULL,
    "Tag" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_photo_user_tags" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_photo_user_tags_photos_PhotoId" FOREIGN KEY ("PhotoId") REFERENCES photos ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_photo_user_tags_PhotoId_Tag" ON photo_user_tags ("PhotoId", "Tag");
CREATE INDEX "IX_photo_user_tags_Tag" ON photo_user_tags ("Tag");

CREATE TABLE photo_comments (
    "Id" uuid NOT NULL,
    "PhotoId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Message" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_photo_comments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_photo_comments_photos_PhotoId" FOREIGN KEY ("PhotoId") REFERENCES photos ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_photo_comments_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_photo_comments_PhotoId" ON photo_comments ("PhotoId");
CREATE INDEX "IX_photo_comments_UserId" ON photo_comments ("UserId");
CREATE INDEX "IX_photo_comments_CreatedAt" ON photo_comments ("CreatedAt");
