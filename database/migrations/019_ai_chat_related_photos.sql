-- Memoressa schema migration: 019_ai_chat_related_photos.sql
-- Persist multiple photo hits on AI agent assistant messages.

ALTER TABLE ai_chat_messages
    ADD COLUMN IF NOT EXISTS "RelatedPhotoIdsJson" text NULL;
