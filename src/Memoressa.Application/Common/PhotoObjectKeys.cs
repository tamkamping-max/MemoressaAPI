using Memoressa.Domain.Entities;

namespace Memoressa.Application.Common;

public static class PhotoObjectKeys
{
    public static bool UsesClientVariantLayout(Photo photo) =>
        !string.IsNullOrWhiteSpace(photo.ThumbnailS3Key)
        && photo.ThumbnailS3Key.EndsWith("_nail.jpg", StringComparison.OrdinalIgnoreCase);

    public static string? ResolveFullObjectKey(Photo photo)
    {
        if (string.IsNullOrWhiteSpace(photo.S3Key))
        {
            return null;
        }

        return UsesClientVariantLayout(photo)
            ? PhotoUploadKeys.GetFullObjectKeyFromCompressed(photo.S3Key)
            : photo.S3Key;
    }

    public static IReadOnlyList<string> CollectDeleteKeys(Photo photo)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(photo.S3Key))
        {
            return [];
        }

        keys.Add(photo.S3Key);

        if (UsesClientVariantLayout(photo))
        {
            keys.Add(PhotoUploadKeys.GetFullObjectKeyFromCompressed(photo.S3Key));
            keys.Add(
                string.IsNullOrWhiteSpace(photo.ThumbnailS3Key)
                    ? PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(photo.S3Key)
                    : photo.ThumbnailS3Key);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(photo.ThumbnailS3Key))
            {
                keys.Add(photo.ThumbnailS3Key);
            }

            var legacyThumb = PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(photo.S3Key);
            keys.Add(legacyThumb);
        }

        return keys.ToList();
    }

    public static string? GetOriginalDownloadFileName(Photo photo)
    {
        var fullKey = ResolveFullObjectKey(photo);
        return string.IsNullOrWhiteSpace(fullKey) ? null : Path.GetFileName(fullKey);
    }
}
