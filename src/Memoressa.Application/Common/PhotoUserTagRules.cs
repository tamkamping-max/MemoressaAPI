namespace Memoressa.Application.Common;

public static class PhotoUserTagRules
{
    public const int MaxTagLength = 128;
    public const int MaxTagsPerPhoto = 64;

    /// <summary>
    /// Normalizes a user tag list for full replace (trim, drop empty, dedupe case-insensitive).
    /// </summary>
    public static bool TryNormalizeSingle(string raw, out string tag)
    {
        tag = raw.Trim();
        if (tag.Length == 0)
        {
            return false;
        }

        if (tag.Length > MaxTagLength)
        {
            tag = tag[..MaxTagLength];
        }

        return true;
    }

    public static IReadOnlyList<string> NormalizeReplaceList(IEnumerable<string> tags)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in tags)
        {
            var tag = raw.Trim();
            if (tag.Length == 0)
            {
                continue;
            }

            if (tag.Length > MaxTagLength)
            {
                tag = tag[..MaxTagLength];
            }

            if (!seen.Add(tag))
            {
                continue;
            }

            result.Add(tag);
            if (result.Count >= MaxTagsPerPhoto)
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// App sends <c>userTags</c> and/or legacy <c>aiTags</c> on PUT; both mean user tag replace payload.
    /// </summary>
    public static IReadOnlyList<string>? ResolveReplacePayload(
        IReadOnlyList<string>? userTags,
        IReadOnlyList<string>? aiTags)
    {
        if (userTags is not null)
        {
            return NormalizeReplaceList(userTags);
        }

        if (aiTags is not null)
        {
            return NormalizeReplaceList(aiTags);
        }

        return null;
    }
}
