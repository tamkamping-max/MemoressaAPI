using Memoressa.Application.Common;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class PhotoDeletionCleanupTests
{
    [Fact]
    public void PruneTodayMemoriesCacheEntry_RemovesPhotoAndClearsStrategyWhenEmpty()
    {
        var photoId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var cache = new TodayMemoriesCache
        {
            Strategy = TodayMemoriesStrategy.YearsAgoToday,
            ItemsJson = TodayMemoriesCacheCodec.SerializeCachedItems(
            [
                new TodayMemoriesCacheCodec.CachedItem(photoId, "yearsAgoToday", 1, null),
                new TodayMemoriesCacheCodec.CachedItem(otherId, "yearsAgoToday", 2, null)
            ])
        };

        Assert.True(PhotoDeletionCleanup.PruneTodayMemoriesCacheEntry(cache, photoId));
        var remaining = TodayMemoriesCacheCodec.Deserialize(cache.ItemsJson);
        Assert.Single(remaining);
        Assert.Equal(otherId, remaining[0].PhotoId);
        Assert.Equal(TodayMemoriesStrategy.YearsAgoToday, cache.Strategy);
    }

    [Fact]
    public void PruneTodayMemoriesCacheEntry_SetsEmptyStrategyWhenLastPhotoRemoved()
    {
        var photoId = Guid.NewGuid();
        var cache = new TodayMemoriesCache
        {
            Strategy = TodayMemoriesStrategy.RandomFallback,
            ItemsJson = TodayMemoriesCacheCodec.SerializeCachedItems(
            [
                new TodayMemoriesCacheCodec.CachedItem(photoId, "randomFallback", null, null)
            ])
        };

        PhotoDeletionCleanup.PruneTodayMemoriesCacheEntry(cache, photoId);
        Assert.Empty(TodayMemoriesCacheCodec.Deserialize(cache.ItemsJson));
        Assert.Equal(TodayMemoriesStrategy.Empty, cache.Strategy);
    }
}
