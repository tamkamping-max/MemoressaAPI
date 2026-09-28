-- Links the logged-in user to a family_members row ("this is me" for face sync).
ALTER TABLE user_accounts
    ADD COLUMN self_family_member_id CHAR(36) NULL;

ALTER TABLE user_accounts
    ADD CONSTRAINT fk_user_accounts_self_family_member
        FOREIGN KEY (self_family_member_id) REFERENCES family_members(id) ON DELETE SET NULL;

CREATE INDEX ix_user_accounts_self_family_member_id ON user_accounts(self_family_member_id);
