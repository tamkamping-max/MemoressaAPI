using Memoressa.Application.Common;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class MyMemoriesScopeTests
{
    [Fact]
    public void Includes_ExcludesAiAndHighlightRows()
    {
        Assert.True(MyMemoriesScope.Includes(new Memory
        {
            Type = MemoryType.Photo,
            IsAiGenerated = false,
            IsTodayHighlight = false
        }));

        Assert.False(MyMemoriesScope.Includes(new Memory
        {
            Type = MemoryType.Photo,
            IsAiGenerated = true,
            IsTodayHighlight = false
        }));

        Assert.False(MyMemoriesScope.Includes(new Memory
        {
            Type = MemoryType.AiMemory,
            IsAiGenerated = false,
            IsTodayHighlight = false
        }));
    }
}
