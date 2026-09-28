-- Memoressa schema migration: 019_photo_ai_vision_cost.sql
ALTER TABLE photos ADD COLUMN IF NOT EXISTS "AiVisionPromptTokens" integer;
ALTER TABLE photos ADD COLUMN IF NOT EXISTS "AiVisionCompletionTokens" integer;
ALTER TABLE photos ADD COLUMN IF NOT EXISTS "AiVisionCostUsd" numeric(18, 6);
ALTER TABLE photos ADD COLUMN IF NOT EXISTS "AiVisionModel" character varying(64);
ALTER TABLE photos ADD COLUMN IF NOT EXISTS "AiVisionCostAt" timestamp with time zone;
