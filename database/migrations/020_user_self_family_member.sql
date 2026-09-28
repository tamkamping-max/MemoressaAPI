-- Links the logged-in user to a family_members row ("this is me" for face sync).

ALTER TABLE user_accounts
    ADD COLUMN IF NOT EXISTS "SelfFamilyMemberId" uuid NULL;

ALTER TABLE user_accounts
    DROP CONSTRAINT IF EXISTS "FK_user_accounts_family_members_SelfFamilyMemberId";

ALTER TABLE user_accounts
    ADD CONSTRAINT "FK_user_accounts_family_members_SelfFamilyMemberId"
        FOREIGN KEY ("SelfFamilyMemberId") REFERENCES family_members("Id") ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS "IX_user_accounts_SelfFamilyMemberId"
    ON user_accounts("SelfFamilyMemberId");
