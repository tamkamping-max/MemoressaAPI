using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class AudienceConnectionTests
{
    [Fact]
    public async Task GetFriends_StatusAccepted_OmitsPendingRows()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.Friends.Add(new Friend { OwnerUserId = userA, FriendUserId = userB, Name = "B" });
        db.FriendInvites.Add(new FriendInvite
        {
            InviterUserId = userA,
            InviteeUserId = userB,
            InviteeEmail = "b@test.com",
            Status = FriendInviteStatus.Pending
        });
        await db.SaveChangesAsync();

        var all = await CreateFriendService(db, userA).GetFriendsAsync();
        Assert.Equal(2, all.Data!.Count);

        var acceptedOnly = await CreateFriendService(db, userA).GetFriendsAsync("accepted");
        var row = Assert.Single(acceptedOnly.Data!);
        Assert.Equal("accepted", row.ConnectionStatus);
    }

    [Fact]
    public async Task GetFamilyMembers_ConnectionStatusAccepted_OmitsPendingOutgoing()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyOwner(db, inviterId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = inviteeId,
            Email = "invitee@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.FamilyMembers.AddRange(
            new FamilyMember
            {
                FamilyId = familyId,
                Name = "OnTree",
                AssignedToTree = true,
                Generation = Generation.Self
            },
            new FamilyMember
            {
                FamilyId = familyId,
                Name = "Unassigned",
                AssignedToTree = false,
                Generation = Generation.Self
            });
        db.FamilyMemberInvites.Add(new FamilyMemberInvite
        {
            FamilyId = familyId,
            InviterUserId = inviterId,
            InviteeUserId = inviteeId,
            InviteeEmail = "invitee@test.com",
            Status = FriendInviteStatus.Pending
        });
        await db.SaveChangesAsync();

        var service = CreateFamilyService(db, inviterId, familyId);
        Assert.Equal(3, (await service.GetMembersAsync()).Data!.Count);

        var accepted = await service.GetMembersAsync("accepted");
        Assert.Equal(2, accepted.Data!.Count);
        Assert.DoesNotContain(accepted.Data, m => m.ConnectionStatus == "pending_outgoing");
        Assert.Contains(accepted.Data, m => m.Name == "Unassigned" && !m.AssignedToTree);
    }

    [Fact]
    public async Task ValidateFriendIds_PendingInviteId_ReturnsAudienceNotConnected()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        var invite = new FriendInvite
        {
            InviterUserId = userA,
            InviteeUserId = userB,
            InviteeEmail = "b@test.com",
            Status = FriendInviteStatus.Pending
        };
        db.FriendInvites.Add(invite);
        await db.SaveChangesAsync();

        var failure = await AudienceConnectionValidation.ValidateFriendIdsAsync(
            db,
            userA,
            [invite.Id],
            CancellationToken.None);

        Assert.NotNull(failure);
        Assert.Equal(400, failure!.StatusCode);
        Assert.Equal(AudienceConnectionValidation.NotConnectedCode, failure.ErrorCode);
    }

    [Fact]
    public async Task ValidateFamilyMemberIds_PendingInviteId_ReturnsAudienceNotConnected()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();

        await using var db = CreateDb();
        var invite = new FamilyMemberInvite
        {
            FamilyId = familyId,
            InviterUserId = inviterId,
            InviteeEmail = "x@test.com",
            Status = FriendInviteStatus.Pending
        };
        db.FamilyMemberInvites.Add(invite);
        await db.SaveChangesAsync();

        var failure = await AudienceConnectionValidation.ValidateFamilyMemberIdsAsync(
            db,
            familyId,
            [invite.Id],
            CancellationToken.None);

        Assert.NotNull(failure);
        Assert.Equal(AudienceConnectionValidation.NotConnectedCode, failure!.ErrorCode);
    }

    private static void SeedFamilyOwner(MemoressaDbContext db, Guid ownerId, Guid familyId)
    {
        db.UserAccounts.Add(new UserAccount
        {
            Id = ownerId,
            Email = "owner@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = ownerId });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = ownerId, Role = "owner" });
    }

    private static FriendService CreateFriendService(MemoressaDbContext db, Guid userId) =>
        new(db, new FixedUser(userId, null), new StubAvatarUrlResolver());

    private static FamilyService CreateFamilyService(MemoressaDbContext db, Guid userId, Guid familyId) =>
        new(db, new FixedUser(userId, familyId), new StubAvatarUrlResolver());

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class FixedUser(Guid userId, Guid? familyId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => familyId;
        public bool IsAuthenticated => true;
    }

    private sealed class StubAvatarUrlResolver : IAvatarUrlResolver
    {
        public Task<string?> ResolveForResponseAsync(string? storedValue, CancellationToken cancellationToken = default) =>
            Task.FromResult(storedValue);

        public Task<UserDto> ToUserDtoAsync(UserAccount user, CancellationToken cancellationToken = default) =>
            Task.FromResult(user.ToDto());

        public Task<FamilyMemberDto> ToFamilyMemberDtoAsync(
            FamilyMember member,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(member.ToDto());
    }
}
