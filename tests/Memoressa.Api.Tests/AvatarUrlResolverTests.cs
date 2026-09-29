using Memoressa.Application.Common;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Options;
using Memoressa.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Tests;

public class AvatarUrlResolverTests
{
    [Fact]
    public async Task ToUserDtoAsync_PresignsStoredKey()
    {
        const string key = "avatars/users/u1/face.jpg";
        var resolver = new AvatarUrlResolver(
            new FakeS3(),
            Options.Create(new AwsS3Options { PresignedUrlExpiryMinutes = 15 }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AvatarUrlResolver>.Instance);

        var user = new UserAccount
        {
            Email = "a@test.com",
            AvatarUrl = key
        };

        var dto = await resolver.ToUserDtoAsync(user);

        Assert.StartsWith("https://signed/", dto.AvatarUrl, StringComparison.Ordinal);
        Assert.Contains(key, dto.AvatarUrl, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToUserDtoAsync_PassesThroughHttpsUrl()
    {
        const string url = "https://cdn.example.com/avatar.jpg";
        var resolver = new AvatarUrlResolver(new FakeS3(), Options.Create(new AwsS3Options()), Microsoft.Extensions.Logging.Abstractions.NullLogger<AvatarUrlResolver>.Instance);

        var dto = await resolver.ToUserDtoAsync(new UserAccount { Email = "a@test.com", AvatarUrl = url });

        Assert.Equal(url, dto.AvatarUrl);
    }

    private sealed class FakeS3 : IS3StorageService
    {
        public Task<string> GetPresignedGetUrlAsync(
            string s3Key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            Task.FromResult($"https://signed/{s3Key}?exp={expiry.TotalMinutes}");

        public string BuildObjectKey(Guid familyId, Guid userId, string fileName) => throw new NotImplementedException();
        public PhotoUploadKeys.VariantKeys BuildPhotoUploadKeys(
            Guid familyId,
            Guid userId,
            string compressedFileName,
            string originalFileName,
            string? livePhotoVideoFileName) =>
            throw new NotImplementedException();
        public Task<string> GetPresignedPutUrlAsync(
            string s3Key,
            string contentType,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task PutObjectAsync(string s3Key, byte[] bytes, string contentType, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public string BuildThumbnailKey(string originalS3Key) => throw new NotImplementedException();
    }
}
