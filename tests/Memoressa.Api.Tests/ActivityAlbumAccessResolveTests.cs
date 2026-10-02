using Memoressa.Application.Common;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivityAlbumAccessResolveTests
{
    [Fact]
    public async Task ResolveAccessibleAsync_PicksParticipantActivity_WhenExternalIdCollides()
    {
        const string sharedExternalId = "act_duplicate";

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();

        await using var db = CreateDb();

        var friend = new Friend { OwnerUserId = userA, Name = "B", FriendUserId = userB };
        db.Friends.Add(friend);

        var activityA = new ActivityAlbum
        {
            FamilyId = familyA,
            ExternalId = sharedExternalId,
            Title = "A trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userA
        };
        var activityB = new ActivityAlbum
        {
            FamilyId = familyB,
            ExternalId = sharedExternalId,
            Title = "B invited",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userA
        };
        db.ActivityAlbums.AddRange(activityA, activityB);
        db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
        {
            ActivityAlbumId = activityB.Id,
            FriendId = friend.Id,
            FriendReference = friend.Id.ToString()
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var resolved = await ActivityAlbumAccess.ResolveAccessibleAsync(
            db,
            userB,
            sharedExternalId,
            CancellationToken.None);

        Assert.NotNull(resolved.Activity);
        Assert.Equal(activityB.Id, resolved.Activity!.Id);
    }

    [Fact]
    public async Task ResolveAccessibleAsync_PrefersCreatorOwned_WhenUserCanAccessBoth()
    {
        const string sharedExternalId = "act_creator_pick";

        var userA = Guid.NewGuid();
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();

        await using var db = CreateDb();

        var owned = new ActivityAlbum
        {
            FamilyId = familyA,
            ExternalId = sharedExternalId,
            Title = "Mine",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userA,
            CreatedAt = DateTime.UtcNow
        };
        var otherFamily = new ActivityAlbum
        {
            FamilyId = familyB,
            ExternalId = sharedExternalId,
            Title = "Other family",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userA,
            PrivacyScope = UploadPrivacyScope.Family,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.ActivityAlbums.AddRange(owned, otherFamily);
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyA, UserId = userA, Role = "owner" },
            new FamilyMembership { FamilyId = familyB, UserId = userA, Role = "owner" });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var resolved = await ActivityAlbumAccess.ResolveAccessibleAsync(
            db,
            userA,
            sharedExternalId,
            CancellationToken.None);

        Assert.NotNull(resolved.Activity);
        Assert.Equal(owned.Id, resolved.Activity!.Id);
    }

    [Fact]
    public async Task ResolveAccessibleAsync_ResolvesByInternalGuid()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        var activity = new ActivityAlbum
        {
            FamilyId = familyId,
            ExternalId = "act_guid",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userId
        };
        db.ActivityAlbums.Add(activity);
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var resolved = await ActivityAlbumAccess.ResolveAccessibleAsync(
            db,
            userId,
            activity.Id.ToString(),
            CancellationToken.None);

        Assert.NotNull(resolved.Activity);
        Assert.Equal(activity.Id, resolved.Activity!.Id);
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MemoressaDbContext(options);
    }
}
