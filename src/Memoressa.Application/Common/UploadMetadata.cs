namespace Memoressa.Application.Common;

public static class UploadMetadata
{
    public static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
