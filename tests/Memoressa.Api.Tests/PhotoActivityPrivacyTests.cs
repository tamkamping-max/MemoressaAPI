using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Memoressa.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class PhotoActivityPrivacyTests
{
    [Fact]
    public void PhotoDto_IncludesActivityParticipantsAndUiFlags()
    {
        var photo = new Photo
        {
            PrivacyScope = UploadPrivacyScope.FriendsAndFamily,
            ActivityParticipantsVisible = true
        };

        var dto = photo.ToDto();

        Assert.True(dto.ActivityParticipantsVisible);
        Assert.True(dto.PrivacyFamily);
        Assert.True(dto.PrivacyFriends);
        Assert.False(dto.PrivacyOnlySelf);
    }

    [Fact]
    public void ValidateStartUpload_RejectsCustomWithoutAudience()
    {
        var request = new StartUploadRequestDto
        {
            PrivacyScope = UploadPrivacyScope.Custom,
            MemberIds = [],
            FriendIds = []
        };

        var result = PhotoPrivacyValidation.ValidateStartUpload(request, hasActivityAlbum: false);

        Assert.NotNull(result);
        Assert.Equal(400, result!.StatusCode);
    }

    [Fact]
    public void ValidateCompleteOrUpdate_RejectsOnlySelfWithFamilyVisibility()
    {
        var result = PhotoPrivacyValidation.ValidateCompleteOrUpdate(
            UploadPrivacyScope.OnlySelf,
            MemoryVisibility.Family,
            null,
            null);

        Assert.NotNull(result);
        Assert.Equal(400, result!.StatusCode);
    }

    [Fact]
    public async Task CanViewAsync_ActivityParticipant_SeesOnlySelfPhoto_WhenFlagSet()
    {
        var options = new DbContextOptionsBuilder<MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new MemoressaDbContext(options);

        var familyId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        var activityId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        db.Families.Add(new Family { Id = familyId, Name = "F", OwnerUserId = uploaderId });
        db.UserAccounts.AddRange(
            new UserAccount { Id = uploaderId, Email = "u@test.com" },
            new UserAccount { Id = participantId, Email = "p@test.com" });
        db.FamilyMemberships.Add(new FamilyMembership { UserId = uploaderId, FamilyId = familyId });
        db.FamilyMemberships.Add(new FamilyMembership { UserId = participantId, FamilyId = familyId });

        db.ActivityAlbums.Add(new ActivityAlbum
        {
            Id = activityId,
            FamilyId = familyId,
            ExternalId = "act_test",
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            CreatorUserId = uploaderId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PrivacyScope = UploadPrivacyScope.Family
        });

        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = uploaderId,
            S3Key = "a.jpg",
            ContentType = "image/jpeg",
            PrivacyScope = UploadPrivacyScope.OnlySelf,
            ActivityParticipantsVisible = true
        });

        db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
        {
            ActivityAlbumId = activityId,
            PhotoId = photoId
        });

        await db.SaveChangesAsync();

        Assert.True(await PhotoViewerAccess.CanViewAsync(db, photoId, uploaderId, CancellationToken.None));
        Assert.True(await PhotoViewerAccess.CanViewAsync(db, photoId, participantId, CancellationToken.None));

        db.ActivityAlbumPhotos.Remove(await db.ActivityAlbumPhotos.FirstAsync(l => l.PhotoId == photoId));
        await db.SaveChangesAsync();

        Assert.False(await PhotoViewerAccess.CanViewAsync(db, photoId, participantId, CancellationToken.None));
    }
}
