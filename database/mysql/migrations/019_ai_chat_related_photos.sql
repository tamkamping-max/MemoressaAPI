-- Memoressa MySQL schema migration: 019_ai_chat_related_photos.sql
-- Persist multiple photo hits on AI agent assistant messages.

ALTER TABLE ai_chat_messages
    ADD COLUMN `RelatedPhotoIdsJson` text NULL;
