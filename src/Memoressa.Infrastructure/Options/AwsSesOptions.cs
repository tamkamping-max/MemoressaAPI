namespace Memoressa.Infrastructure.Options;

public class AwsSesOptions
{
    public const string SectionName = "AwsSes";

    /// <summary>Used to derive default SMTP host <c>email-smtp.{region}.amazonaws.com</c> when <see cref="SmtpHost"/> is empty.</summary>
    public string Region { get; set; } = "us-east-1";
    public string FromEmail { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "Memoressa";
    /// <summary>Optional SES configuration set (SMTP header <c>X-SES-CONFIGURATION-SET</c>).</summary>
    public string? ConfigurationSetName { get; set; }
    /// <summary>e.g. <c>email-smtp.us-east-1.amazonaws.com</c>. Empty → derived from <see cref="Region"/>.</summary>
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    /// <summary>SMTP credentials from SES console (not IAM access key id).</summary>
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
}
