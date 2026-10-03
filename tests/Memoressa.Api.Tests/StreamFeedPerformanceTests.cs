using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class StreamFeedPerformanceTests
{
    [Fact]
    public async Task BatchGetPhotosAsync_ReturnsUpTo50SummariesInOrder()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        foreach (var id in ids)
        {
            db.Photos.Add(new Photo
            {
                Id = id,
                FamilyId = familyId,
                UploadedByUserId = userId,
                S3Key = $"{id}.jpg"
            });
        }

        await db.SaveChangesAsync();

        var service = new PhotoService(
            db,
            new FixedUser(userId, familyId),
            new SummaryPhotoUrlResolver(),
            new StubS3Storage(),
            Microsoft.Extensions.Options.Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

        var result = await service.BatchGetPhotosAsync(new PhotoBatchRequestDto
        {
            Ids = ids.Select(id => $"photo_{id:N}").Reverse().ToList()
        });

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Items.Count);
        Assert.Equal(ids[2], result.Data.Items[0].Id);
        Assert.Equal("thumb", result.Data.Items[0].ThumbnailUrl);
    }

    [Fact]
    public async Task GetMemoriesAsync_IncludesPhotoCountAndCoverPhotos()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var photoIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        foreach (var id in photoIds)
        {
            db.Photos.Add(new Photo
            {
                Id = id,
                FamilyId = familyId,
                UploadedByUserId = userId,
                S3Key = $"{id}.jpg"
            });
        }

        var memory = new Memory
        {
            FamilyId = familyId,
            CreatedByUserId = userId,
            Title = "Trip",
            Type = MemoryType.Photo
        };
        db.Memories.Add(memory);
        var order = 0;
        foreach (var id in photoIds)
        {
            db.MemoryPhotos.Add(new MemoryPhoto { MemoryId = memory.Id, PhotoId = id, SortOrder = order++ });
        }

        await db.SaveChangesAsync();

        var service = new MemoryService(db, new FixedUser(userId, familyId), new SummaryPhotoUrlResolver());
        var result = await service.GetMemoriesAsync();

        Assert.True(result.Success);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(5, dto.PhotoCount);
        Assert.Equal(4, dto.CoverPhotos.Count);
        Assert.NotNull(dto.CoverThumbnailUrl);
    }

    [Fact]
    public async Task GetInProgressAsync_IncludesPhotoCountAndPreviewPhotos()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var photoIds = Enumerable.Range(0, 15).Select(_ => Guid.NewGuid()).ToList();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        foreach (var id in photoIds)
        {
            db.Photos.Add(new Photo
            {
                Id = id,
                FamilyId = familyId,
                UploadedByUserId = userId,
                S3Key = $"{id}.jpg"
            });
        }

        var activity = new ActivityAlbum
        {
            FamilyId = familyId,
            ExternalId = "act_feed",
            Title = "Party",
            Type = ActivityAlbumType.Gathering,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userId
        };
        db.ActivityAlbums.Add(activity);
        var sort = 0;
        foreach (var id in photoIds)
        {
            db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
            {
                ActivityAlbumId = activity.Id,
                PhotoId = id,
                SortOrder = sort++
            });
        }

        await db.SaveChangesAsync();

        var service = new ActivityService(
            db,
            new FixedUser(userId, familyId),
            new SummaryPhotoUrlResolver(),
            new StubAvatarUrlResolver());

        var result = await service.GetInProgressAsync();
        var item = Assert.Single(result.Data!.Data.Items);
        Assert.Equal(15, item.PhotoCount);
        Assert.Equal(12, item.PreviewPhotos.Count);

        var photos = await service.GetActivityPhotosAsync(activity.ExternalId, limit: 5);
        Assert.True(photos.Success);
        Assert.Equal(15, photos.Data!.Data.Total);
        Assert.Equal(5, photos.Data.Data.Items.Count);
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
            Task.FromResult(new PhotoDto
            {
                Id = photo.Id,
                ThumbnailUrl = "thumb",
                RemoteUrl = "remote",
                FullUrl = "full",
                UploadedBy = photo.UploadedByUserId
            });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>(
                photos.Select(p => new PhotoDto
                {
                    Id = p.Id,
                    ThumbnailUrl = "thumb",
                    RemoteUrl = "remote",
                    FullUrl = "full",
                    UploadedBy = p.UploadedByUserId
                }).ToList());

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
