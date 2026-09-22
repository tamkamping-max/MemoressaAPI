namespace Memoressa.Application.Common;

public class MediaStorageSettings
{
    public const string SectionName = "AwsS3";

    public int PresignedUrlExpiryMinutes { get; set; } = 15;
}
