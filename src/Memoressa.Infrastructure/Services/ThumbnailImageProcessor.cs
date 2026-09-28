using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Memoressa.Infrastructure.Services;

public static class ThumbnailImageProcessor
{
    public static byte[] CreateJpegThumbnail(byte[] sourceBytes, int maxEdgePixels, int jpegQuality)
    {
        using var input = new MemoryStream(sourceBytes);
        using var image = Image.Load(input);

        var width = image.Width;
        var height = image.Height;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("Invalid image dimensions.");
        }

        var resizeOptions = new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = width >= height
                ? new Size(maxEdgePixels, 0)
                : new Size(0, maxEdgePixels)
        };

        image.Mutate(ctx => ctx.Resize(resizeOptions));

        using var output = new MemoryStream();
        image.SaveAsJpeg(output, new JpegEncoder { Quality = jpegQuality });
        return output.ToArray();
    }

    public static bool IsImageContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("image/svg", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsVideoContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType)
        && contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
}
