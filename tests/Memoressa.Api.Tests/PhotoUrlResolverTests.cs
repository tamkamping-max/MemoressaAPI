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
        Assert.Equal("GET:uploads/family/user/photo.jpg:15", dto.ThumbnailUrl);
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
            S3Key = "uploads/family/user/photo.jpg",
            ThumbnailS3Key = "uploads/family/user/photo_thumb.jpg"
        };

        var dto = await resolver.ToDtoAsync(photo);

        Assert.Equal("GET:uploads/family/user/photo.jpg:15", dto.RemoteUrl);
        Assert.Equal("GET:uploads/family/user/photo_thumb.jpg:15", dto.ThumbnailUrl);
    }

    private sealed class FakeS3StorageService : IS3StorageService
    {
        public string BuildObjectKey(Guid familyId, Guid userId, string fileName) => fileName;

        public Task<string> GetPresignedPutUrlAsync(string s3Key, string contentType, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult($"PUT:{s3Key}:{expiry.TotalMinutes}");

        public Task<string> GetPresignedGetUrlAsync(string s3Key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
            Task.FromResult($"GET:{s3Key}:{expiry.TotalMinutes}");
    }
}
