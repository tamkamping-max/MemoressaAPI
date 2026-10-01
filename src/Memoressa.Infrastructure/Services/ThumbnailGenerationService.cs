using System.Diagnostics;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class ThumbnailGenerationService : IThumbnailGenerationService
{
    private readonly IMemoressaDbContext _db;
    private readonly IS3StorageService _s3;
    private readonly MediaStorageSettings _settings;
    private readonly ILogger<ThumbnailGenerationService> _logger;

    public ThumbnailGenerationService(
        IMemoressaDbContext db,
        IS3StorageService s3,
        IOptions<MediaStorageSettings> settings,
        ILogger<ThumbnailGenerationService> logger)
    {
        _db = db;
        _s3 = s3;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task GenerateAndStoreAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        try
        {
            await GenerateAndStoreCoreAsync(photoId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail generation failed for photo {PhotoId}.", photoId);
        }
    }

    private async Task GenerateAndStoreCoreAsync(Guid photoId, CancellationToken cancellationToken)
    {
        var photo = await _db.Photos.FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        if (photo is null || string.IsNullOrWhiteSpace(photo.S3Key))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(photo.ThumbnailS3Key))
        {
            return;
        }

        var sourceBytes = await LoadSourceBytesAsync(photo, cancellationToken);
        if (sourceBytes is null || sourceBytes.Length == 0)
        {
            _logger.LogWarning("Thumbnail skipped for photo {PhotoId}: unable to read source object.", photoId);
            return;
        }

        byte[]? thumbBytes = null;
        if (ThumbnailImageProcessor.IsImageContentType(photo.ContentType))
        {
            thumbBytes = TryCreateImageThumbnail(sourceBytes, photoId);
        }
        else if (ThumbnailImageProcessor.IsVideoContentType(photo.ContentType))
        {
            thumbBytes = await TryCreateVideoThumbnailAsync(sourceBytes, photoId, cancellationToken);
        }
        else
        {
            _logger.LogDebug(
                "Thumbnail skipped for photo {PhotoId}: unsupported content type {ContentType}.",
                photoId,
                photo.ContentType);
            return;
        }

        if (thumbBytes is null || thumbBytes.Length == 0)
        {
            return;
        }

        var thumbKey = _s3.BuildThumbnailKey(photo.S3Key);
        await _s3.PutObjectAsync(thumbKey, thumbBytes, "image/jpeg", cancellationToken);

        photo.ThumbnailS3Key = thumbKey;
        photo.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Stored thumbnail for photo {PhotoId} at key {ThumbnailKey} ({Bytes} bytes).",
            photoId,
            thumbKey,
            thumbBytes.Length);
    }

    private async Task<byte[]?> LoadSourceBytesAsync(Domain.Entities.Photo photo, CancellationToken cancellationToken)
    {
        if (photo.FileSizeBytes.HasValue && photo.FileSizeBytes.Value > _settings.ThumbnailMaxSourceBytes)
        {
            _logger.LogWarning(
                "Thumbnail skipped for photo {PhotoId}: source size {Bytes} exceeds limit {Limit}.",
                photo.Id,
                photo.FileSizeBytes.Value,
                _settings.ThumbnailMaxSourceBytes);
            return null;
        }

        var bytes = await _s3.GetObjectBytesAsync(photo.S3Key!, cancellationToken);
        if (bytes.Length > _settings.ThumbnailMaxSourceBytes)
        {
            _logger.LogWarning(
                "Thumbnail skipped for photo {PhotoId}: downloaded size {Bytes} exceeds limit {Limit}.",
                photo.Id,
                bytes.Length,
                _settings.ThumbnailMaxSourceBytes);
            return null;
        }

        return bytes;
    }

    private byte[]? TryCreateImageThumbnail(byte[] sourceBytes, Guid photoId)
    {
        try
        {
            return ThumbnailImageProcessor.CreateJpegThumbnail(
                sourceBytes,
                _settings.ThumbnailMaxEdgePixels,
                _settings.ThumbnailJpegQuality);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create image thumbnail for photo {PhotoId}.", photoId);
            return null;
        }
    }

    private async Task<byte[]?> TryCreateVideoThumbnailAsync(
        byte[] videoBytes,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var frameBytes = await ExtractVideoFrameAsync(videoBytes, cancellationToken);
        if (frameBytes is null || frameBytes.Length == 0)
        {
            _logger.LogWarning("Failed to extract video frame for photo {PhotoId}.", photoId);
            return null;
        }

        return TryCreateImageThumbnail(frameBytes, photoId);
    }

    private async Task<byte[]?> ExtractVideoFrameAsync(byte[] videoBytes, CancellationToken cancellationToken)
    {
        var tempVideo = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mp4");
        var tempFrame = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");

        try
        {
            await File.WriteAllBytesAsync(tempVideo, videoBytes, cancellationToken);

            var arguments =
                $"-hide_banner -loglevel error -y -ss 00:00:01 -i \"{tempVideo}\" -frames:v 1 -vf scale={_settings.ThumbnailMaxEdgePixels}:-2 \"{tempFrame}\"";

            var startInfo = new ProcessStartInfo
            {
                FileName = _settings.FfmpegPath,
                Arguments = arguments,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0 || !File.Exists(tempFrame))
            {
                _logger.LogWarning(
                    "ffmpeg exited with code {Code} for video thumbnail: {Error}",
                    process.ExitCode,
                    stderr.Trim());
                return null;
            }

            return await File.ReadAllBytesAsync(tempFrame, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffmpeg video thumbnail extraction failed.");
            return null;
        }
        finally
        {
            TryDelete(tempVideo);
            TryDelete(tempFrame);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }
}
