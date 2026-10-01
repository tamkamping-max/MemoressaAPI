namespace Memoressa.Application.Common;

public class StorageQuotaSettings
{
    public const string SectionName = "Storage";

    /// <summary>Per-user cloud storage quota (original/full photo bytes only). Default 1 GiB.</summary>
    public long QuotaBytesPerUser { get; set; } = 1_073_741_824;
}
