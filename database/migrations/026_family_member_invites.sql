ALTER TABLE family_members
    ADD COLUMN IF NOT EXISTS "AssignedToTree" boolean NOT NULL DEFAULT true;

CREATE TABLE family_member_invites (
    "Id" uuid NOT NULL,
    "FamilyId" uuid NOT NULL,
    "InviterUserId" uuid NOT NULL,
    "InviteeUserId" uuid NULL,
    "InviteeEmail" character varying(320) NOT NULL,
    "Status" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_family_member_invites" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_family_member_invites_families_FamilyId" FOREIGN KEY ("FamilyId") REFERENCES families ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_family_member_invites_user_accounts_InviterUserId" FOREIGN KEY ("InviterUserId") REFERENCES user_accounts ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_family_member_invites_user_accounts_InviteeUserId" FOREIGN KEY ("InviteeUserId") REFERENCES user_accounts ("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_family_member_invites_FamilyId" ON family_member_invites ("FamilyId");
CREATE INDEX "IX_family_member_invites_InviterUserId" ON family_member_invites ("InviterUserId");
CREATE INDEX "IX_family_member_invites_InviteeUserId" ON family_member_invites ("InviteeUserId");
CREATE INDEX "IX_family_member_invites_InviteeEmail" ON family_member_invites ("InviteeEmail");
