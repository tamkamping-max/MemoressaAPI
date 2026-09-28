-- Optional data backfill after 017_photo_albums.sql
--
-- Full grouping (same memory / activity / day) is product-specific and may be run
-- from an admin job. This script only documents the promotion rule:
--
-- When a photo group has userTags/description on a single photo, copy them to the
-- new photo_album row and prefer album-level fields in the App.
--
-- Example: create one-album-per-memory (MySQL 8+) — review on staging before production.
--
-- INSERT INTO photo_albums (`Id`, `FamilyId`, `CreatedByUserId`, `Description`, `Visibility`, `CoverPhotoId`, `PhotoSetFingerprint`, `CreatedAt`, `UpdatedAt`)
-- SELECT UUID(), p.FamilyId, MIN(p.UploadedByUserId), NULL, MIN(p.Visibility), MIN(p.Id), '...', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
-- FROM memory_photos mp
-- JOIN photos p ON p.Id = mp.PhotoId
-- GROUP BY mp.MemoryId, p.FamilyId;
--
-- Then insert photo_album_photos / merge photo_user_tags into photo_album_user_tags per group.
-- Use POST /api/v1/photo-albums find-or-create for new App flows instead of re-backfilling.

SELECT 1;
