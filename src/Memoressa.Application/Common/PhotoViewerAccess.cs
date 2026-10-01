using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

/// <summary>
/// Limits photo visibility to the authenticated viewer within their resolved family.
/// </summary>
public static class PhotoViewerAccess
{
    public static IQueryable<Photo> ApplyViewerFilter(IQueryable<Photo> query, Guid viewerUserId) =>
        query.Where(p =>
            p.PrivacyScope != UploadPrivacyScope.OnlySelf
            || p.UploadedByUserId == viewerUserId);

    public static bool CanView(Photo photo, Guid viewerUserId) =>
        photo.PrivacyScope != UploadPrivacyScope.OnlySelf
        || photo.UploadedByUserId == viewerUserId;

    public static bool IsUploader(Photo photo, Guid userId) =>
        photo.UploadedByUserId == userId;
}
