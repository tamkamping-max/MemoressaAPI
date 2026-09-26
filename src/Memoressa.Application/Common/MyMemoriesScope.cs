using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

/// <summary>
/// 我的回憶 — user-created via <c>POST /api/v1/memories</c> only. Excludes server-curated AI / today-highlight rows.
/// </summary>
public static class MyMemoriesScope
{
    public static IQueryable<Memory> Apply(IQueryable<Memory> query) =>
        query.Where(m =>
            !m.IsTodayHighlight
            && !m.IsAiGenerated
            && m.Type != MemoryType.AiMemory);

    public static bool Includes(Memory memory) =>
        !memory.IsTodayHighlight
        && !memory.IsAiGenerated
        && memory.Type != MemoryType.AiMemory;
}
