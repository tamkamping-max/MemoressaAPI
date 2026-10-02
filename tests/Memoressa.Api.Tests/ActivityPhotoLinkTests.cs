using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Application.Services;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class ActivityPhotoLinkTests
{
    [Fact]
    public async Task StageActivityPhotoLink_Links_Photo_Before_Photo_Is_Saved_To_Database()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var db = new MemoressaDbContext(options);

        var familyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = "act_test_link",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            Status = ActivityAlbumStatus.InProgress,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatorUserId = userId,
            PrivacyScope = UploadPrivacyScope.Family,
        });

        var photo = new Photo
        {
            FamilyId = familyId,
            UploadedByUserId = userId,
            S3Key = "k.jpg",
            ContentType = "image/jpeg",
            PrivacyScope = UploadPrivacyScope.Family,
        };
        db.Photos.Add(photo);
        await db.SaveChangesAsync();

        var service = new ActivityService(db, new StubCurrentUser(), new StubPhotoUrlResolver(), new StubAvatarUrlResolver());

        await service.StageActivityPhotoLinkAsync(activityId, photo.Id, familyId, CancellationToken.None);
        await db.SaveChangesAsync();

        var link = await db.ActivityAlbumPhotos.AsNoTracking()
            .FirstOrDefaultAsync(ap => ap.ActivityAlbumId == activityId && ap.PhotoId == photo.Id);
        Assert.NotNull(link);
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? FamilyId => null;
        public bool IsAuthenticated => false;
    }

    private sealed class StubPhotoUrlResolver : IPhotoUrlResolver
    {
        public Task<PhotoDto> ToDtoAsync(
            Photo photo,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
            IEnumerable<Photo> photos,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<string?> GetPresignedUrlAsync(
            Photo photo,
            bool thumbnail = false,
            PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
