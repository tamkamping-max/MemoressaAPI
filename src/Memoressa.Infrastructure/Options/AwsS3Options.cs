namespace Memoressa.Infrastructure.Options;

public class AwsS3Options
{
    public const string SectionName = "AwsS3";

    public string Region { get; set; } = "us-east-1";
    public string BucketName { get; set; } = "memoressa-uploads";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ServiceUrl { get; set; }
    public string KeyPrefix { get; set; } = "uploads";
    public int PresignedUrlExpiryMinutes { get; set; } = 10_080;

    /// <summary>
    /// Longer-lived presigned GET URLs for server-side AI (e.g. OpenAI Vision fetching images).
    /// </summary>
    public int AiPresignedUrlExpiryMinutes { get; set; } = 60;
}
