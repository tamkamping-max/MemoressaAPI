-- Links the logged-in user to a family_members row ("this is me" for face sync).
-- MySQL local schema uses PascalCase column names (same as EF Core default).

ALTER TABLE user_accounts ADD `SelfFamilyMemberId` char(36) NULL;

ALTER TABLE user_accounts ADD CONSTRAINT `FK_user_accounts_family_members_SelfFamilyMemberId`
    FOREIGN KEY (`SelfFamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE SET NULL;

CREATE INDEX `IX_user_accounts_SelfFamilyMemberId` ON user_accounts (`SelfFamilyMemberId`);
