-- Run ONLY if you applied an early 020 that used snake_case (self_family_member_id).
-- Fresh installs should use 020 only.

SET @has_snake := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'user_accounts'
      AND COLUMN_NAME = 'self_family_member_id');

SET @drop_fk := IF(
    @has_snake > 0,
    'ALTER TABLE user_accounts DROP FOREIGN KEY fk_user_accounts_self_family_member',
    'SELECT 1');
PREPARE stmt FROM @drop_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @drop_idx := IF(
    @has_snake > 0,
    'DROP INDEX ix_user_accounts_self_family_member_id ON user_accounts',
    'SELECT 1');
PREPARE stmt FROM @drop_idx;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @drop_col := IF(
    @has_snake > 0,
    'ALTER TABLE user_accounts DROP COLUMN self_family_member_id',
    'SELECT 1');
PREPARE stmt FROM @drop_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_pascal := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'user_accounts'
      AND COLUMN_NAME = 'SelfFamilyMemberId');

SET @add_col := IF(
    @has_pascal = 0,
    'ALTER TABLE user_accounts ADD `SelfFamilyMemberId` char(36) NULL',
    'SELECT 1');
PREPARE stmt FROM @add_col;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @add_fk := IF(
    @has_pascal = 0,
    'ALTER TABLE user_accounts ADD CONSTRAINT `FK_user_accounts_family_members_SelfFamilyMemberId` FOREIGN KEY (`SelfFamilyMemberId`) REFERENCES family_members (`Id`) ON DELETE SET NULL',
    'SELECT 1');
PREPARE stmt FROM @add_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @add_idx := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'user_accounts' AND INDEX_NAME = 'IX_user_accounts_SelfFamilyMemberId') = 0,
    'CREATE INDEX `IX_user_accounts_SelfFamilyMemberId` ON user_accounts (`SelfFamilyMemberId`)',
    'SELECT 1');
PREPARE stmt FROM @add_idx;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
