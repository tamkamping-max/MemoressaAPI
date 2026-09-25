namespace Memoressa.Application.Common;

public static class PhotoUploadKeys
{
    public sealed record VariantKeys(
        string CompressedObjectKey,
        string FullObjectKey,
        string ThumbnailObjectKey,
        string CompressedFileName,
        string FullFileName,
        string ThumbnailFileName);

    public static string NormalizeFileName(string fileName)
    {
        var safe = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "photo.jpg" : fileName);
        var stem = Path.GetFileNameWithoutExtension(safe);
        if (string.IsNullOrWhiteSpace(stem))
        {
            stem = "photo";
        }

        return $"{stem}.jpg";
    }

    public static VariantKeys Build(
        string keyPrefix,
        Guid familyId,
        Guid userId,
        string fileName,
        Guid uploadFolderId)
    {
        var compressedFileName = NormalizeFileName(fileName);
        var stem = Path.GetFileNameWithoutExtension(compressedFileName);
        var fullFileName = $"{stem}_full.jpg";
        var thumbnailFileName = $"{stem}_nail.jpg";
        var folder = $"{keyPrefix.TrimEnd('/')}/{familyId:N}/{userId:N}/{uploadFolderId:N}";

        return new VariantKeys(
            $"{folder}/{compressedFileName}",
            $"{folder}/{fullFileName}",
            $"{folder}/{thumbnailFileName}",
            compressedFileName,
            fullFileName,
            thumbnailFileName);
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
}
