using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class PhotoSummaryLoader
{
    public static async Task<IReadOnlyList<PhotoSummaryDto>> LoadSummariesAsync(
        IMemoressaDbContext db,
        IPhotoUrlResolver photoUrls,
        Guid familyId,
        Guid viewerUserId,
        IReadOnlyList<Guid> photoIdsInOrder,
        CancellationToken cancellationToken)
    {
        if (photoIdsInOrder.Count == 0)
        {
            return [];
        }

        var distinct = photoIdsInOrder.Distinct().ToList();
        var photos = await PhotoViewerAccess.ApplyViewerFilter(
                db.Photos.AsNoTracking()
                    .Where(p => p.FamilyId == familyId && distinct.Contains(p.Id) && !p.IsHidden)
                    .Include(p => p.UploadedBy),
                viewerUserId)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return [];
        }

        var dtos = await photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken);
        var dtoById = dtos.ToDictionary(d => d.Id);
        var photoById = photos.ToDictionary(p => p.Id);

        var summaries = new List<PhotoSummaryDto>(photoIdsInOrder.Count);
        foreach (var id in photoIdsInOrder)
        {
            if (!photoById.TryGetValue(id, out var photo) || !dtoById.TryGetValue(id, out var dto))
            {
                continue;
            }

            summaries.Add(PhotoSummaryMapping.FromPhoto(photo, dto));
        }

        return summaries;
    }

    public static async Task<IReadOnlyList<PhotoSummaryDto>> LoadSummariesCrossFamilyAsync(
        IMemoressaDbContext db,
        IPhotoUrlResolver photoUrls,
        Guid viewerUserId,
        IReadOnlyList<Guid> photoIdsInOrder,
        CancellationToken cancellationToken)
    {
        if (photoIdsInOrder.Count == 0)
        {
            return [];
        }

        var distinct = photoIdsInOrder.Distinct().ToList();
        var photos = await PhotoViewerAccess.ApplyViewerFilter(
                db.Photos.AsNoTracking()
                    .Where(p => distinct.Contains(p.Id) && !p.IsHidden)
                    .Include(p => p.UploadedBy),
                viewerUserId)
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return [];
        }

        var dtos = await photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken);
        var dtoById = dtos.ToDictionary(d => d.Id);
        var photoById = photos.ToDictionary(p => p.Id);

        var summaries = new List<PhotoSummaryDto>(photoIdsInOrder.Count);
        foreach (var id in photoIdsInOrder)
        {
            if (!photoById.TryGetValue(id, out var photo) || !dtoById.TryGetValue(id, out var dto))
            {
                continue;
            }

            summaries.Add(PhotoSummaryMapping.FromPhoto(photo, dto));
        }

        return summaries;
    }
}
