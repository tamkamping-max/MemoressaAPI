using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class AgentSearchQueryTests
{
    private static readonly DateTime Ref = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

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

    [Theory]
    [InlineData("今天9月份照片", 2026, 9, null)]
    [InlineData("今天年月份照片", 2026, 9, null)]
    [InlineData("今年9月的照片", 2026, 9, null)]
    [InlineData("2018年9月", 2018, 9, null)]
    [InlineData("今天", 2026, 9, 28)]
    public void ExtractCalendarFilter_ParsesMonthAndYear(
        string query,
        int? year,
        int? month,
        int? day)
    {
        var filter = AgentSearchQuery.ExtractCalendarFilter(query, Ref);
        Assert.Equal(year, filter.Year);
        Assert.Equal(month, filter.Month);
        Assert.Equal(day, filter.Day);
    }
}
