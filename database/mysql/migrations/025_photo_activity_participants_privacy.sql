ALTER TABLE photos
    ADD COLUMN activity_participants_visible TINYINT(1) NOT NULL DEFAULT 0;

ALTER TABLE upload_sessions
    ADD COLUMN activity_participants_visible TINYINT(1) NOT NULL DEFAULT 0;

ALTER TABLE upload_sessions
    ADD COLUMN privacy_member_ids_json JSON NOT NULL DEFAULT (JSON_ARRAY());

ALTER TABLE upload_sessions
    ADD COLUMN privacy_friend_ids_json JSON NOT NULL DEFAULT (JSON_ARRAY());

UPDATE photos p
INNER JOIN activity_album_photos aap ON aap.`PhotoId` = p.`Id`
SET p.activity_participants_visible = 1;
