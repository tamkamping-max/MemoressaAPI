using Memoressa.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class PhotoActivityLinkage
{
    public static Task<bool> IsLinkedToActivityAlbumAsync(
        IMemoressaDbContext db,
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        db.ActivityAlbumPhotos.AsNoTracking()
            .AnyAsync(link => link.PhotoId == photoId, cancellationToken);
}
