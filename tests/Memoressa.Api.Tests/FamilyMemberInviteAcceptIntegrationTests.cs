using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FamilyMemberInviteAcceptIntegrationTests
{
    [Fact]
    public async Task AcceptInvite_ResponseAndGetMembers_UseInviteeHomeFamily_WithInviterMirrorRow()
    {
        var inviterFamilyId = Guid.NewGuid();
        var inviteeFamilyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", inviterFamilyId, "Inviter");
        SeedUser(db, inviteeId, "invitee@test.com", inviteeFamilyId, "Invitee");
        await db.SaveChangesAsync();

        var inviterService = CreateService(db, inviterId, inviterFamilyId);
        var inviteId = (await inviterService.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" })).Data!.Id;

        var inviteeService = CreateService(db, inviteeId, inviteeFamilyId);
        var accepted = await inviteeService.AcceptFamilyMemberInviteAsync(inviteId);

        Assert.True(accepted.Success);
        Assert.Equal("accepted", accepted.Data!.ConnectionStatus);
        Assert.False(accepted.Data.AssignedToTree);
        Assert.Equal(inviterId, accepted.Data.LinkedUserId);
        Assert.NotEqual(inviteeId, accepted.Data.LinkedUserId);

        var mirrorRow = await db.FamilyMembers.AsNoTracking()
            .SingleAsync(m => m.FamilyId == inviteeFamilyId && m.LinkedUserId == inviterId);
        Assert.Equal(accepted.Data.Id, mirrorRow.Id);
        Assert.Equal(accepted.Data.FamilyMemberId, mirrorRow.Id);
        Assert.False(mirrorRow.AssignedToTree);

        var inviterSideRow = await db.FamilyMembers.AsNoTracking()
            .SingleAsync(m => m.FamilyId == inviterFamilyId && m.LinkedUserId == inviteeId);
        Assert.False(inviterSideRow.AssignedToTree);

        var roster = await inviteeService.GetMembersAsync();
        Assert.Contains(
            roster.Data!,
            m => m.LinkedUserId == inviterId && !m.AssignedToTree && m.ConnectionStatus == "accepted");
    }

    [Fact]
    public async Task AcceptInvite_BootstrapsHomeFamily_WhenInviteeHasNoMembership()
    {
        var inviterFamilyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedUser(db, inviterId, "inviter@test.com", inviterFamilyId, "Inviter");
        db.UserAccounts.Add(new UserAccount
        {
            Id = inviteeId,
            Email = "orphan@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var inviterService = CreateService(db, inviterId, inviterFamilyId);
        var inviteId = (await inviterService.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "orphan@test.com" })).Data!.Id;

        var inviteeService = CreateService(db, inviteeId, familyId: null);
        var accepted = await inviteeService.AcceptFamilyMemberInviteAsync(inviteId);
        Assert.True(accepted.Success);
        Assert.Equal(inviterId, accepted.Data!.LinkedUserId);

        var membership = await db.FamilyMemberships.AsNoTracking()
            .SingleAsync(m => m.UserId == inviteeId);
        var roster = CreateService(db, inviteeId, membership.FamilyId).GetMembersAsync();
        Assert.Contains(
            (await roster).Data!,
            m => m.LinkedUserId == inviterId && !m.AssignedToTree);
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

    private static FamilyService CreateService(MemoressaDbContext db, Guid userId, Guid? familyId) =>
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
