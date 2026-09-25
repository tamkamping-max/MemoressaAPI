using Memoressa.Domain.Entities;

namespace Memoressa.Application.Common;

public static class PhotoObjectKeys
{
    public static bool UsesClientVariantLayout(Photo photo) =>
        !string.IsNullOrWhiteSpace(photo.S3KeyFull)
        || (!string.IsNullOrWhiteSpace(photo.ThumbnailS3Key)
            && photo.ThumbnailS3Key.EndsWith("_nail.jpg", StringComparison.OrdinalIgnoreCase));

    public static string? ResolveFullObjectKey(Photo photo)
    {
        if (!string.IsNullOrWhiteSpace(photo.S3KeyFull))
        {
            return photo.S3KeyFull;
        }

        if (string.IsNullOrWhiteSpace(photo.S3Key))
        {
            return null;
        }

        return UsesClientVariantLayout(photo)
            ? PhotoUploadKeys.GetFullObjectKeyFromCompressed(photo.S3Key)
            : photo.S3Key;
    }

    public static string? ResolveThumbnailObjectKey(Photo photo)
    {
        if (!string.IsNullOrWhiteSpace(photo.ThumbnailS3Key))
        {
            return photo.ThumbnailS3Key;
        }

        return string.IsNullOrWhiteSpace(photo.S3Key)
            ? null
            : PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(photo.S3Key);
    }

    public static IReadOnlyList<string> CollectDeleteKeys(Photo photo)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(photo.S3Key))
        {
            keys.Add(photo.S3Key);
        }

        var fullKey = ResolveFullObjectKey(photo);
        if (!string.IsNullOrWhiteSpace(fullKey))
        {
            keys.Add(fullKey);
        }

        if (!string.IsNullOrWhiteSpace(photo.ThumbnailS3Key))
        {
            keys.Add(photo.ThumbnailS3Key);
        }
        else if (!string.IsNullOrWhiteSpace(photo.S3Key))
        {
            keys.Add(PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(photo.S3Key));
        }

        if (!string.IsNullOrWhiteSpace(photo.LivePhotoVideoS3Key))
        {
            keys.Add(photo.LivePhotoVideoS3Key);
        }

        return keys.ToList();
    }

    public static string? GetOriginalDownloadFileName(Photo photo) =>
        string.IsNullOrWhiteSpace(photo.OriginalFileName)
            ? (ResolveFullObjectKey(photo) is { } key ? Path.GetFileName(key) : null)
            : photo.OriginalFileName;
}
