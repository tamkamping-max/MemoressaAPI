using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivityServiceDeleteTests
{
    [Fact]
    public async Task DeleteAsync_Returns403WhenUserIsNotCreator()
    {
        var creatorId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyWithActivity(db, familyId, creatorId, otherId, out var externalId);

        var service = new ActivityService(db, new FixedUser(otherId, familyId), new StubPhotoUrlResolver());

        var result = await service.DeleteAsync(externalId);

        Assert.Equal(403, result.StatusCode);
        Assert.True(await db.ActivityAlbums.AnyAsync(a => a.ExternalId == externalId));
    }

    [Fact]
    public async Task DeleteAsync_RemovesAlbumAndPhotoLinksNotPhotos()
    {
        var creatorId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamilyWithActivity(db, familyId, creatorId, creatorId, out var externalId);

        var activityId = await db.ActivityAlbums.Where(a => a.ExternalId == externalId).Select(a => a.Id).FirstAsync();
        var photo = new Photo
        {
            FamilyId = familyId,
            UploadedByUserId = creatorId,
            S3Key = "k.jpg",
            TakenAt = DateTime.UtcNow
        };
        db.Photos.Add(photo);
        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
        {
            ActivityAlbumId = activityId,
            PhotoId = photo.Id,
            SortOrder = 1
        });
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new FixedUser(creatorId, familyId), new StubPhotoUrlResolver());
        var result = await service.DeleteAsync(externalId);

        Assert.Equal(204, result.StatusCode);
        Assert.False(await db.ActivityAlbums.AnyAsync(a => a.ExternalId == externalId));
        Assert.False(await db.ActivityAlbumPhotos.AnyAsync(ap => ap.ActivityAlbumId == activityId));
        Assert.True(await db.Photos.AnyAsync(p => p.Id == photo.Id));
    }

    private static MemoressaDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MemoressaDbContext(options);
    }

    private static void SeedFamilyWithActivity(
        IMemoressaDbContext db,
        Guid familyId,
        Guid creatorId,
        Guid memberId,
        out string externalId)
    {
        externalId = "act_testdelete12345";
        db.UserAccounts.Add(new UserAccount { Id = creatorId, Email = "c@test.com", PasswordHash = "x" });
        if (memberId != creatorId)
        {
            db.UserAccounts.Add(new UserAccount { Id = memberId, Email = "m@test.com", PasswordHash = "x" });
        }

        db.Families.Add(new Family { Id = familyId, OwnerUserId = creatorId, Name = "F" });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = creatorId, Role = "owner" });
        if (memberId != creatorId)
        {
            db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = memberId, Role = "member" });
        }

        db.ActivityAlbums.Add(new ActivityAlbum
        {
            FamilyId = familyId,
            ExternalId = externalId,
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = creatorId
        });
        db.SaveChangesAsync().GetAwaiter().GetResult();
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
            Task.FromResult(photo.ToDto());

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>(photos.Select(p => p.ToDto()).ToList());

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
