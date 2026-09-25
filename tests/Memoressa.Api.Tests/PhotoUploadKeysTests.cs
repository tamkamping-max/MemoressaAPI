using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PhotoUploadKeysTests
{
    [Fact]
    public void Build_ProducesExpectedFileNames()
    {
        var keys = PhotoUploadKeys.Build("uploads", Guid.NewGuid(), Guid.NewGuid(), "abc.jpg", Guid.NewGuid());

        Assert.Equal("abc.jpg", keys.CompressedFileName);
        Assert.Equal("abc_full.jpg", keys.FullFileName);
        Assert.Equal("abc_nail.jpg", keys.ThumbnailFileName);
        Assert.EndsWith("/abc.jpg", keys.CompressedObjectKey);
        Assert.EndsWith("/abc_full.jpg", keys.FullObjectKey);
        Assert.EndsWith("/abc_nail.jpg", keys.ThumbnailObjectKey);
    }

    [Fact]
    public void DeriveKeys_FromCompressedObjectKey()
    {
        const string compressed = "uploads/fam/user/folder/abc.jpg";

        Assert.Equal("uploads/fam/user/folder/abc_full.jpg", PhotoUploadKeys.GetFullObjectKeyFromCompressed(compressed));
        Assert.Equal("uploads/fam/user/folder/abc_nail.jpg", PhotoUploadKeys.GetThumbnailObjectKeyFromCompressed(compressed));
    }
}
