using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FamilyMemberInviteTests
{
    [Fact]
    public async Task CreateInvite_ReturnsPendingOutgoing_OnInviterRoster()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", familyId, "Inviter");
        SeedUser(db, inviteeId, "invitee@test.com", Guid.NewGuid(), "Invitee");
        await db.SaveChangesAsync();

        var service = CreateService(db, inviterId, familyId);
        var created = await service.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });
        Assert.True(created.Success);
        Assert.Equal("pending_outgoing", created.Data!.ConnectionStatus);
        Assert.False(created.Data.AssignedToTree);

        var roster = await service.GetMembersAsync();
        Assert.Contains(roster.Data!, m => m.ConnectionStatus == "pending_outgoing" && m.Email == "invitee@test.com");
    }

    [Fact]
    public async Task AcceptInvite_CreatesUnassignedMember_WithAssignedToTreeFalse()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        var inviteeFamilyId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", familyId, "Inviter");
        SeedUser(db, inviteeId, "invitee@test.com", inviteeFamilyId, "Invitee");
        await db.SaveChangesAsync();

        var inviterService = CreateService(db, inviterId, familyId);
        var created = await inviterService.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });
        var inviteId = created.Data!.InviteId!.Value;

        var inviteeService = CreateService(db, inviteeId, inviteeFamilyId);
        var accepted = await inviteeService.AcceptFamilyMemberInviteAsync(inviteId);
        Assert.True(accepted.Success);
        Assert.Equal("accepted", accepted.Data!.ConnectionStatus);
        Assert.False(accepted.Data.AssignedToTree);
        Assert.Equal(inviterId, accepted.Data.LinkedUserId);

        var inviterRoster = await inviterService.GetMembersAsync();
        Assert.Contains(inviterRoster.Data!, m => m.LinkedUserId == inviteeId && !m.AssignedToTree);

        var inviteeRoster = await inviteeService.GetMembersAsync();
        Assert.Contains(
            inviteeRoster.Data!,
            m => m.LinkedUserId == inviterId && !m.AssignedToTree && m.ConnectionStatus == "accepted");
    }

    [Fact]
    public async Task GetInvites_ReturnsPendingIncoming_ForInvitee()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", familyId, "Inviter");
        SeedUser(db, inviteeId, "invitee@test.com", Guid.NewGuid(), "Invitee");
        await db.SaveChangesAsync();

        var inviterService = CreateService(db, inviterId, familyId);
        await inviterService.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });

        var inviteeService = CreateService(db, inviteeId, Guid.NewGuid());
        var inbox = await inviteeService.GetFamilyMemberInvitesAsync();
        Assert.True(inbox.Success);
        var item = Assert.Single(inbox.Data!);
        Assert.Equal("pending_incoming", item.Status);
        Assert.NotNull(item.Inviter);
        Assert.Equal("inviter@test.com", item.Inviter!.Email);
    }

    [Fact]
    public async Task DeletePendingOutgoingInvite_ByInviteId()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", familyId, "Inviter");
        SeedUser(db, inviteeId, "invitee@test.com", Guid.NewGuid(), "Invitee");
        await db.SaveChangesAsync();

        var service = CreateService(db, inviterId, familyId);
        var created = await service.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });
        var inviteId = created.Data!.Id;

        var deleted = await service.DeleteMemberAsync(inviteId);
        Assert.True(deleted.Success);
        Assert.Equal(204, deleted.StatusCode);

        var roster = await service.GetMembersAsync();
        Assert.DoesNotContain(roster.Data!, m => m.Id == inviteId);
    }

    private static void SeedUser(
        MemoressaDbContext db,
        Guid userId,
        string email,
        Guid familyId,
        string nickname)
    {
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            Email = email,
            Nickname = nickname,
            PasswordHash = "x",
            IsActive = true
        });
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = userId });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
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
