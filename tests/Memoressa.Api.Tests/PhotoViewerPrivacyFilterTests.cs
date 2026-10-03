using Memoressa.Application.Abstractions;
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

public class PhotoViewerPrivacyFilterTests
{
    [Fact]
    public async Task FamilyPhoto_VisibleToSameFamilyMember_NotOutsider()
    {
        var familyId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, familyId, userA, userB);
        db.UserAccounts.Add(new UserAccount { Id = outsider, Email = "out@test.com", PasswordHash = "x", IsActive = true });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userA,
            S3Key = "a.jpg",
            ContentType = "image/jpeg",
            PrivacyScope = UploadPrivacyScope.Family
        });
        await db.SaveChangesAsync();

        Assert.True(await PhotoViewerAccess.CanViewAsync(db, photoId, userB, CancellationToken.None));
        Assert.False(await PhotoViewerAccess.CanViewAsync(db, photoId, outsider, CancellationToken.None));
    }

    [Fact]
    public async Task Timeline_ExcludesOnlySelfPhotos_FromOtherUploaders()
    {
        var familyId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var privatePhoto = Guid.NewGuid();
        var sharedPhoto = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, familyId, userA, userB);
        db.Photos.AddRange(
            new Photo
            {
                Id = privatePhoto,
                FamilyId = familyId,
                UploadedByUserId = userA,
                S3Key = "p.jpg",
                ContentType = "image/jpeg",
                PrivacyScope = UploadPrivacyScope.OnlySelf,
                TakenAt = DateTime.UtcNow
            },
            new Photo
            {
                Id = sharedPhoto,
                FamilyId = familyId,
                UploadedByUserId = userA,
                S3Key = "f.jpg",
                ContentType = "image/jpeg",
                PrivacyScope = UploadPrivacyScope.Family,
                TakenAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await db.SaveChangesAsync();

        var service = CreatePhotoService(db, userB, familyId);
        var timeline = await service.GetTimelinePhotosAsync(limit: 20);
        Assert.True(timeline.Success);
        Assert.DoesNotContain(timeline.Data!.Items, p => p.Id == privatePhoto);
        Assert.Contains(timeline.Data.Items, p => p.Id == sharedPhoto);
    }

    [Fact]
    public async Task OnlySelf_LinkedToActivity_RequiresActivityParticipantsVisible()
    {
        var familyId = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var participant = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, familyId, uploader, participant);
        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = "act_x",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            CreatorUserId = uploader,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PrivacyScope = UploadPrivacyScope.Family
        });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = uploader,
            S3Key = "x.jpg",
            ContentType = "image/jpeg",
            PrivacyScope = UploadPrivacyScope.OnlySelf,
            ActivityParticipantsVisible = false
        });
        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto { ActivityAlbumId = activityId, PhotoId = photoId });
        await db.SaveChangesAsync();

        Assert.False(await PhotoViewerAccess.CanViewAsync(db, photoId, participant, CancellationToken.None));

        var photo = await db.Photos.FirstAsync(p => p.Id == photoId);
        photo.ActivityParticipantsVisible = true;
        await db.SaveChangesAsync();

        Assert.True(await PhotoViewerAccess.CanViewAsync(db, photoId, participant, CancellationToken.None));
    }

    [Fact]
    public async Task ActivityPhotoCount_ReflectsViewerVisibleLinks()
    {
        var familyId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, familyId, userA, userB);
        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = "act_count",
            Title = "Party",
            Type = ActivityAlbumType.Gathering,
            CreatorUserId = userA,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = ActivityAlbumStatus.InProgress
        });

        var visibleId = Guid.NewGuid();
        var hiddenId = Guid.NewGuid();
        db.Photos.AddRange(
            new Photo
            {
                Id = visibleId,
                FamilyId = familyId,
                UploadedByUserId = userA,
                S3Key = "v.jpg",
                ContentType = "image/jpeg",
                PrivacyScope = UploadPrivacyScope.Family
            },
            new Photo
            {
                Id = hiddenId,
                FamilyId = familyId,
                UploadedByUserId = userA,
                S3Key = "h.jpg",
                ContentType = "image/jpeg",
                PrivacyScope = UploadPrivacyScope.OnlySelf
            });
        db.ActivityAlbumPhotos.AddRange(
            new ActivityAlbumPhoto { ActivityAlbumId = activityId, PhotoId = visibleId, SortOrder = 1 },
            new ActivityAlbumPhoto { ActivityAlbumId = activityId, PhotoId = hiddenId, SortOrder = 0 });
        await db.SaveChangesAsync();

        var snapshots = await ActivityAlbumPhotoFeedHelper.LoadViewerSnapshotsAsync(
            db,
            userB,
            [activityId],
            previewLimit: 12,
            CancellationToken.None);

        var snap = snapshots[activityId];
        Assert.Equal(1, snap.PhotoCount);
        Assert.Equal([visibleId], snap.PreviewPhotoIds);
    }

    private static void SeedFamily(MemoressaDbContext db, Guid familyId, params Guid[] userIds)
    {
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = userIds[0] });
        foreach (var userId in userIds)
        {
            db.UserAccounts.Add(new UserAccount
            {
                Id = userId,
                Email = $"{userId:N}@test.com",
                PasswordHash = "x",
                IsActive = true
            });
            db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId });
        }
    }

    private static PhotoService CreatePhotoService(MemoressaDbContext db, Guid userId, Guid familyId) =>
        new(
            db,
            new FixedUser(userId, familyId),
            new StubPhotoUrlResolver(),
            new StubS3Storage(),
            Options.Create(new MediaStorageSettings()),
            new StubPhotoAlbumService());

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

    private sealed class StubPhotoUrlResolver : IPhotoUrlResolver
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
            Task.FromResult<string?>(null);
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
