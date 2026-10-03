using Memoressa.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public readonly record struct ActivityAlbumPhotoFeedSnapshot(
    int PhotoCount,
    IReadOnlyList<Guid> PreviewPhotoIds);

public static class ActivityAlbumPhotoFeedHelper
{
    /// <summary>Raw link counts (admin); prefer <see cref="LoadViewerSnapshotsAsync"/> for API responses.</summary>
    public static async Task<IReadOnlyDictionary<Guid, ActivityAlbumPhotoFeedSnapshot>> LoadSnapshotsAsync(
        IMemoressaDbContext db,
        IReadOnlyList<Guid> activityAlbumIds,
        int previewLimit,
        CancellationToken cancellationToken) =>
        await LoadSnapshotsInternalAsync(db, activityAlbumIds, previewLimit, viewerUserId: null, cancellationToken);

    /// <summary>Viewer-visible linked photo counts and preview ids (App 1003).</summary>
    public static async Task<IReadOnlyDictionary<Guid, ActivityAlbumPhotoFeedSnapshot>> LoadViewerSnapshotsAsync(
        IMemoressaDbContext db,
        Guid viewerUserId,
        IReadOnlyList<Guid> activityAlbumIds,
        int previewLimit,
        CancellationToken cancellationToken) =>
        await LoadSnapshotsInternalAsync(db, activityAlbumIds, previewLimit, viewerUserId, cancellationToken);

    private static async Task<IReadOnlyDictionary<Guid, ActivityAlbumPhotoFeedSnapshot>> LoadSnapshotsInternalAsync(
        IMemoressaDbContext db,
        IReadOnlyList<Guid> activityAlbumIds,
        int previewLimit,
        Guid? viewerUserId,
        CancellationToken cancellationToken)
    {
        if (activityAlbumIds.Count == 0)
        {
            return new Dictionary<Guid, ActivityAlbumPhotoFeedSnapshot>();
        }

        var links = await db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => activityAlbumIds.Contains(ap.ActivityAlbumId))
            .OrderByDescending(ap => ap.SortOrder)
            .ThenByDescending(ap => ap.CreatedAt)
            .Select(ap => new { ap.ActivityAlbumId, ap.PhotoId, ap.SortOrder, ap.CreatedAt })
            .ToListAsync(cancellationToken);

        HashSet<Guid>? visiblePhotoIds = null;
        if (viewerUserId.HasValue && links.Count > 0)
        {
            var distinctPhotoIds = links.Select(l => l.PhotoId).Distinct().ToList();
            var visibleList = await PhotoViewerAccess.ApplyViewerFilter(
                    db.Photos.AsNoTracking()
                        .Where(p => distinctPhotoIds.Contains(p.Id) && !p.IsHidden),
                    viewerUserId.Value,
                    db)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            visiblePhotoIds = visibleList.ToHashSet();
        }

        var countMap = new Dictionary<Guid, int>();
        var previewMap = new Dictionary<Guid, List<Guid>>();

        foreach (var link in links)
        {
            if (visiblePhotoIds is not null && !visiblePhotoIds.Contains(link.PhotoId))
            {
                continue;
            }

            countMap.TryGetValue(link.ActivityAlbumId, out var count);
            countMap[link.ActivityAlbumId] = count + 1;

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
