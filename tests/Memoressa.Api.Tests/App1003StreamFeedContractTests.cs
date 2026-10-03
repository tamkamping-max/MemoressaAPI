using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class App1003StreamFeedContractTests
{
    [Fact]
    public async Task ActivityList_ReturnsHasMoreAndNextCursor()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        await using var db = CreateDb();
        SeedFamily(db, userId, familyId);
        for (var i = 0; i < 3; i++)
        {
            db.ActivityAlbums.Add(new ActivityAlbum
            {
                FamilyId = familyId,
                ExternalId = $"act_{i}",
                Title = $"Trip {i}",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.Completed,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                CreatorUserId = userId,
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        await db.SaveChangesAsync();

        var service = CreateActivityService(db, userId, familyId);
        var page = await service.ListAsync(limit: 2);
        Assert.True(page.Success);
        Assert.Equal(2, page.Data!.Data.Items.Count);
        Assert.True(page.Data.Data.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(page.Data.Data.NextCursor));
    }

    [Fact]
    public async Task ActivityPhotos_ReturnsTotalHasMoreAndCursorPages()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var photoIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        await using var db = CreateDb();
        SeedFamily(db, userId, familyId);
        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = "act_photos",
            Title = "Event",
            Type = ActivityAlbumType.Gathering,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userId
        });

        var order = 0;
        foreach (var photoId in photoIds)
        {
            db.Photos.Add(new Photo
            {
                Id = photoId,
                FamilyId = familyId,
                UploadedByUserId = userId,
                S3Key = $"{photoId}.jpg"
            });
            db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
            {
                ActivityAlbumId = activityId,
                PhotoId = photoId,
                SortOrder = order++
            });
        }

        await db.SaveChangesAsync();

        var service = CreateActivityService(db, userId, familyId);
        var first = await service.GetActivityPhotosAsync(activityId.ToString(), limit: 2);
        Assert.True(first.Success);
        Assert.Equal(2, first.Data!.Data.Items.Count);
        Assert.Equal(5, first.Data.Data.Total);
        Assert.True(first.Data.Data.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(first.Data.Data.NextCursor));

        var second = await service.GetActivityPhotosAsync(
            activityId.ToString(),
            limit: 2,
            creatorUserId: null,
            cursor: first.Data.Data.NextCursor);
        Assert.True(second.Success);
        Assert.Equal(2, second.Data!.Data.Items.Count);
        Assert.True(second.Data.Data.HasMore);
    }

    [Fact]
    public async Task ResolveActivity_WhenAmbiguous_Returns409WithCode()
    {
        var viewerId = Guid.NewGuid();
        var creatorA = Guid.NewGuid();
        var creatorB = Guid.NewGuid();
        var familyA = Guid.NewGuid();
        var familyB = Guid.NewGuid();
        const string externalId = "act_ambiguous";

        await using var db = CreateDb();
        db.UserAccounts.AddRange(
            new UserAccount { Id = viewerId, Email = "v@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = creatorA, Email = "a@test.com", PasswordHash = "x", IsActive = true },
            new UserAccount { Id = creatorB, Email = "b@test.com", PasswordHash = "x", IsActive = true });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { FamilyId = familyA, UserId = viewerId, Role = "member" },
            new FamilyMembership { FamilyId = familyB, UserId = viewerId, Role = "member" });
        db.ActivityAlbums.AddRange(
            new ActivityAlbum
            {
                FamilyId = familyA,
                ExternalId = externalId,
                Title = "A",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.InProgress,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatorUserId = creatorA,
                PrivacyScope = UploadPrivacyScope.Family
            },
            new ActivityAlbum
            {
                FamilyId = familyB,
                ExternalId = externalId,
                Title = "B",
                Type = ActivityAlbumType.Travel,
                Status = ActivityAlbumStatus.InProgress,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatorUserId = creatorB,
                PrivacyScope = UploadPrivacyScope.Family
            });
        await db.SaveChangesAsync();

        var service = CreateActivityService(db, viewerId, familyA);
        var result = await service.GetActivityPhotosAsync(externalId, limit: 10);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(ActivityErrorCodes.ActivityAlbumAmbiguous, result.ErrorCode);
    }

    private static void SeedFamily(MemoressaDbContext db, Guid userId, Guid familyId)
    {
        db.UserAccounts.Add(new UserAccount
        {
            Id = userId,
            Email = "u@test.com",
            PasswordHash = "x",
            IsActive = true
        });
        db.Families.Add(new Family { Id = familyId, Name = "Home", OwnerUserId = userId });
        db.FamilyMemberships.Add(new FamilyMembership { FamilyId = familyId, UserId = userId, Role = "owner" });
    }

    private static ActivityService CreateActivityService(MemoressaDbContext db, Guid userId, Guid familyId) =>
        new(db, new FixedUser(userId, familyId), new SummaryPhotoUrlResolver(), new StubAvatarUrlResolver());

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

    private sealed class SummaryPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PhotoDto { Id = photo.Id, ThumbnailUrl = "thumb", UploadedBy = photo.UploadedByUserId });

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PhotoDto>>(photos.Select(p => new PhotoDto
            {
                Id = p.Id,
                ThumbnailUrl = "thumb",
                UploadedBy = p.UploadedByUserId
            }).ToList());

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("url");
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
