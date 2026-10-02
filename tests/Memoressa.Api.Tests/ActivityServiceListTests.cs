using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivityServiceListTests
{
    [Fact]
    public async Task ListAsync_ExcludeInProgress_ReturnsCompletedWithCreatedAt()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        db.ActivityAlbums.AddRange(
            new ActivityAlbum
            {
                FamilyId = familyId,
                ExternalId = "act_live",
                Title = "Live",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.InProgress,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatorUserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new ActivityAlbum
            {
                FamilyId = familyId,
                ExternalId = "act_done",
                Title = "Done",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.Completed,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
                CreatorUserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userId, familyId), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var result = await service.ListAsync(excludeStatus: "inProgress");

        Assert.True(result.Success);
        Assert.Single(result.Data!.Data.Items);
        Assert.Equal("act_done", result.Data.Data.Items[0].Id);
        Assert.True(result.Data.Data.Items[0].CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task ReplaceActivityPhotosAsync_ReplacesLinksAndPreservesOrder()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var photoA = Guid.NewGuid();
        var photoB = Guid.NewGuid();
        var photoC = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        var activity = new ActivityAlbum
        {
            FamilyId = familyId,
            ExternalId = "act_replace",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userId
        };
        db.ActivityAlbums.Add(activity);
        foreach (var id in new[] { photoA, photoB, photoC })
        {
            db.Photos.Add(new Photo
            {
                Id = id,
                FamilyId = familyId,
                UploadedByUserId = userId,
                S3Key = $"{id}.jpg"
            });
        }

        db.ActivityAlbumPhotos.AddRange(
            new ActivityAlbumPhoto { ActivityAlbumId = activity.Id, PhotoId = photoA, SortOrder = 0 },
            new ActivityAlbumPhoto { ActivityAlbumId = activity.Id, PhotoId = photoB, SortOrder = 1 });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userId, familyId), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var result = await service.ReplaceActivityPhotosAsync(
            activity.ExternalId,
            new ActivityAlbumPhotosRequestDto
            {
                PhotoIds =
                [
                    $"photo_{photoC:D}",
                    photoB.ToString()
                ]
            });

        Assert.True(result.Success);
        Assert.Equal([photoC, photoB], result.Data!.Data.PhotoIds);

        var links = await db.ActivityAlbumPhotos
            .Where(ap => ap.ActivityAlbumId == activity.Id)
            .OrderBy(ap => ap.SortOrder)
            .Select(ap => ap.PhotoId)
            .ToListAsync();
        Assert.Equal([photoC, photoB], links);
        Assert.DoesNotContain(photoA, links);

        db.UserAccounts.Add(new UserAccount { Id = otherId, Email = "o@test.com", PasswordHash = "x", IsActive = true });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = otherId, Role = "member" });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var asOther = new ActivityService(db, new FixedUser(otherId, familyId), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var denied = await asOther.ReplaceActivityPhotosAsync(
            activity.ExternalId,
            new ActivityAlbumPhotosRequestDto { PhotoIds = [photoB.ToString()] });
        Assert.Equal(403, denied.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidFriendId_Returns400()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userId, familyId), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var result = await service.CreateAsync(new UpsertActivityAlbumRequestDto
        {
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            FriendIds = ["not-a-real-friend"]
        });

        Assert.Equal(400, result.StatusCode);
        Assert.Contains("friendIds", result.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.ActivityAlbums.AnyAsync());
    }

    [Fact]
    public async Task GetActiveTodayAsync_PhotoPreviewsIncludeFullUrl()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        var activity = new ActivityAlbum
        {
            FamilyId = familyId,
            ExternalId = "act_fullurl",
            Title = "Today",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userId
        };
        db.ActivityAlbums.Add(activity);
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "big.jpg"
        });
        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
        {
            ActivityAlbumId = activity.Id,
            PhotoId = photoId,
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var service = new ActivityService(
            db,
            new FixedUser(userId, familyId),
            new FullUrlPhotoUrlResolver("https://cdn/full.jpg"),
            new StubAvatarUrlResolver());

        var result = await service.GetActiveTodayAsync(today);
        Assert.True(result.Success);
        var card = Assert.Single(result.Data!.Data.Items);
        var preview = Assert.Single(card.Photos);
        Assert.Equal("https://cdn/full.jpg", preview.FullUrl);
    }

    [Fact]
    public async Task CreateAsync_WithPhotoIds_LinksPhotos()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount { Id = userId, Email = "u@test.com", PasswordHash = "x", IsActive = true });
        db.Families.Add(new Family { Id = familyId, OwnerUserId = userId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "k.jpg"
        });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userId, familyId), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var created = await service.CreateAsync(new UpsertActivityAlbumRequestDto
        {
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PhotoIds = [photoId]
        });

        Assert.True(created.Success);
        var activityId = await db.ActivityAlbums.Where(a => a.ExternalId == created.Data!.Id).Select(a => a.Id)
            .FirstAsync();
        Assert.True(await db.ActivityAlbumPhotos.AnyAsync(ap =>
            ap.ActivityAlbumId == activityId && ap.PhotoId == photoId));
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

    private sealed class StubPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class FullUrlPhotoUrlResolver(string fullUrl) : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhotoDto { Id = photo.Id, FullUrl = fullUrl, UploadedBy = photo.UploadedByUserId });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default)
        {
            var list = photos
                .Select(p => new PhotoDto { Id = p.Id, FullUrl = fullUrl, UploadedBy = p.UploadedByUserId })
                .ToList();
            return Task.FromResult<IReadOnlyList<PhotoDto>>(list);
        }

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(fullUrl);
    }
}
