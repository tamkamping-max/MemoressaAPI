using Memoressa.Application.Common;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class TodayMemoriesCacheRefreshTests
{
    [Fact]
    public void ShouldRecomposeEmptyCache_WhenEligiblePhotosReachMinimum()
    {
        var cache = new TodayMemoriesCache
        {
            Strategy = TodayMemoriesStrategy.Empty,
            ItemsJson = "[]"
        };

        Assert.True(TodayMemoriesCacheRefresh.ShouldRecomposeEmptyCache(cache, TodayMemoriesConstants.MinPhotos));
        Assert.False(TodayMemoriesCacheRefresh.ShouldRecomposeEmptyCache(cache, TodayMemoriesConstants.MinPhotos - 1));
    }

    [Fact]
    public void ShouldNotRecompose_WhenCacheHasItems()
    {
        var cache = new TodayMemoriesCache
        {
            Strategy = TodayMemoriesStrategy.YearsAgoToday,
            ItemsJson = TodayMemoriesCacheCodec.SerializeEntries([])
        };

        Assert.False(TodayMemoriesCacheRefresh.ShouldRecomposeEmptyCache(cache, 100));
    }
}
