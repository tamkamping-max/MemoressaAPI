using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.Common;

public static class PhotoUploaderEnrichment
{
    public static async Task<PhotoDto> EnrichPhotoDtoAsync(
        IMemoressaDbContext db,
        Photo photo,
        PhotoDto dto,
        CancellationToken cancellationToken = default)
    {
        var list = await EnrichPhotoDtosAsync(db, [photo], [dto], cancellationToken);
        return list[0];
    }

    public static async Task<IReadOnlyList<PhotoDto>> EnrichPhotoDtosAsync(
        IMemoressaDbContext db,
        IReadOnlyList<Photo> photos,
        IReadOnlyList<PhotoDto> dtos,
        CancellationToken cancellationToken = default)
    {
        if (photos.Count == 0)
        {
            return dtos;
        }

        var labels = await PhotoUploaderFamilyLabels.LoadAsync(db, photos, cancellationToken);
        var photoById = photos.ToDictionary(p => p.Id);
        var enriched = new List<PhotoDto>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (!photoById.TryGetValue(dto.Id, out var photo))
            {
                enriched.Add(dto);
                continue;
            }

            labels.TryGetValue((photo.FamilyId, photo.UploadedByUserId), out var familyLabel);
            var fields = PhotoUploaderDisplay.Resolve(photo, familyLabel);
            enriched.Add(PhotoUploaderDisplay.ApplyToDto(dto, fields));
        }

        return enriched;
    }
}
