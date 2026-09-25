namespace Memoressa.Application.Common;

public static class PhotoUploadKeys
{
    private static readonly HashSet<string> AllowedOriginalExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".heic", ".heif", ".png"
    };

    private static readonly HashSet<string> AllowedOriginalContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/heic", "image/heif", "image/png"
    };

    private static readonly HashSet<string> AllowedLiveVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/quicktime", "video/mp4"
    };

    private static readonly HashSet<string> AllowedLiveVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mov", ".mp4"
    };

    public sealed record VariantKeys(
        string CompressedObjectKey,
        string FullObjectKey,
        string ThumbnailObjectKey,
        string? LivePhotoVideoObjectKey,
        string CompressedFileName,
        string FullFileName,
        string ThumbnailFileName,
        string? LivePhotoVideoFileName);

    public static string NormalizeCompressedFileName(string fileName)
    {
        var safe = SanitizeFileName(fileName);
        var stem = Path.GetFileNameWithoutExtension(safe);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "photo";
        }

        return $"{stem}.jpg";
    }

    public static bool TryNormalizeOriginalFileName(string fileName, out string normalized)
    {
        normalized = SanitizeFileName(fileName);
        var ext = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedOriginalExtensions.Contains(ext))
        {
            normalized = string.Empty;
            return false;
        }

        return true;
    }

    public static bool TryNormalizeLiveVideoFileName(string fileName, out string normalized)
    {
        normalized = SanitizeFileName(fileName);
        var ext = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedLiveVideoExtensions.Contains(ext))
        {
            normalized = string.Empty;
            return false;
        }

        return true;
    }

    public static bool IsAllowedOriginalContentType(string contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedOriginalContentTypes.Contains(contentType.Trim());

    public static bool IsAllowedLiveVideoContentType(string contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && AllowedLiveVideoContentTypes.Contains(contentType.Trim());

    public static VariantKeys Build(
        string keyPrefix,
        Guid familyId,
        Guid userId,
        Guid uploadFolderId,
        string compressedFileName,
        string originalFileName,
        string? livePhotoVideoFileName)
    {
        var compressed = NormalizeCompressedFileName(compressedFileName);
        if (!TryNormalizeOriginalFileName(originalFileName, out var original))
        {
            throw new ArgumentException("Invalid original file name", nameof(originalFileName));
        }

        string? liveVideoFile = null;
        if (!string.IsNullOrWhiteSpace(livePhotoVideoFileName))
        {
            if (!TryNormalizeLiveVideoFileName(livePhotoVideoFileName, out liveVideoFile))
            {
                throw new ArgumentException("Invalid Live Photo video file name", nameof(livePhotoVideoFileName));
            }
        }

        var stem = Path.GetFileNameWithoutExtension(compressed);
        var thumbnailFileName = $"{stem}_nail.jpg";
        var folder = $"{keyPrefix.TrimEnd('/')}/{familyId:N}/{userId:N}/{uploadFolderId:N}";

        return new VariantKeys(
            $"{folder}/{compressed}",
            $"{folder}/{original}",
            $"{folder}/{thumbnailFileName}",
            liveVideoFile is null ? null : $"{folder}/{liveVideoFile}",
            compressed,
            original,
            thumbnailFileName,
            liveVideoFile);
    }

    public static string GetFullObjectKeyFromCompressed(string compressedObjectKey)
    {
        var fileName = Path.GetFileName(compressedObjectKey);
        var directory = compressedObjectKey[..(compressedObjectKey.Length - fileName.Length)];
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{directory}{stem}_full.jpg";
    }

    public static string GetThumbnailObjectKeyFromCompressed(string compressedObjectKey)
    {
        var fileName = Path.GetFileName(compressedObjectKey);
        var directory = compressedObjectKey[..(compressedObjectKey.Length - fileName.Length)];
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{directory}{stem}_nail.jpg";
    }

    public static bool UsesVariantLayout(string? compressedObjectKey, string? fullObjectKey) =>
        !string.IsNullOrWhiteSpace(compressedObjectKey) && !string.IsNullOrWhiteSpace(fullObjectKey);

    private static string SanitizeFileName(string fileName)
    {
        var safe = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "photo.jpg" : fileName.Trim());
        return safe.Replace('\\', '_').Replace('/', '_');
    }
}
