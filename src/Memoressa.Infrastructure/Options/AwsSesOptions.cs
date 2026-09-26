namespace Memoressa.Infrastructure.Options;

public class AwsSesOptions
{
    public const string SectionName = "AwsSes";

    public string Region { get; set; } = "us-east-1";
    public string FromEmail { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Memoressa";
    public string? ConfigurationSetName { get; set; }
    /// <summary>When empty, uses the same keys as AwsS3 if configured; otherwise AWS default credential chain.</summary>
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
}
