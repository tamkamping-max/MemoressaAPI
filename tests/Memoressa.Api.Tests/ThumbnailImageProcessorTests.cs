using Memoressa.Infrastructure.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Memoressa.Api.Tests;

public class ThumbnailImageProcessorTests
{
    [Fact]
    public void CreateJpegThumbnail_ResizesLandscapeToMaxEdge480()
    {
        using var sourceImage = new Image<Rgba32>(960, 720);
        sourceImage.Mutate(x => x.BackgroundColor(Color.Red));
        using var sourceStream = new MemoryStream();
        sourceImage.SaveAsJpeg(sourceStream);

        var thumbBytes = ThumbnailImageProcessor.CreateJpegThumbnail(sourceStream.ToArray(), 480, 82);

        using var thumb = Image.Load(thumbBytes);
        Assert.Equal(480, thumb.Width);
        Assert.Equal(360, thumb.Height);
    }

    [Fact]
    public void CreateJpegThumbnail_ResizesPortraitToMaxEdge480()
    {
        using var sourceImage = new Image<Rgba32>(720, 960);
        sourceImage.Mutate(x => x.BackgroundColor(Color.Blue));
        using var sourceStream = new MemoryStream();
        sourceImage.SaveAsJpeg(sourceStream);

        var thumbBytes = ThumbnailImageProcessor.CreateJpegThumbnail(sourceStream.ToArray(), 480, 82);

        using var thumb = Image.Load(thumbBytes);
        Assert.Equal(360, thumb.Width);
        Assert.Equal(480, thumb.Height);
    }

    [Fact]
    public void BuildThumbnailKey_PlacesThumbNextToOriginal()
    {
        const string original = "uploads/family/user/guid/photo.jpg";
        var lastSlash = original.LastIndexOf('/');
        var thumbKey = string.Concat(original.AsSpan(0, lastSlash + 1), "thumb.jpg");
        Assert.Equal("uploads/family/user/guid/thumb.jpg", thumbKey);
    }
}
