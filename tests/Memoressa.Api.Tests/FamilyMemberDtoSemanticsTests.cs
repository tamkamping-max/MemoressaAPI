using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FamilyMemberDtoSemanticsTests
{
    [Fact]
    public async Task GetMembers_EachAcceptedRow_HasDistinctFamilyMemberId_AndLinkedUserId()
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
        var ownerMember = new FamilyMember
        {
            FamilyId = familyId,
            Name = "Owner",
            LinkedUserId = ownerId,
            AssignedToTree = true,
            Generation = Generation.Self
        };
        var linkedMember = new FamilyMember
        {
            FamilyId = familyId,
            Name = "Linked",
            LinkedUserId = linkedId,
            AssignedToTree = true,
            Generation = Generation.Self
        };
        db.FamilyMembers.AddRange(ownerMember, linkedMember);
        await db.SaveChangesAsync();

        var roster = await CreateService(db, ownerId, familyId).GetMembersAsync("accepted");
        Assert.Equal(2, roster.Data!.Count);

        var ownerRow = roster.Data.Single(m => m.LinkedUserId == ownerId);
        var linkedRow = roster.Data.Single(m => m.LinkedUserId == linkedId);

        Assert.Equal(ownerMember.Id, ownerRow.Id);
        Assert.Equal(ownerMember.Id, ownerRow.FamilyMemberId);
        Assert.Equal(linkedMember.Id, linkedRow.Id);
        Assert.Equal(linkedMember.Id, linkedRow.FamilyMemberId);
        Assert.NotEqual(ownerRow.FamilyMemberId, linkedRow.FamilyMemberId);

        Assert.Equal(linkedMember.Id, linkedRow.Counterparty!.FamilyMemberId);
        Assert.Equal(linkedId, linkedRow.Counterparty.Id);
        Assert.Equal(linkedId, linkedRow.LinkedUserId);
    }

    [Fact]
    public async Task AcceptInvite_ReturnsFamilyMemberId_OnAcceptedRow()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, inviterId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = inviteeId,
            Email = "invitee@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var inviterService = CreateService(db, inviterId, familyId);
        var created = await inviterService.CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });
        var inviteId = created.Data!.InviteId!.Value;

        var accepted = await CreateService(db, inviteeId, Guid.NewGuid())
            .AcceptFamilyMemberInviteAsync(inviteId);

        Assert.True(accepted.Success);
        Assert.Equal("accepted", accepted.Data!.ConnectionStatus);
        Assert.NotNull(accepted.Data.FamilyMemberId);
        Assert.Equal(accepted.Data.Id, accepted.Data.FamilyMemberId);
        Assert.Equal(inviterId, accepted.Data.LinkedUserId);
        Assert.Equal(accepted.Data.FamilyMemberId, accepted.Data.Counterparty!.FamilyMemberId);
        Assert.Equal(inviterId, accepted.Data.Counterparty.Id);
        Assert.False(accepted.Data.AssignedToTree);
    }

    [Fact]
    public async Task PendingOutgoing_HasInviteIdAsConnection_WithoutFamilyMemberId()
    {
        var familyId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, inviterId, familyId);
        db.UserAccounts.Add(new UserAccount
        {
            Id = inviteeId,
            Email = "invitee@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var created = await CreateService(db, inviterId, familyId).CreateFamilyMemberInviteAsync(
            new CreateFamilyMemberInviteRequestDto { Email = "invitee@test.com" });

        Assert.Null(created.Data!.FamilyMemberId);
        Assert.Equal(created.Data.Id, created.Data.FamilyConnectionId);
        Assert.Equal(inviteeId, created.Data.LinkedUserId);
        Assert.Null(created.Data.Counterparty!.FamilyMemberId);
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
