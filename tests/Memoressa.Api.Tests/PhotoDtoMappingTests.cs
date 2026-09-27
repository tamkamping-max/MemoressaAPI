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
}

public class PhotoViewerAccessTests
{
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
