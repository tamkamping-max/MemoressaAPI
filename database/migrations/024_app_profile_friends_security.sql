-- Profile city on family members, friend invites, account security codes, email verified timestamp.

ALTER TABLE family_members ADD "CityId" text;

ALTER TABLE user_accounts ADD "EmailVerifiedAt" timestamp with time zone;

CREATE TABLE friend_invites (
    "Id" uuid NOT NULL,
    "InviterUserId" uuid NOT NULL,
    "InviteeUserId" uuid,
    "InviteeEmail" character varying(320) NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_friend_invites" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_friend_invites_user_accounts_InviterUserId" FOREIGN KEY ("InviterUserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_friend_invites_user_accounts_InviteeUserId" FOREIGN KEY ("InviteeUserId") REFERENCES user_accounts ("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_friend_invites_InviterUserId" ON friend_invites ("InviterUserId");
CREATE INDEX "IX_friend_invites_InviteeUserId" ON friend_invites ("InviteeUserId");
CREATE INDEX "IX_friend_invites_InviteeEmail" ON friend_invites ("InviteeEmail");

CREATE TABLE email_verification_codes (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CodeHash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    "FailedVerifyAttempts" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_email_verification_codes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_email_verification_codes_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_email_verification_codes_UserId" ON email_verification_codes ("UserId");

CREATE TABLE email_change_codes (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "NewEmail" character varying(320) NOT NULL,
    "CodeHash" text NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    "FailedVerifyAttempts" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_email_change_codes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_email_change_codes_user_accounts_UserId" FOREIGN KEY ("UserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_email_change_codes_UserId" ON email_change_codes ("UserId");
