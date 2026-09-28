using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

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
    public void CanView_RespectsOnlySelfPrivacy(UploadPrivacyScope scope, bool blockedForOtherUser)
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var photo = new Photo
        {
            UploadedByUserId = ownerId,
            PrivacyScope = scope
        };

        Assert.True(PhotoViewerAccess.CanView(photo, ownerId));
        Assert.Equal(blockedForOtherUser, !PhotoViewerAccess.CanView(photo, viewerId));
    }
}
