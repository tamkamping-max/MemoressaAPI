using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FamilyMemberDeleteTests
{
    [Fact]
    public async Task DeleteMember_RemovesAcceptedRow_FromRoster()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyOwner(db, ownerId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = linkedId,
            Email = "linked@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        var member = new FamilyMember
        {
            FamilyId = familyId,
            Name = "Linked",
            LinkedUserId = linkedId,
            AssignedToTree = true,
            Generation = Generation.Self
        };
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();

        var service = CreateService(db, ownerId, familyId);
        var deleted = await service.DeleteMemberAsync(member.Id);
        Assert.True(deleted.Success);
        Assert.Equal(204, deleted.StatusCode);

        Assert.Empty(await db.FamilyMembers.AsNoTracking().Where(m => m.FamilyId == familyId).ToListAsync());
        var roster = await service.GetMembersAsync();
        Assert.Empty(roster.Data!);
    }

    [Fact]
    public async Task DeleteMember_Returns404_WhenIdIsLinkedUserId()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var linkedId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyOwner(db, ownerId, familyId);
        db.FamilyMembers.Add(new FamilyMember
        {
            FamilyId = familyId,
            Name = "Linked",
            LinkedUserId = linkedId,
            AssignedToTree = true,
            Generation = Generation.Self
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db, ownerId, familyId).DeleteMemberAsync(linkedId);
        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeleteMember_Returns403_WhenCancelingSomeoneElsesPendingInvite()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var otherMemberId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyOwner(db, inviterId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = otherMemberId,
            Email = "other@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.UserAccounts.Add(new UserAccount
        {
            Id = inviteeId,
            Email = "invitee@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = familyId,
            UserId = otherMemberId,
            Role = "member"
        });

        var invite = new FamilyMemberInvite
        {
            FamilyId = familyId,
            InviterUserId = inviterId,
            InviteeUserId = inviteeId,
            InviteeEmail = "invitee@test.com",
            Status = FriendInviteStatus.Pending
        };
        db.FamilyMemberInvites.Add(invite);
        await db.SaveChangesAsync();

        var result = await CreateService(db, otherMemberId, familyId).DeleteMemberAsync(invite.Id);
        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
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

    private static FamilyService CreateService(MemoressaDbContext db, Guid userId, Guid familyId) =>
        new(db, new FixedUser(userId, familyId), new StubAvatarUrlResolver());

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
