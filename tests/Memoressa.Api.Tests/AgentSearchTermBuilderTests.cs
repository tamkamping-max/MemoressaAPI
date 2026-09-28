using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentSearchTermBuilderTests
{
    [Fact]
    public void Build_MergesSynonymsAndGrokKeywords()
    {
        var terms = AgentSearchTermBuilder.Build("開心", ["happy", "feliz", "開心"]);
        Assert.Contains(terms, t => t.Equals("happy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(terms, t => t.Equals("feliz", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Limit_KeepsUserQueryFirst()
    {
        var terms = AgentSearchTermBuilder.Build("開心");
        var limited = AgentSearchTermBuilder.Limit(terms, 2);
        Assert.Equal(2, limited.Count);
        Assert.Equal("開心", limited[0]);
    }
}
