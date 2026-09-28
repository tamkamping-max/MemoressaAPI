using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentSearchQueryTests
{
    [Theory]
    [InlineData("找妈妈2018夏天的照片", 2018)]
    [InlineData("2020 2021 travel", 2020, 2021)]
    [InlineData("no year here")]
    public void ExtractYears_FindsFourDigitYears(string query, params int[] expected)
    {
        var years = AgentSearchQuery.ExtractYears(query);
        Assert.Equal(expected.OrderBy(y => y), years.OrderBy(y => y));
    }
}
