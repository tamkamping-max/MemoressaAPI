using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Memoressa.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Tests;

public class PhotoUrlResolverTests
{
    [Fact]
    public async Task ToDtoAsync_GeneratesPresignedUrlsFromS3KeysOnly()
    {
        var s3 = new FakeS3StorageService();
        var resolver = new PhotoUrlResolver(s3, Options.Create(new AwsS3Options
        {
            PresignedUrlExpiryMinutes = 15,
            AiPresignedUrlExpiryMinutes = 60
        }));

        var photo = new Photo
        {
            S3Key = "uploads/family/user/photo.jpg",
            ThumbnailS3Key = null
        };

        var dto = await resolver.ToDtoAsync(photo);

        Assert.Equal("GET:uploads/family/user/photo.jpg:15", dto.RemoteUrl);
        Assert.Equal("GET:uploads/family/user/photo_nail.jpg:15", dto.ThumbnailUrl);
        Assert.Equal(dto.RemoteUrl, dto.AssetPath);
        Assert.Equal(dto.ThumbnailUrl, dto.ThumbnailPath);
    }

    [Fact]
    public async Task ToDtoAsync_UsesDedicatedThumbnailKeyWhenPresent()
    {
        var s3 = new FakeS3StorageService();
        var resolver = new PhotoUrlResolver(s3, Options.Create(new AwsS3Options()));

        var photo = new Photo
        {
            S3Key = "uploads/family/user/abc.jpg",
            S3KeyFull = "uploads/family/user/IMG_1234.HEIC",
            ThumbnailS3Key = "uploads/family/user/abc_nail.jpg"
        };

        var dto = await resolver.ToDtoAsync(photo);

        Assert.Equal("GET:uploads/family/user/abc.jpg:15", dto.RemoteUrl);
        Assert.Equal("GET:uploads/family/user/abc_nail.jpg:15", dto.ThumbnailUrl);
        Assert.Equal("GET:uploads/family/user/IMG_1234.HEIC:15", dto.FullUrl);
    }

    [Fact]
    public async Task ToDtoAsync_LegacyFullUrlDerivedFromCompressedKey()
    {
        var s3 = new FakeS3StorageService();
        var resolver = new PhotoUrlResolver(s3, Options.Create(new AwsS3Options()));

        var photo = new Photo
        {
            S3Key = "uploads/family/user/abc.jpg",
            ThumbnailS3Key = "uploads/family/user/abc_nail.jpg"
        };

        var dto = await resolver.ToDtoAsync(photo);

        Assert.Equal("GET:uploads/family/user/abc_full.jpg:15", dto.FullUrl);
    }

    private sealed class FakeS3StorageService : IS3StorageService
    {
        public string BuildObjectKey(Guid familyId, Guid userId, string fileName) => fileName;

        public PhotoUploadKeys.VariantKeys BuildPhotoUploadKeys(
            Guid familyId,
            Guid userId,
            string compressedFileName,
            string originalFileName,
            string? livePhotoVideoFileName) =>
            PhotoUploadKeys.Build(
                "uploads",
                familyId,
                userId,
                Guid.NewGuid(),
                compressedFileName,
                originalFileName,
                livePhotoVideoFileName);

        public Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(1024);

        public string BuildThumbnailKey(string originalS3Key)
        {
            var lastSlash = originalS3Key.LastIndexOf('/');
            return lastSlash < 0
                ? $"{originalS3Key}_thumb.jpg"
                : string.Concat(originalS3Key.AsSpan(0, lastSlash + 1), "thumb.jpg");
        }

        public Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task PutObjectAsync(string s3Key, byte[] bytes, string contentType, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> GetPresignedPutUrlAsync(string s3Key, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult($"PUT:{s3Key}:{expiry.TotalMinutes}");

        public Task<string> GetPresignedGetUrlAsync(string s3Key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult($"GET:{s3Key}:{expiry.TotalMinutes}");

        public Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
