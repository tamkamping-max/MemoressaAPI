-- Links the logged-in user to a family_members row ("this is me" for face sync).
ALTER TABLE user_accounts
    ADD COLUMN IF NOT EXISTS self_family_member_id UUID NULL;

ALTER TABLE user_accounts
    DROP CONSTRAINT IF EXISTS fk_user_accounts_self_family_member;

ALTER TABLE user_accounts
    ADD CONSTRAINT fk_user_accounts_self_family_member
        FOREIGN KEY (self_family_member_id) REFERENCES family_members(id) ON DELETE SET NULL;

CREATE INDEX IF NOT EXISTS ix_user_accounts_self_family_member_id
    ON user_accounts(self_family_member_id);
