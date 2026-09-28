using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

/// <summary>
/// User-created memory albums (<c>POST /api/v1/memories</c>). Not used for MemoressaApp 回憶 tab (see POST /photos/today-memories).
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
