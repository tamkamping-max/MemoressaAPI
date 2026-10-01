using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class PhotoDeletionCleanup
{
    /// <summary>
    /// After a photo row is removed, prune <c>today_memories_cache</c> (App 今日回憶 + 回憶頁 via POST /photos/today-memories).
    /// Manual <c>memories</c> albums only lose links via FK cascade — no auto compose/delete here.
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
