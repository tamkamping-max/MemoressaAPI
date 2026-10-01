using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class TodayMemoriesPresentationTests
{
    [Theory]
    [InlineData(TodayMemoriesStrategy.YearsAgoToday, "onThisDay", "往年今日")]
    [InlineData(TodayMemoriesStrategy.RandomFallback, "curatedFlashback", "精選回顧")]
    [InlineData(TodayMemoriesStrategy.Empty, "empty", "暫無回憶")]
    public void StrategySlugAndLabel(TodayMemoriesStrategy strategy, string slug, string label)
    {
        Assert.Equal(slug, TodayMemoriesPresentation.StrategySlug(strategy));
        Assert.Equal(label, TodayMemoriesPresentation.StrategyLabel(strategy));
    }

    [Theory]
    [InlineData("yearsAgoToday", 3, "onThisDay", "3 年前的今天")]
    [InlineData("randomFallback", null, "curatedFlashback", "精選回顧")]
    [InlineData("filler", null, "extraPick", "更多回憶")]
    public void ReasonSlugAndLabel(string internalReason, int? yearsAgo, string slug, string label)
    {
        Assert.Equal(slug, TodayMemoriesPresentation.ReasonSlug(internalReason));
        Assert.Equal(label, TodayMemoriesPresentation.ReasonLabel(internalReason, yearsAgo));
    }

    [Fact]
    public void Present_MapsResponseForApp()
    {
        var raw = new TodayMemoriesResponseDto
        {
            Strategy = TodayMemoriesStrategy.RandomFallback,
            ReferenceDate = new DateOnly(2026, 3, 29),
            Items =
            [
                new TodayMemoryPhotoItemDto
                {
                    Photo = new PhotoDto { Id = Guid.NewGuid() },
                    Reason = "randomFallback"
                }
            ]
        };

        var presented = TodayMemoriesPresentation.Present(raw);

        Assert.Equal("curatedFlashback", TodayMemoriesPresentation.StrategySlug(presented.Strategy));
        Assert.Equal("精選回顧", presented.StrategyLabel);
        Assert.Equal("curatedFlashback", presented.Items[0].Reason);
        Assert.Equal("精選回顧", presented.Items[0].ReasonLabel);
    }
}
