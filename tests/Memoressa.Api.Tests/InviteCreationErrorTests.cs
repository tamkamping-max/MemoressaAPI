using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class InviteCreationErrorTests
{
    [Fact]
    public async Task CreateFriendInvite_UnregisteredEmail_Returns404WithCode()
    {
        var userA = Guid.NewGuid();
        await using var db = CreateDb();
        db.UserAccounts.Add(new UserAccount
        {
            Id = userA,
            Email = "a@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var result = await CreateFriendService(db, userA).CreateFriendInviteAsync(
            new CreateFriendInviteRequestDto { Email = "missing@test.com" });

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(InviteErrorCodes.EmailNotRegistered, result.ErrorCode);
    }

    [Fact]
    public async Task CreateFriendInvite_AlreadyFriends_Returns409WithCode()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.Friends.Add(new Friend { OwnerUserId = userA, FriendUserId = userB, Name = "B" });
        await db.SaveChangesAsync();

        var result = await CreateFriendService(db, userA).CreateFriendInviteAsync(
            new CreateFriendInviteRequestDto { Email = "b@test.com" });

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(InviteErrorCodes.FriendAlreadyConnected, result.ErrorCode);
    }

    [Fact]
    public async Task CreateFriendInvite_PendingIncoming_Returns409WithCode()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = userA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = userB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.FriendInvites.Add(new FriendInvite
        {
            InviterUserId = userB,
            InviteeUserId = userA,
            InviteeEmail = "a@test.com",
            Status = FriendInviteStatus.Pending
        });
        await db.SaveChangesAsync();

        var result = await CreateFriendService(db, userA).CreateFriendInviteAsync(
            new CreateFriendInviteRequestDto { Email = "b@test.com" });

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(InviteErrorCodes.FriendAlreadyConnected, result.ErrorCode);
    }

    [Fact]
    public async Task CreateFamilyInvite_AlreadyMember_Returns409WithCode()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, ownerId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = linkedId,
            Email = "linked@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = familyId,
            Name = "Linked",
            LinkedUserId = linkedId,
            AssignedToTree = false,
            Generation = Generation.Self
        });
        await db.SaveChangesAsync();

        var result = await CreateFamilyService(db, ownerId, familyId).CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "linked@test.com" });

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(InviteErrorCodes.FamilyAlreadyConnected, result.ErrorCode);
    }

    [Fact]
    public async Task CreateFamilyInvite_CannotInviteSelf_Returns400WithCode()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await using var db = CreateDb();
        SeedFamily(db, ownerId, familyId);
        await db.SaveChangesAsync();

        var result = await CreateFamilyService(db, ownerId, familyId).CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "owner@test.com" });

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(InviteErrorCodes.CannotInviteSelf, result.ErrorCode);
    }

    private static void SeedFamily(MemoressaDbContext db, Guid ownerId, Guid familyId)
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
        new(db, new FixedUser(userId), new StubAvatarUrlResolver());

    private static FamilyService CreateFamilyService(MemoressaDbContext db, Guid userId, Guid familyId) =>
        new(db, new FixedUser(userId, familyId), new StubAvatarUrlResolver());

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new MemoressaDbContext(options);
    }

    private sealed class FixedUser(Guid userId, Guid? familyId = null) : ICurrentUserService
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
