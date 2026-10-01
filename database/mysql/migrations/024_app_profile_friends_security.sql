-- Profile city on family members, friend invites, account security codes, email verified timestamp.

ALTER TABLE family_members ADD `CityId` text NULL;

ALTER TABLE user_accounts ADD `EmailVerifiedAt` datetime(6) NULL;

CREATE TABLE friend_invites (
    `Id` char(36) NOT NULL,
    `InviterUserId` char(36) NOT NULL,
    `InviteeUserId` char(36) NULL,
    `InviteeEmail` varchar(320) NOT NULL,
    `Status` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_friend_invites` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_friend_invites_user_accounts_InviterUserId` FOREIGN KEY (`InviterUserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_friend_invites_user_accounts_InviteeUserId` FOREIGN KEY (`InviteeUserId`) REFERENCES user_accounts (`Id`) ON DELETE SET NULL
);

CREATE INDEX `IX_friend_invites_InviterUserId` ON friend_invites (`InviterUserId`);
CREATE INDEX `IX_friend_invites_InviteeUserId` ON friend_invites (`InviteeUserId`);
CREATE INDEX `IX_friend_invites_InviteeEmail` ON friend_invites (`InviteeEmail`);

CREATE TABLE email_verification_codes (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `CodeHash` text NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6) NULL,
    `FailedVerifyAttempts` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_email_verification_codes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_email_verification_codes_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_email_verification_codes_UserId` ON email_verification_codes (`UserId`);

CREATE TABLE email_change_codes (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `NewEmail` varchar(320) NOT NULL,
    `CodeHash` text NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6) NULL,
    `FailedVerifyAttempts` integer NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_email_change_codes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_email_change_codes_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_email_change_codes_UserId` ON email_change_codes (`UserId`);
