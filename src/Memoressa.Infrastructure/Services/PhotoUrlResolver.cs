using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Memoressa.Infrastructure.Services;

public class PhotoUrlResolver : IPhotoUrlResolver
{
    private readonly IS3StorageService _s3;
    private readonly AwsS3Options _options;

    public PhotoUrlResolver(IS3StorageService s3, IOptions<AwsS3Options> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<PhotoDto> ToDtoAsync(
        Photo photo,
        PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
        CancellationToken cancellationToken = default)
    {
        var remoteUrl = await GetPresignedUrlAsync(photo, thumbnail: false, purpose, cancellationToken);
        var thumbnailUrl = await GetPresignedUrlAsync(photo, thumbnail: true, purpose, cancellationToken);

        return photo.ToDto(remoteUrl, thumbnailUrl);
    }

    public async Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
        IEnumerable<Photo> photos,
        PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
        CancellationToken cancellationToken = default)
    {
        var list = new List<PhotoDto>();
        foreach (var photo in photos)
        {
            list.Add(await ToDtoAsync(photo, purpose, cancellationToken));
        }

        return list;
    }

    public async Task<string?> GetPresignedUrlAsync(
        Photo photo,
        bool thumbnail = false,
        PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
        CancellationToken cancellationToken = default)
    {
        var s3Key = ResolveS3Key(photo, thumbnail);
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            return photo.LocalAssetPath;
        }

        var expiry = purpose switch
        {
            PhotoUrlPurpose.AiProcessing => TimeSpan.FromMinutes(_options.AiPresignedUrlExpiryMinutes),
            _ => TimeSpan.FromMinutes(_options.PresignedUrlExpiryMinutes)
        };

        return await _s3.GetPresignedGetUrlAsync(s3Key, expiry, cancellationToken);
    }

    private static string? ResolveS3Key(Photo photo, bool thumbnail)
    {
        if (thumbnail)
        {
            return !string.IsNullOrWhiteSpace(photo.ThumbnailS3Key)
                ? photo.ThumbnailS3Key
                : photo.S3Key;
        }

        return photo.S3Key;
    }
}
