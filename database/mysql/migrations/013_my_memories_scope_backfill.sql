-- Backfill flags on server-curated memories so GET /memories (我的回憶) excludes legacy rows.
-- MemoryType.AiMemory = 4

UPDATE memories
SET `IsAiGenerated` = 1
WHERE `Type` = 4 AND `IsAiGenerated` = 0;

UPDATE memories
SET `IsAiGenerated` = 1
WHERE `Title` = 'AI Memory'
  AND `Description` = 'Automatically curated memory'
  AND `IsAiGenerated` = 0;

UPDATE memories
SET `IsAiGenerated` = 1, `IsTodayHighlight` = 1
WHERE `Title` = 'Today''s Memory'
  AND `Description` = 'AI curated highlight for today'
  AND (`IsAiGenerated` = 0 OR `IsTodayHighlight` = 0);
