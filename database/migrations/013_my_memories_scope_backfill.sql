-- Backfill flags on server-curated memories so GET /memories (我的回憶) excludes legacy rows.
-- MemoryType.AiMemory = 4

UPDATE memories
SET "IsAiGenerated" = true
WHERE "Type" = 4 AND "IsAiGenerated" = false;

UPDATE memories
SET "IsAiGenerated" = true
WHERE "Title" = 'AI Memory'
  AND "Description" = 'Automatically curated memory'
  AND "IsAiGenerated" = false;

UPDATE memories
SET "IsAiGenerated" = true, "IsTodayHighlight" = true
WHERE "Title" = 'Today''s Memory'
  AND "Description" = 'AI curated highlight for today'
  AND ("IsAiGenerated" = false OR "IsTodayHighlight" = false);
