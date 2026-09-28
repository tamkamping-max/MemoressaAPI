using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PhotoAlbumFingerprintTests
{
    [Fact]
    public void Compute_IsOrderIndependent()
    {
        var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var fp1 = PhotoAlbumFingerprint.Compute([a, b]);
        var fp2 = PhotoAlbumFingerprint.Compute([b, a]);

        Assert.Equal(fp1, fp2);
        Assert.Equal(64, fp1.Length);
    }

    [Fact]
    public void Compute_DedupesIds()
    {
        var id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var fp1 = PhotoAlbumFingerprint.Compute([id, id]);
        var fp2 = PhotoAlbumFingerprint.Compute([id]);

        Assert.Equal(fp1, fp2);
    }
}
