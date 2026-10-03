using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Api.Tests;

public class UploadAbandonTests
{
    [Fact]
    public async Task AbandonUploadAsync_PendingSession_DeletesS3AndMarksExpired()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var s3 = new TrackingS3Storage(exists: true);

        await using var db = CreateDb(userId, out var familyId);
        db.UploadSessions.Add(CreatePendingSession(sessionId, userId, familyId, "fam/k.jpg", "fam/full.heic", "fam/thumb.jpg"));
        await db.SaveChangesAsync();

        var service = CreateUploadService(db, userId, s3);
        var result = await service.AbandonUploadAsync(sessionId);

        Assert.True(result.Success);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal(UploadSessionStatus.Expired, (await db.UploadSessions.SingleAsync()).Status);
        Assert.Equal(3, s3.DeletedKeys.Count);
        Assert.Contains("fam/k.jpg", s3.DeletedKeys);
    }

    [Fact]
    public async Task AbandonUploadAsync_OtherUsersSession_Returns404()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using var db = CreateDb(ownerId, out var familyId);
        db.UploadSessions.Add(CreatePendingSession(sessionId, ownerId, familyId, "a.jpg", "b.heic", "c.jpg"));
        await db.SaveChangesAsync();

        var service = CreateUploadService(db, otherId, new TrackingS3Storage());
        var result = await service.AbandonUploadAsync(sessionId);

        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task AbandonUploadAsync_CompletedSession_Returns409()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using var db = CreateDb(userId, out var familyId);
        var session = CreatePendingSession(sessionId, userId, familyId, "a.jpg", "b.heic", "c.jpg");
        session.Status = UploadSessionStatus.Completed;
        session.ResultPhotoId = Guid.NewGuid();
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateUploadService(db, userId, new TrackingS3Storage());
        var result = await service.AbandonUploadAsync(sessionId);

        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task AbandonUploadAsync_AlreadyExpired_Returns204()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using var db = CreateDb(userId, out var familyId);
        var session = CreatePendingSession(sessionId, userId, familyId, "a.jpg", "b.heic", "c.jpg");
        session.Status = UploadSessionStatus.Expired;
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var s3 = new TrackingS3Storage();
        var service = CreateUploadService(db, userId, s3);
        var result = await service.AbandonUploadAsync(sessionId);

        Assert.True(result.Success);
        Assert.Equal(204, result.StatusCode);
        Assert.Empty(s3.DeletedKeys);
    }

    [Fact]
    public async Task CleanupExpiredPendingUploadSessionsAsync_AbandonsStalePending()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var s3 = new TrackingS3Storage(exists: true);

        await using var db = CreateDb(userId, out var familyId);
        var session = CreatePendingSession(sessionId, userId, familyId, "a.jpg", "b.heic", "c.jpg");
        session.ExpiresAt = DateTime.UtcNow.AddHours(-48);
        db.UploadSessions.Add(session);
        await db.SaveChangesAsync();

        var service = CreateUploadService(db, userId, s3);
        var count = await service.CleanupExpiredPendingUploadSessionsAsync();

        Assert.Equal(1, count);
        Assert.Equal(UploadSessionStatus.Expired, (await db.UploadSessions.SingleAsync()).Status);
        Assert.NotEmpty(s3.DeletedKeys);
    }

    private static UploadService CreateUploadService(
        MemoressaDbContext db,
        Guid userId,
        IS3StorageService s3) =>
        new(
            db,
            new FixedUser(userId),
            s3,
            new StubPhotoUrlResolver(),
            Options.Create(new MediaStorageSettings()),
            Options.Create(new StorageQuotaSettings()),
            new StubActivityService(),
            Options.Create(new UploadSessionCleanupOptions { GraceHours = 24 }));

    private static MemoressaDbContext CreateDb(Guid userId, out Guid familyId)
    {
        familyId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new MemoressaDbContext(options);
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x" });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        return db;
    }

    private static UploadSession CreatePendingSession(
        Guid sessionId,
        Guid userId,
        Guid familyId,
        string s3Key,
        string s3KeyFull,
        string s3KeyThumbnail) =>
        new()
        {
            Id = sessionId,
            UserId = userId,
            FamilyId = familyId,
            MediaKind = MediaKind.Photo,
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            S3Key = s3Key,
            S3KeyFull = s3KeyFull,
            S3KeyThumbnail = s3KeyThumbnail,
            FileSizeBytes = 1000,
            OriginalStillFileSizeBytes = 1000,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Status = UploadSessionStatus.Pending
        };

    private sealed class FixedUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => null;
        public bool IsAuthenticated => true;
    }

    private sealed class TrackingS3Storage(bool exists = false) : IS3StorageService
    {
        public List<string> DeletedKeys { get; } = [];

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

        public Task DeleteObjectAsync(string s3Key, CancellationToken cancellationToken = default)
        {
            DeletedKeys.Add(s3Key);
            return Task.CompletedTask;
        }

        public Task<bool> ObjectExistsAsync(string s3Key, CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task<long?> GetObjectSizeBytesAsync(string s3Key, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

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

    private sealed class StubPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhotoDto { Id = photo.Id });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>([]);

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubActivityService : IActivityService
    {
        public Task<ServiceResult<ApiDataResponseDto<ActivityAlbumListPageDataDto>>> ListAsync(
            string? status = null,
            string? excludeStatus = null,
            int? limit = null,
            string? cursor = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ApiDataResponseDto<ActivityAlbumListDataDto>>> GetInProgressAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ApiDataResponseDto<ActivityAlbumDto>>> GetByIdAsync(
            string activityId,
            Guid? creatorUserId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ActivityAlbumDto>> CreateAsync(
            UpsertActivityAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ActivityAlbumDto>> UpdateAsync(
            string activityId,
            UpsertActivityAlbumRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult> DeleteAsync(string activityId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>> GetActiveTodayAsync(
            DateOnly? date,
            int? limit = null,
            int? photoLimit = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>> GetActivityPhotosAsync(
            string activityId,
            int? limit = null,
            Guid? creatorUserId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult> AttachPhotosAsync(
            string activityId,
            ActivityAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>> ReplaceActivityPhotosAsync(
            string activityId,
            ActivityAlbumPhotosRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task LinkPhotoAfterUploadAsync(
            Guid activityAlbumId,
            Guid photoId,
            Guid familyId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StageActivityPhotoLinkAsync(
            Guid activityAlbumId,
            Guid photoId,
            Guid familyId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
