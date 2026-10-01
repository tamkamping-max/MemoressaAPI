namespace Memoressa.Application.Common;

public class UploadSessionCleanupOptions
{
    public const string SectionName = "UploadSessionCleanup";

    public int IntervalHours { get; set; } = 6;

    /// <summary>Pending sessions with ExpiresAt older than UtcNow minus this grace are abandoned.</summary>
    public int GraceHours { get; set; } = 24;
}
