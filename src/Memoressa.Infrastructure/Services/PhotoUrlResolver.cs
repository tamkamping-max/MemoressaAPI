using Memoressa.Application.Common;
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
        var remoteUrl = await PresignKeyAsync(ResolveCompressedObjectKey(photo), purpose, cancellationToken);
        var thumbnailUrl = await PresignKeyAsync(ResolveThumbnailObjectKey(photo), purpose, cancellationToken);
        var fullUrl = await PresignKeyAsync(ResolveFullObjectKey(photo), purpose, cancellationToken);

        return photo.ToDto(remoteUrl, thumbnailUrl, fullUrl);
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
        var s3Key = thumbnail ? ResolveThumbnailObjectKey(photo) : ResolveCompressedObjectKey(photo);
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            return photo.LocalAssetPath;
        }

        return await PresignKeyAsync(s3Key, purpose, cancellationToken);
    }

    private async Task<string?> PresignKeyAsync(
        string? s3Key,
        PhotoUrlPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(s3Key))
        {
            return null;
        }

        var expiry = purpose switch
        {
            PhotoUrlPurpose.AiProcessing => TimeSpan.FromMinutes(_options.AiPresignedUrlExpiryMinutes),
            _ => TimeSpan.FromMinutes(_options.PresignedUrlExpiryMinutes)
        };

        return await _s3.GetPresignedGetUrlAsync(s3Key, expiry, cancellationToken);
    }

    internal static string? ResolveCompressedObjectKey(Photo photo) => photo.S3Key;

    internal static string? ResolveThumbnailObjectKey(Photo photo) =>
        PhotoObjectKeys.ResolveThumbnailObjectKey(photo);

    internal static string? ResolveFullObjectKey(Photo photo) =>
        PhotoObjectKeys.ResolveFullObjectKey(photo);
}
