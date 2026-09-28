-- Photo custom audience: friends (PUT friendIds full replace; Guid ids from uploader friends list).

CREATE TABLE photo_friends (
    `Id` char(36) NOT NULL,
    `PhotoId` char(36) NOT NULL,
    `FriendId` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_photo_friends` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_photo_friends_friends_FriendId` FOREIGN KEY (`FriendId`) REFERENCES friends (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_photo_friends_photos_PhotoId` FOREIGN KEY (`PhotoId`) REFERENCES photos (`Id`) ON DELETE CASCADE
);

CREATE UNIQUE INDEX `IX_photo_friends_PhotoId_FriendId` ON photo_friends (`PhotoId`, `FriendId`);
CREATE INDEX `IX_photo_friends_FriendId` ON photo_friends (`FriendId`);
