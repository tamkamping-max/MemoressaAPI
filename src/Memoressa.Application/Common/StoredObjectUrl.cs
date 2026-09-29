namespace Memoressa.Application.Common;

public static class StoredObjectUrl
{
    /// <summary>True when the value is an object key stored in S3 (not an absolute http(s) URL).</summary>
    public static bool IsPresignableObjectKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
