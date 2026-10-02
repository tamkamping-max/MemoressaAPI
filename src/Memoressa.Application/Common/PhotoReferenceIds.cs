namespace Memoressa.Application.Common;

public static class PhotoReferenceIds
{
    public static bool TryParse(string? raw, out Guid photoId)
    {
        photoId = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (Guid.TryParse(trimmed, out photoId))
        {
            return true;
        }

        const string prefix = "photo_";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var tail = trimmed[prefix.Length..];
            if (Guid.TryParse(tail, out photoId))
            {
                return true;
            }

            if (tail.Length == 32 && Guid.TryParseExact(tail, "N", out photoId))
            {
                return true;
            }
        }

        return false;
    }

    public static (List<Guid> Ids, string? Error) ParseDistinctOrdered(IReadOnlyList<string> rawIds)
    {
        if (rawIds.Count == 0)
        {
            return ([], "photoIds must contain at least one id");
        }

        var ids = new List<Guid>(rawIds.Count);
        var seen = new HashSet<Guid>();
        foreach (var raw in rawIds)
        {
            if (!TryParse(raw, out var id))
            {
                return ([], $"Invalid photo id: {raw}");
            }

            if (seen.Add(id))
            {
                ids.Add(id);
            }
        }

        return (ids, null);
    }
}
