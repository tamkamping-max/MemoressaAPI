using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class PhotoDeletionCleanup
{
    /// <summary>
    /// After a photo row is removed, prune 今日回憶 caches only. 我的回憶 rows are user-created;
    /// photo links drop via FK cascade on <c>memory_photos</c> — do not auto-delete or create memories here.
    /// </summary>
    public static void PruneTodayMemoriesCaches(IReadOnlyList<TodayMemoriesCache> todayMemoriesCaches, Guid photoId)
    {
        foreach (var cache in todayMemoriesCaches)
        {
            PruneTodayMemoriesCacheEntry(cache, photoId);
        }
    }

    public static bool PruneTodayMemoriesCacheEntry(TodayMemoriesCache cache, Guid photoId)
    {
        var items = TodayMemoriesCacheCodec.Deserialize(cache.ItemsJson);
        var filtered = items.Where(i => i.PhotoId != photoId).ToList();
        if (filtered.Count == items.Count)
        {
            return false;
        }

        cache.ItemsJson = TodayMemoriesCacheCodec.SerializeCachedItems(filtered);
        if (filtered.Count == 0)
        {
            cache.Strategy = TodayMemoriesStrategy.Empty;
        }

        cache.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
