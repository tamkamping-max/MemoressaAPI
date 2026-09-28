-- Memoressa MySQL schema migration: 019_photo_ai_vision_cost.sql
ALTER TABLE photos ADD `AiVisionPromptTokens` int NULL;
ALTER TABLE photos ADD `AiVisionCompletionTokens` int NULL;
ALTER TABLE photos ADD `AiVisionCostUsd` decimal(18, 6) NULL;
ALTER TABLE photos ADD `AiVisionModel` varchar(64) NULL;
ALTER TABLE photos ADD `AiVisionCostAt` datetime(6) NULL;
