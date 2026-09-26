using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class PhotoDeletionCleanup
{
    /// <summary>
    /// After a photo row is removed (and FK cascades have run), prune 今日回憶 caches and empty 我的回憶 entries.
    /// </summary>
    public static async Task AfterPhotoRemovedAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid photoId,
        IReadOnlyList<Guid> memoryIdsThatReferencedPhoto,
        IReadOnlyList<TodayMemoriesCache> todayMemoriesCaches,
        CancellationToken cancellationToken = default)
    {
        foreach (var cache in todayMemoriesCaches)
        {
            PruneTodayMemoriesCacheEntry(cache, photoId);
        }

        if (memoryIdsThatReferencedPhoto.Count == 0)
        {
            return;
        }

        foreach (var memoryId in memoryIdsThatReferencedPhoto)
        {
            // Exclude photoId: cleanup runs in the same transaction before SaveChanges, so the link row may still exist in DB.
            var hasPhotos = await db.MemoryPhotos.AsNoTracking()
                .AnyAsync(mp => mp.MemoryId == memoryId && mp.PhotoId != photoId, cancellationToken);
            if (hasPhotos)
            {
                continue;
            }

            var hasVideos = await db.MemoryVideos.AsNoTracking()
                .AnyAsync(mv => mv.MemoryId == memoryId, cancellationToken);
            if (hasVideos)
            {
                continue;
            }

            var highlightCaches = await db.TodayHighlightCaches
                .Where(h => h.MemoryId == memoryId)
                .ToListAsync(cancellationToken);
            if (highlightCaches.Count > 0)
            {
                db.TodayHighlightCaches.RemoveRange(highlightCaches);
            }

            var memory = await db.Memories.FirstOrDefaultAsync(m => m.Id == memoryId, cancellationToken);
            if (memory is not null)
            {
                db.Memories.Remove(memory);
            }
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
