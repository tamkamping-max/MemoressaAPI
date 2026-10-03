using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FriendDeleteTests
{
    [Fact]
    public async Task DeleteFriend_RemovesBothSides_OfAcceptedConnection()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });

        var rowA = new Friend { OwnerUserId = userA, FriendUserId = userB, Name = "B" };
        var rowB = new Friend { OwnerUserId = userB, FriendUserId = userA, Name = "A" };
        db.Friends.AddRange(rowA, rowB);
        await db.SaveChangesAsync();

        var serviceA = CreateService(db, userA);
        var deleted = await serviceA.DeleteFriendAsync(rowA.Id);
        Assert.True(deleted.Success);
        Assert.Equal(204, deleted.StatusCode);

        Assert.Empty(await db.Friends.AsNoTracking().ToListAsync());

        var listB = await CreateService(db, userB).GetFriendsAsync();
        Assert.Empty(listB.Data!);
    }

    [Fact]
    public async Task DeleteFriend_ByInviteId_CancelsPendingOutgoing()
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

        var serviceA = CreateService(db, userA);
        var deleted = await serviceA.DeleteFriendAsync(invite.Id);
        Assert.True(deleted.Success);
        Assert.Equal(204, deleted.StatusCode);

        var friends = await serviceA.GetFriendsAsync();
        Assert.Empty(friends.Data!);

        var invitesB = await CreateService(db, userB).GetFriendInvitesAsync();
        Assert.Empty(invitesB.Data!);
    }

    [Fact]
    public async Task DeleteFriend_Returns404_WhenIdIsCounterpartyUserId()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.Friends.Add(new Friend { OwnerUserId = userA, FriendUserId = userB, Name = "B" });
        await db.SaveChangesAsync();

        var result = await CreateService(db, userA).DeleteFriendAsync(userB);
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    private static FriendService CreateService(MemoressaDbContext db, Guid userId) =>
        new(db, new FixedUser(userId), new StubAvatarUrlResolver());

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class FixedUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public Guid? FamilyId => null;
        public bool IsAuthenticated => true;
    }

    private sealed class StubAvatarUrlResolver : IAvatarUrlResolver
    {
        public Task<string?> ResolveForResponseAsync(string? storedValue, CancellationToken cancellationToken = default) =>
            Task.FromResult(storedValue);

        public Task<Memoressa.Application.DTOs.UserDto> ToUserDtoAsync(
            UserAccount user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(user.ToDto());

        public Task<Memoressa.Application.DTOs.FamilyMemberDto> ToFamilyMemberDtoAsync(
            FamilyMember member,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(member.ToDto());
    }
}
