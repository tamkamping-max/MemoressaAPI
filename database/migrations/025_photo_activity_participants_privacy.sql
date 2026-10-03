ALTER TABLE photos
    ADD COLUMN IF NOT EXISTS "ActivityParticipantsVisible" boolean NOT NULL DEFAULT false;

ALTER TABLE upload_sessions
    ADD COLUMN IF NOT EXISTS "ActivityParticipantsVisible" boolean NOT NULL DEFAULT false;

ALTER TABLE upload_sessions
    ADD COLUMN IF NOT EXISTS "PrivacyMemberIdsJson" text NOT NULL DEFAULT '[]';

ALTER TABLE upload_sessions
    ADD COLUMN IF NOT EXISTS "PrivacyFriendIdsJson" text NOT NULL DEFAULT '[]';

UPDATE photos p
SET "ActivityParticipantsVisible" = true
WHERE EXISTS (
    SELECT 1 FROM activity_album_photos aap WHERE aap."PhotoId" = p."Id");
