using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentSearchQueryTests
{
    [Theory]
    [InlineData("找妈妈2018夏天的照片", 2018)]
    [InlineData("2018", 2018)]
    [InlineData("photos from 1999 and 2001", 1999, 2001)]
    public void ExtractYears_FindsYearsInQuery(string query, params int[] expected)
    {
        var years = AgentSearchQuery.ExtractYears(query);
        Assert.Equal(expected, years);
    }

    [Theory]
    [InlineData("happy", true)]
    [InlineData("開心", true)]
    [InlineData("找妈妈2018夏天的照片", false)]
    [InlineData("what photos do we have?", false)]
    [InlineData("帮我找开心的照片", false)]
    [InlineData("今天9月份照片", false)]
    [InlineData("9月旅行", false)]
    public void IsSimpleSearchPhrase_ClassifiesTagSearch(string query, bool expected)
    {
        Assert.Equal(expected, AgentSearchQuery.IsSimpleSearchPhrase(query));
    }
}
