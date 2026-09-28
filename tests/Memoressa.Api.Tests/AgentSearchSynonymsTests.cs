using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentSearchSynonymsTests
{
    [Fact]
    public void BuildSearchTerms_開心_IncludesHappy()
    {
        var terms = AgentSearchSynonyms.BuildSearchTerms("開心");
        Assert.Contains(terms, t => t.Equals("happy", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(terms, t => t.Equals("開心", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSearchTerms_PhraseWith開心_IncludesHappy()
    {
        var terms = AgentSearchSynonyms.BuildSearchTerms("找開心的照片");
        Assert.Contains(terms, t => t.Equals("happy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildSearchTerms_Happy_Includes開心()
    {
        var terms = AgentSearchSynonyms.BuildSearchTerms("happy");
        Assert.Contains(terms, t => t.Equals("開心", StringComparison.OrdinalIgnoreCase));
    }
}
