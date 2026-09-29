namespace Memoressa.Application.Common;

public class MediaStorageSettings
{
    public const string SectionName = "AwsS3";

    /// <summary>Presigned PUT expiry and upload session lifetime (max ~7 days for AWS SigV4).</summary>
    public int UploadPresignedUrlExpiryMinutes { get; set; } = 10_080;

    public string KeyPrefix { get; set; } = "uploads";

    /// <summary>Presigned GET for GET /photos/{{id}}/download (original image).</summary>
    public int DownloadPresignedUrlExpiryMinutes { get; set; } = 60;

    /// <summary>Longest edge for generated thumbnails (matches MemoressaApp photoThumbMaxPx).</summary>
    public int ThumbnailMaxEdgePixels { get; set; } = 480;

    /// <summary>JPEG quality for generated thumbnails (matches MemoressaApp photoThumbJpegQuality).</summary>
    public int ThumbnailJpegQuality { get; set; } = 82;

    /// <summary>Skip server-side thumbnail generation when the source object exceeds this size.</summary>
    public long ThumbnailMaxSourceBytes { get; set; } = 104_857_600;

    /// <summary>ffmpeg binary path for video frame extraction (EC2: apt install ffmpeg).</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
}
