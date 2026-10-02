using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivityCrossFamilyParticipantTests
{
    [Fact]
    public async Task FriendParticipant_SeesCrossFamilyInProgressActivity_AndCanReadPhotos_NotUpdate()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.Families.AddRange(
            new Family { Id = familyA, OwnerUserId = userA, Name = "A" },
            new Family { Id = familyB, OwnerUserId = userB, Name = "B" });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyA, UserId = userA, Role = "owner" },
            new FamilyMembership { FamilyId = familyB, UserId = userB, Role = "owner" });

        var friend = new Friend
        {
            OwnerUserId = userA,
            Name = "B",
            FriendUserId = userB
        };
        db.Friends.Add(friend);

        var activity = new ActivityAlbum
        {
            FamilyId = familyA,
            ExternalId = "act_cross_friend",
            Title = "Shared trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            EndDate = today.AddDays(2),
            CreatorUserId = userA,
            PrivacyScope = UploadPrivacyScope.Family
        };
        db.ActivityAlbums.Add(activity);
        db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
        {
            ActivityAlbumId = activity.Id,
            FriendId = friend.Id,
            FriendReference = friend.Id.ToString()
        });

        var photoId = Guid.NewGuid();
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyA,
            UploadedByUserId = userA,
            S3Key = "trip.jpg",
            PrivacyScope = UploadPrivacyScope.Family
        });
        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
        {
            ActivityAlbumId = activity.Id,
            PhotoId = photoId,
            SortOrder = 0
        });
        await db.SaveChangesAsync();

        var serviceB = new ActivityService(db, new FixedUser(userB, familyB), new StubPhotoUrlResolver());

        var inProgress = await serviceB.GetInProgressAsync();
        Assert.True(inProgress.Success);
        Assert.Contains(inProgress.Data!.Data.Items, i => i.Id == activity.ExternalId);

        var activeToday = await serviceB.GetActiveTodayAsync(today);
        Assert.True(activeToday.Success);
        Assert.Contains(activeToday.Data!.Data.Items, i => i.Activity.Id == activity.ExternalId);

        var list = await serviceB.ListAsync(excludeStatus: "completed,cancelled");
        Assert.True(list.Success);
        Assert.Contains(list.Data!.Data.Items, i => i.Id == activity.ExternalId);

        var photos = await serviceB.GetActivityPhotosAsync(activity.ExternalId);
        Assert.True(photos.Success);
        Assert.Single(photos.Data!.Data.Items);

        var updateDenied = await serviceB.UpdateAsync(
            activity.ExternalId,
            new UpsertActivityAlbumRequestDto
            {
                Title = "Hacked",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.InProgress,
                StartDate = today
            });
        Assert.Equal(403, updateDenied.StatusCode);

        var replaceDenied = await serviceB.ReplaceActivityPhotosAsync(
            activity.ExternalId,
            new ActivityAlbumPhotosRequestDto { PhotoIds = [photoId.ToString()] });
        Assert.Equal(403, replaceDenied.StatusCode);
    }

    [Fact]
    public async Task FriendParticipant_CanLinkOwnUploadedPhotos()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.Families.AddRange(
            new Family { Id = familyA, OwnerUserId = userA, Name = "A" },
            new Family { Id = familyB, OwnerUserId = userB, Name = "B" });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyA, UserId = userA, Role = "owner" },
            new FamilyMembership { FamilyId = familyB, UserId = userB, Role = "owner" });

        var friend = new Friend { OwnerUserId = userA, Name = "B", FriendUserId = userB };
        db.Friends.Add(friend);

        var activity = new ActivityAlbum
        {
            FamilyId = familyA,
            ExternalId = "act_link",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userA,
            PrivacyScope = UploadPrivacyScope.Family
        };
        db.ActivityAlbums.Add(activity);
        db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
        {
            ActivityAlbumId = activity.Id,
            FriendId = friend.Id,
            FriendReference = friend.Id.ToString()
        });

        var bPhoto = Guid.NewGuid();
        db.Photos.Add(new Photo
        {
            Id = bPhoto,
            FamilyId = familyB,
            UploadedByUserId = userB,
            S3Key = "b.jpg",
            PrivacyScope = UploadPrivacyScope.Family
        });
        await db.SaveChangesAsync();

        var serviceB = new ActivityService(db, new FixedUser(userB, familyB), new StubPhotoUrlResolver());
        var link = await serviceB.AttachPhotosAsync(
            activity.ExternalId,
            new ActivityAlbumPhotosRequestDto { PhotoIds = [$"photo_{bPhoto:D}"] });

        Assert.True(link.Success);
        Assert.True(await db.ActivityAlbumPhotos.AnyAsync(ap =>
            ap.ActivityAlbumId == activity.Id && ap.PhotoId == bPhoto));
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
            Task.FromResult(new PhotoDto { Id = photo.Id, RemoteUrl = "https://example/x.jpg" });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default)
        {
            var list = photos.Select(p => new PhotoDto { Id = p.Id, RemoteUrl = "https://example/x.jpg" })
                .ToList();
            return Task.FromResult<IReadOnlyList<PhotoDto>>(list);
        }

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("https://example/x.jpg");
    }
}
