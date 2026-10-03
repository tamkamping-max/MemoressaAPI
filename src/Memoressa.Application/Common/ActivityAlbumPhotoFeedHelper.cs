using Memoressa.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public readonly record struct ActivityAlbumPhotoFeedSnapshot(
    int PhotoCount,
    IReadOnlyList<Guid> PreviewPhotoIds);

public static class ActivityAlbumPhotoFeedHelper
{
    public static async Task<IReadOnlyDictionary<Guid, ActivityAlbumPhotoFeedSnapshot>> LoadSnapshotsAsync(
        IMemoressaDbContext db,
        IReadOnlyList<Guid> activityAlbumIds,
        int previewLimit,
        CancellationToken cancellationToken)
    {
        if (activityAlbumIds.Count == 0)
        {
            return new Dictionary<Guid, ActivityAlbumPhotoFeedSnapshot>();
        }

        var counts = await db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => activityAlbumIds.Contains(ap.ActivityAlbumId))
            .GroupBy(ap => ap.ActivityAlbumId)
            .Select(g => new { ActivityAlbumId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = counts.ToDictionary(x => x.ActivityAlbumId, x => x.Count);

        var links = await db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => activityAlbumIds.Contains(ap.ActivityAlbumId))
            .OrderByDescending(ap => ap.SortOrder)
            .ThenByDescending(ap => ap.CreatedAt)
            .Select(ap => new { ap.ActivityAlbumId, ap.PhotoId, ap.SortOrder, ap.CreatedAt })
            .ToListAsync(cancellationToken);

        var previewMap = new Dictionary<Guid, List<Guid>>();
        foreach (var link in links)
        {
            if (!previewMap.TryGetValue(link.ActivityAlbumId, out var list))
            {
                list = [];
                previewMap[link.ActivityAlbumId] = list;
            }

            if (list.Count < previewLimit)
            {
                list.Add(link.PhotoId);
            }
        }

        var result = new Dictionary<Guid, ActivityAlbumPhotoFeedSnapshot>(activityAlbumIds.Count);
        foreach (var id in activityAlbumIds)
        {
            var photoCount = countMap.GetValueOrDefault(id);
            previewMap.TryGetValue(id, out var previewIds);
            result[id] = new ActivityAlbumPhotoFeedSnapshot(photoCount, previewIds ?? []);
        }

        return result;
    }
}
