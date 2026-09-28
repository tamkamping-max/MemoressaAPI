namespace Memoressa.Application.Interfaces;

public interface IThumbnailGenerationService
{
    /// <summary>
    /// Downloads the uploaded original from S3, generates a JPEG thumbnail, uploads it, and sets Photo.ThumbnailS3Key.
    /// </summary>
    Task GenerateAndStoreAsync(Guid photoId, CancellationToken cancellationToken = default);
}
