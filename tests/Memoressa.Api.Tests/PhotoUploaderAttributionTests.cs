using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Tests;

public class PhotoUploaderAttributionTests
{
    [Fact]
    public async Task Timeline_IncludesUploaderNickname_ForFamilyMemberUpload()
    {
        var familyId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using var db = CreateDb();
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = viewerId });
        db.UserAccounts.AddRange(
            new UserAccount { Id = viewerId, Email = "viewer@test.com", PasswordHash = "x", IsActive = true, Nickname = "Viewer" },
            new UserAccount { Id = uploaderId, Email = "uploader@test.com", PasswordHash = "x", IsActive = true });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyId, UserId = viewerId },
            new FamilyMembership { FamilyId = familyId, UserId = uploaderId });
        db.FamilyMembers.Add(new FamilyMember
        {
            Id = memberId,
            FamilyId = familyId,
            Name = "Alice",
            LinkedUserId = uploaderId
        });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = uploaderId,
            S3Key = "a.jpg",
            ContentType = "image/jpeg",
            TakenAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new PhotoService(
            db,
            new FixedUser(viewerId, familyId),
            new SummaryPhotoUrlResolver(),
            new StubS3Storage(),
            Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

        var timeline = await service.GetTimelinePhotosAsync(limit: 10);
        Assert.True(timeline.Success);
        var item = Assert.Single(timeline.Data!.Items);
        Assert.Equal(uploaderId, item.UploadedBy);
        Assert.Equal("Alice", item.UploaderNickname);
        Assert.Equal("Alice", item.UploaderDisplayName);
        Assert.Equal("Alice", item.Uploader!.Nickname);
    }

    [Fact]
    public async Task BatchGetPhotosAsync_IncludesUploaderFields()
    {
        var familyId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = viewerId });
        db.UserAccounts.AddRange(
            new UserAccount { Id = viewerId, Email = "viewer@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = uploaderId, Email = "bob@test.com", PasswordHash = "x", IsActive = true, Nickname = "Bob" });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyId, UserId = viewerId },
            new FamilyMembership { FamilyId = familyId, UserId = uploaderId });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = uploaderId,
            S3Key = "b.jpg",
            ContentType = "image/jpeg"
        });
        await db.SaveChangesAsync();

        var service = new PhotoService(
            db,
            new FixedUser(viewerId, familyId),
            new SummaryPhotoUrlResolver(),
            new StubS3Storage(),
            Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

        var batch = await service.BatchGetPhotosAsync(new PhotoBatchRequestDto { Ids = [photoId.ToString()] });
        Assert.True(batch.Success);
        var summary = Assert.Single(batch.Data!.Items);
        Assert.Equal(uploaderId, summary.UploadedBy);
        Assert.Equal("Bob", summary.UploaderNickname);
        Assert.Equal("Bob", summary.UploaderDisplayName);
        Assert.NotNull(summary.Uploader);
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class FixedUser(Guid userId, Guid familyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => familyId;
        public bool IsAuthenticated => true;
    }

    private sealed class SummaryPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhotoDto { Id = photo.Id, UploadedBy = photo.UploadedByUserId });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>(
                photos.Select(p => new PhotoDto { Id = p.Id, UploadedBy = p.UploadedByUserId }).ToList());

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("url");
    }

    private sealed class StubS3Storage : IS3StorageService
    {
        public string BuildObjectKey(Guid familyId, Guid userId, string fileName) => fileName;
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
        public Task<string> GetPresignedGetUrlAsync(
            string s3Key,
            TimeSpan expiry,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(1);
        public Task<byte[]> GetObjectBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task PutObjectAsync(
            string s3Key,
            byte[] bytes,
            string contentType,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public string BuildThumbnailKey(string originalS3Key) => originalS3Key;
    }

    private sealed class StubPhotoAlbumService : IPhotoAlbumService
    {
        public Task<ServiceResult<PhotoAlbumDto>> CreateOrFindAsync(
            CreatePhotoAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumListPageDto>> ListCardsAsync(
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> UpdateAsync(
            Guid id,
            UpdatePhotoAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> PatchPhotosAsync(
            Guid id,
            PatchPhotoAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumDto>> UnlinkPhotosAsync(
            Guid id,
            UnlinkPhotoAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>> GetCommentsAsync(
            Guid albumId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult<PhotoAlbumCommentDto>> AddCommentAsync(
            Guid albumId,
            AddPhotoAlbumCommentRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<ServiceResult> DeleteCommentAsync(
            Guid albumId,
            Guid commentId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
        public Task<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?> TryGetPrimaryAlbumForPhotoAsync(
            Guid photoId,
            Guid familyId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?>(null);
    }
}
