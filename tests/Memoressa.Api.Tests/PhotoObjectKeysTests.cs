using Memoressa.Application.Common;
using Memoressa.Domain.Entities;

namespace Memoressa.Api.Tests;

public class PhotoObjectKeysTests
{
    [Fact]
    public void CollectDeleteKeys_IncludesLegacyVariantTriple()
    {
        var photo = new Photo
        {
            S3Key = "uploads/fam/user/folder/abc.jpg",
            ThumbnailS3Key = "uploads/fam/user/folder/abc_nail.jpg"
        };

        var keys = PhotoObjectKeys.CollectDeleteKeys(photo);

        Assert.Contains("uploads/fam/user/folder/abc.jpg", keys);
        Assert.Contains("uploads/fam/user/folder/abc_full.jpg", keys);
        Assert.Contains("uploads/fam/user/folder/abc_nail.jpg", keys);
        Assert.Equal(3, keys.Count);
    }

    [Fact]
    public void CollectDeleteKeys_UsesStoredFullKeyAndLiveVideo()
    {
        var photo = new Photo
        {
            S3Key = "uploads/fam/user/folder/abc.jpg",
            S3KeyFull = "uploads/fam/user/folder/IMG_1234.HEIC",
            ThumbnailS3Key = "uploads/fam/user/folder/abc_nail.jpg",
            LivePhotoVideoS3Key = "uploads/fam/user/folder/IMG_1234.MOV"
        };

        var keys = PhotoObjectKeys.CollectDeleteKeys(photo);

        Assert.Contains("uploads/fam/user/folder/IMG_1234.HEIC", keys);
        Assert.Contains("uploads/fam/user/folder/IMG_1234.MOV", keys);
        Assert.DoesNotContain("uploads/fam/user/folder/abc_full.jpg", keys);
        Assert.Equal(4, keys.Count);
    }
}
