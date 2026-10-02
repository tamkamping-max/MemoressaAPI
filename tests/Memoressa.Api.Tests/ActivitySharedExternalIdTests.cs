using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivitySharedExternalIdTests
{
    private const string SharedExternalId = "act_same_external";

    [Fact]
    public async Task GetActiveTodayAsync_ReturnsDistinctItems_WhenExternalIdCollides()
    {
        var userM = Guid.NewGuid();
        var userF = Guid.NewGuid();
        var familyM = Guid.NewGuid();
        var familyF = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var db = CreateDb();
        SeedUsers(db, userM, userF, familyM, familyF);

        var friend = new Friend { OwnerUserId = userF, Name = "M", FriendUserId = userM };
        db.Friends.Add(friend);

        var ownTrip = new ActivityAlbum
        {
            FamilyId = familyM,
            ExternalId = SharedExternalId,
            Title = "台北行",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userM
        };
        var friendTrip = new ActivityAlbum
        {
            FamilyId = familyF,
            ExternalId = SharedExternalId,
            Title = "大阪行",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userF
        };
        db.ActivityAlbums.AddRange(ownTrip, friendTrip);
        db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
        {
            ActivityAlbumId = friendTrip.Id,
            FriendId = friend.Id,
            FriendReference = friend.Id.ToString()
        });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userM, familyM), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());
        var result = await service.GetActiveTodayAsync(today);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Data.Items.Count);

        var ownCard = result.Data.Data.Items.Single(i => i.Activity.CreatorUserId == userM);
        var friendCard = result.Data.Data.Items.Single(i => i.Activity.CreatorUserId == userF);

        Assert.Equal("台北行", ownCard.Activity.Title);
        Assert.Equal(ActivityAlbumType.Travel, ownCard.Activity.Type);
        Assert.True(ownCard.Activity.ViewerIsCreator);
        Assert.Equal(SharedExternalId, ownCard.Activity.ExternalId);

        Assert.Equal("大阪行", friendCard.Activity.Title);
        Assert.Equal(ActivityAlbumType.Travel, friendCard.Activity.Type);
        Assert.False(friendCard.Activity.ViewerIsCreator);
        Assert.True(friendCard.Activity.ViewerIsParticipant);
    }

    [Fact]
    public async Task GetActivityPhotosAsync_UsesCreatorUserId_ToDisambiguate()
    {
        var userM = Guid.NewGuid();
        var userF = Guid.NewGuid();
        var familyM = Guid.NewGuid();
        var familyF = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await using var db = CreateDb();
        SeedUsers(db, userM, userF, familyM, familyF);

        var friend = new Friend { OwnerUserId = userF, Name = "M", FriendUserId = userM };
        db.Friends.Add(friend);

        var ownTrip = new ActivityAlbum
        {
            FamilyId = familyM,
            ExternalId = SharedExternalId,
            Title = "Mine",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userM
        };
        var friendTrip = new ActivityAlbum
        {
            FamilyId = familyF,
            ExternalId = SharedExternalId,
            Title = "Friend",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = today,
            CreatorUserId = userF
        };
        db.ActivityAlbums.AddRange(ownTrip, friendTrip);
        db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
        {
            ActivityAlbumId = friendTrip.Id,
            FriendId = friend.Id,
            FriendReference = friend.Id.ToString()
        });

        var ownPhoto = Guid.NewGuid();
        var friendPhoto = Guid.NewGuid();
        db.Photos.AddRange(
            new Photo { Id = ownPhoto, FamilyId = familyM, UploadedByUserId = userM, S3Key = "m.jpg" },
            new Photo { Id = friendPhoto, FamilyId = familyF, UploadedByUserId = userF, S3Key = "f.jpg" });
        db.ActivityAlbumPhotos.AddRange(
            new ActivityAlbumPhoto { ActivityAlbumId = ownTrip.Id, PhotoId = ownPhoto, SortOrder = 0 },
            new ActivityAlbumPhoto { ActivityAlbumId = friendTrip.Id, PhotoId = friendPhoto, SortOrder = 0 });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(userM, familyM), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());

        var ownPhotos = await service.GetActivityPhotosAsync(SharedExternalId, creatorUserId: userM);
        Assert.True(ownPhotos.Success);
        Assert.Equal(ownPhoto, Assert.Single(ownPhotos.Data!.Data.Items).Id);

        var friendPhotos = await service.GetActivityPhotosAsync(SharedExternalId, creatorUserId: userF);
        Assert.True(friendPhotos.Success);
        Assert.Equal(friendPhoto, Assert.Single(friendPhotos.Data!.Data.Items).Id);

        var defaultOwn = await service.GetActivityPhotosAsync(SharedExternalId);
        Assert.True(defaultOwn.Success);
        Assert.Equal(ownPhoto, Assert.Single(defaultOwn.Data!.Data.Items).Id);
    }

    private static void SeedUsers(MemoressaDbContext db, Guid userM, Guid userF, Guid familyM, Guid familyF)
    {
        db.UserAccounts.AddRange(
            new UserAccount { Id = userM, Email = "m@test.com", PasswordHash = "x", IsActive = true, Nickname = "M" },
            new UserAccount { Id = userF, Email = "f@test.com", PasswordHash = "x", IsActive = true, Nickname = "F" });
        db.Families.AddRange(
            new Family { Id = familyM, OwnerUserId = userM, Name = "M" },
            new Family { Id = familyF, OwnerUserId = userF, Name = "F" });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyM, UserId = userM, Role = "owner" },
            new FamilyMembership { FamilyId = familyF, UserId = userF, Role = "owner" });
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

}
