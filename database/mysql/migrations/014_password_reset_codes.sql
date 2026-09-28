-- OTP password reset codes (hashed, 15-minute TTL)

CREATE TABLE password_reset_codes (
    `Id` char(36) NOT NULL,
    `UserId` char(36) NOT NULL,
    `CodeHash` varchar(256) NOT NULL,
    `ExpiresAt` datetime(6) NOT NULL,
    `UsedAt` datetime(6) NULL,
    `FailedVerifyAttempts` int NOT NULL DEFAULT 0,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NOT NULL,
    CONSTRAINT `PK_password_reset_codes` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_password_reset_codes_user_accounts_UserId` FOREIGN KEY (`UserId`) REFERENCES user_accounts (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_password_reset_codes_UserId_CreatedAt` ON password_reset_codes (`UserId`, `CreatedAt`);
