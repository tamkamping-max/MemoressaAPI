using Memoressa.Application.Common;
using Memoressa.Domain.Entities;

namespace Memoressa.Api.Tests;

public class PhotoObjectKeysTests
{
    [Fact]
    public void CollectDeleteKeys_IncludesVariantTriple()
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
}
