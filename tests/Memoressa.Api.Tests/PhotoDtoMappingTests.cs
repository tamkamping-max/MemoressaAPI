using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Api.Tests;

public class PhotoDtoMappingTests
{
    [Fact]
    public void ToDto_IncludesUploaderNicknameEmailAndNestedUploader()
    {
        var uploaderId = Guid.NewGuid();
        var photo = new Photo
        {
            UploadedByUserId = uploaderId,
            UploadedBy = new UserAccount
            {
                Id = uploaderId,
                Email = "tam@example.com",
                Nickname = "Tam"
            }
        };

        var dto = photo.ToDto();

        Assert.Equal(uploaderId, dto.UploadedBy);
        Assert.Equal("Tam", dto.UploaderNickname);
        Assert.Equal("tam@example.com", dto.UploaderEmail);
        Assert.NotNull(dto.Uploader);
        Assert.Equal(uploaderId, dto.Uploader!.Id);
        Assert.Equal("Tam", dto.Uploader.Nickname);
        Assert.Equal("tam@example.com", dto.Uploader.Email);
    }

    [Fact]
    public void ToDto_OmitsDisplayFieldsWhenNicknameAndEmailMissing()
    {
        var uploaderId = Guid.NewGuid();
        var photo = new Photo
        {
            UploadedByUserId = uploaderId,
            UploadedBy = new UserAccount { Id = uploaderId, Email = " ", Nickname = null }
        };

        var dto = photo.ToDto();

        Assert.Equal(uploaderId, dto.UploadedBy);
        Assert.Null(dto.UploaderNickname);
        Assert.Null(dto.UploaderEmail);
        Assert.Null(dto.Uploader);
    }

    [Fact]
    public void ToDto_MapsUserTagsAndAiTagsSeparately()
    {
        var photo = new Photo
        {
            UserTags =
            [
                new PhotoUserTag { Tag = "pet" },
                new PhotoUserTag { Tag = "family" }
            ],
            AiTags =
            [
                new PhotoAiTag { Tag = "outdoor" }
            ]
        };

        var dto = photo.ToDto();

        Assert.Equal(["family", "pet"], dto.UserTags);
        Assert.Equal(["outdoor"], dto.AiTags);
    }

    [Fact]
    public void ToDto_MapsPrivacyScope()
    {
        var photo = new Photo { PrivacyScope = UploadPrivacyScope.Custom };
        var dto = photo.ToDto();
        Assert.Equal(UploadPrivacyScope.Custom, dto.PrivacyScope);
    }

    [Fact]
    public void ToDto_MapsFriendIdsFromPhotoFriends()
    {
        var friendId = Guid.NewGuid();
        var photo = new Photo
        {
            PhotoFriends = [new PhotoFriend { FriendId = friendId }]
        };

        var dto = photo.ToDto();

        Assert.Equal([friendId], dto.FriendIds);
    }
}

public class PhotoViewerAccessTests
{
    [Fact]
    public void IsUploader_MatchesUploadedByUserId()
    {
        var uploaderId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var photo = new Photo { UploadedByUserId = uploaderId };

        Assert.True(PhotoViewerAccess.IsUploader(photo, uploaderId));
        Assert.False(PhotoViewerAccess.IsUploader(photo, otherId));
    }

    [Theory]
    [InlineData(UploadPrivacyScope.Family, false)]
    [InlineData(UploadPrivacyScope.OnlySelf, true)]
    public async Task CanViewAsync_RespectsOnlySelfPrivacy(UploadPrivacyScope scope, bool blockedForOtherUser)
    {
        var options = new DbContextOptionsBuilder<Memoressa.Infrastructure.Data.MemoressaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new Memoressa.Infrastructure.Data.MemoressaDbContext(options);

        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var familyId = Guid.NewGuid();
        var photoId = Guid.NewGuid();

        db.Families.Add(new Family { Id = familyId, Name = "T", OwnerUserId = ownerId });
        db.UserAccounts.AddRange(
            new UserAccount { Id = ownerId, Email = "o@test.com" },
            new UserAccount { Id = viewerId, Email = "v@test.com" });
        db.FamilyMemberships.AddRange(
            new FamilyMembership { UserId = ownerId, FamilyId = familyId },
            new FamilyMembership { UserId = viewerId, FamilyId = familyId });
        db.Photos.Add(new Photo
        {
            Id = photoId,
            FamilyId = familyId,
            UploadedByUserId = ownerId,
            S3Key = "x.jpg",
            ContentType = "image/jpeg",
            PrivacyScope = scope
        });
        await db.SaveChangesAsync();

        Assert.True(await PhotoViewerAccess.CanViewAsync(db, photoId, ownerId, CancellationToken.None));
        Assert.Equal(blockedForOtherUser, !await PhotoViewerAccess.CanViewAsync(db, photoId, viewerId, CancellationToken.None));
    }
}
