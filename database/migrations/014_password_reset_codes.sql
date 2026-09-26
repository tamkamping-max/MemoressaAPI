-- OTP password reset codes (hashed, 15-minute TTL)

CREATE TABLE password_reset_codes (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CodeHash" character varying(256) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    "FailedVerifyAttempts" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_password_reset_codes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_password_reset_codes_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_password_reset_codes_UserId_CreatedAt" ON password_reset_codes ("UserId", "CreatedAt");
