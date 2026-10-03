using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class FamilyMemberRosterTests
{
    [Fact]
    public async Task GetMembers_IncludesUnassignedAccepted_InDefaultAndAcceptedFilter()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, ownerId, familyId);
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
                Name = "Pool",
                LinkedUserId = Guid.NewGuid(),
                AssignedToTree = false,
                Generation = Generation.Self
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, ownerId, familyId);

        var full = await service.GetMembersAsync();
        Assert.Equal(2, full.Data!.Count);
        var pool = Assert.Single(full.Data, m => m.Name == "Pool");
        Assert.False(pool.AssignedToTree);
        Assert.Null(pool.Generation);

        var acceptedOnly = await service.GetMembersAsync("accepted");
        Assert.Equal(2, acceptedOnly.Data!.Count);
        Assert.Contains(acceptedOnly.Data, m => m.Name == "Pool" && !m.AssignedToTree);
    }

    [Fact]
    public async Task UpdateMember_AssignedToTreeAndGeneration_ReflectedOnNextGet()
    {
        var familyId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, ownerId, familyId);
        var member = new FamilyMember
        {
            FamilyId = familyId,
            Name = "Movable",
            AssignedToTree = false,
            Generation = Generation.Self
        };
        db.FamilyMembers.Add(member);
        await db.SaveChangesAsync();

        var service = CreateService(db, ownerId, familyId);

        var updated = await service.UpdateMemberAsync(
            member.Id,
            new UpdateFamilyMemberRequestDto
            {
                Name = "Movable",
                Generation = Generation.Parent,
                AssignedToTree = true
            });
        Assert.True(updated.Success);
        Assert.True(updated.Data!.AssignedToTree);
        Assert.Equal(Generation.Parent, updated.Data.Generation);

        var fromList = await service.GetMembersAsync("accepted");
        var row = Assert.Single(fromList.Data!, m => m.Id == member.Id);
        Assert.True(row.AssignedToTree);
        Assert.Equal(Generation.Parent, row.Generation);
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
