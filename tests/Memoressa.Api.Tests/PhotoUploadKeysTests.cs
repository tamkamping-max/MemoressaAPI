using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PhotoUploadKeysTests
{
    [Fact]
    public void TryNormalizeOriginalFileName_AcceptsWebp()
    {
        Assert.True(PhotoUploadKeys.TryNormalizeOriginalFileName("photo.webp", out var normalized));
        Assert.Equal("photo.webp", normalized);
        Assert.True(PhotoUploadKeys.IsAllowedOriginalContentType("image/webp"));
    }

    [Fact]
    public void Build_UsesTrueOriginalFileNameForFullObject()
    {
        var folderId = Guid.NewGuid();
        var keys = PhotoUploadKeys.Build(
            "uploads",
            Guid.NewGuid(),
            Guid.NewGuid(),
            folderId,
            "abc.jpg",
            "IMG_1234.HEIC",
            null);

        Assert.Equal("abc.jpg", keys.CompressedFileName);
        Assert.Equal("IMG_1234.HEIC", keys.FullFileName);
        Assert.Equal("abc_nail.jpg", keys.ThumbnailFileName);
        Assert.EndsWith("/abc.jpg", keys.CompressedObjectKey);
        Assert.EndsWith("/IMG_1234.HEIC", keys.FullObjectKey);
        Assert.EndsWith("/abc_nail.jpg", keys.ThumbnailObjectKey);
        Assert.Null(keys.LivePhotoVideoObjectKey);
    }

    [Fact]
    public void Build_IncludesLivePhotoVideoWhenProvided()
    {
        var keys = PhotoUploadKeys.Build(
            "uploads",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "abc.jpg",
            "IMG_1234.HEIC",
            "IMG_1234.MOV");

        Assert.Equal("IMG_1234.MOV", keys.LivePhotoVideoFileName);
        Assert.EndsWith("/IMG_1234.MOV", keys.LivePhotoVideoObjectKey);
    }

    [Fact]
    public void DeriveKeys_FromCompressedObjectKey_LegacyLayout()
    {
        const string compressed = "uploads/fam/user/folder/abc.jpg";

        Assert.Equal("uploads/fam/user/folder/abc_full.jpg", PhotoUploadKeys.GetFullObjectKeyFromCompressed(compressed));
        Assert.Equal("uploads/fam/user/folder/abc_nail.jpg", PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(compressed));
    }
}
