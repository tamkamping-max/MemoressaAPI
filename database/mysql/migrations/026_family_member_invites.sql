ALTER TABLE family_members
    ADD COLUMN assigned_to_tree TINYINT(1) NOT NULL DEFAULT 1;

CREATE TABLE family_member_invites (
    `Id` char(36) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `InviterUserId` char(36) NOT NULL,
    `InviteeUserId` char(36) NULL,
    `InviteeEmail` varchar(320) NOT NULL,
    `Status` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_family_member_invites` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_family_member_invites_families_FamilyId` FOREIGN KEY (`FamilyId`) REFERENCES families (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_family_member_invites_user_accounts_InviterUserId` FOREIGN KEY (`InviterUserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_family_member_invites_user_accounts_InviteeUserId` FOREIGN KEY (`InviteeUserId`) REFERENCES user_accounts (`Id`) ON DELETE SET NULL
);

CREATE INDEX `IX_family_member_invites_FamilyId` ON family_member_invites (`FamilyId`);
CREATE INDEX `IX_family_member_invites_InviterUserId` ON family_member_invites (`InviterUserId`);
CREATE INDEX `IX_family_member_invites_InviteeUserId` ON family_member_invites (`InviteeUserId`);
CREATE INDEX `IX_family_member_invites_InviteeEmail` ON family_member_invites (`InviteeEmail`);
