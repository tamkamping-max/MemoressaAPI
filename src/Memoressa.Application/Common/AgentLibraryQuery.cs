namespace Memoressa.Application.Common;

public enum AgentLibraryIntent
{
    None,
    CountPhotos,
    ListPhotos
}

/// <summary>Whole-library questions (count / browse), not tag substring search.</summary>
public static class AgentLibraryQuery
{
    public static AgentLibraryIntent DetectIntent(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return AgentLibraryIntent.None;
        }

        var trimmed = query.Trim();
        var lower = trimmed.ToLowerInvariant();

        var mentionsPhotos = trimmed.Contains("照片", StringComparison.Ordinal)
            || trimmed.Contains("相片", StringComparison.Ordinal)
            || lower.Contains("photo")
            || lower.Contains("picture");

        if (!mentionsPhotos && !trimmed.Contains("张", StringComparison.Ordinal)
            && !trimmed.Contains("張", StringComparison.Ordinal))
        {
            return AgentLibraryIntent.None;
        }

        if (IsCountIntent(trimmed, lower))
        {
            return AgentLibraryIntent.CountPhotos;
        }

        if (IsListIntent(trimmed, lower))
        {
            return AgentLibraryIntent.ListPhotos;
        }

        return AgentLibraryIntent.None;
    }

    private static bool IsCountIntent(string trimmed, string lower)
    {
        if (trimmed.Contains("多少", StringComparison.Ordinal)
            || trimmed.Contains("几张", StringComparison.Ordinal)
            || trimmed.Contains("幾張", StringComparison.Ordinal)
            || trimmed.Contains("几張", StringComparison.Ordinal)
            || trimmed.Contains("总数", StringComparison.Ordinal)
            || trimmed.Contains("總數", StringComparison.Ordinal)
            || trimmed.Contains("总共", StringComparison.Ordinal)
            || trimmed.Contains("總共", StringComparison.Ordinal)
            || trimmed.Contains("一共", StringComparison.Ordinal))
        {
            return true;
        }

        return lower.Contains("how many")
            || lower.Contains("photo count")
            || lower.Contains("number of photos");
    }

    private static bool IsListIntent(string trimmed, string lower)
    {
        if ((trimmed.Contains("所有", StringComparison.Ordinal)
             || trimmed.Contains("全部", StringComparison.Ordinal)
             || trimmed.Contains("每一", StringComparison.Ordinal)
             || trimmed.Contains("所有", StringComparison.Ordinal))
            && (trimmed.Contains("照片", StringComparison.Ordinal)
                || trimmed.Contains("相片", StringComparison.Ordinal)))
        {
            return true;
        }

        if (trimmed.Contains("显示", StringComparison.Ordinal)
            || trimmed.Contains("顯示", StringComparison.Ordinal)
            || trimmed.Contains("列出", StringComparison.Ordinal)
            || trimmed.Contains("给我看", StringComparison.Ordinal)
            || trimmed.Contains("給我看", StringComparison.Ordinal))
        {
            if (trimmed.Contains("照片", StringComparison.Ordinal)
                || trimmed.Contains("相片", StringComparison.Ordinal)
                || trimmed.Contains("所有", StringComparison.Ordinal)
                || trimmed.Contains("全部", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return lower.Contains("show all")
            || lower.Contains("all my photos")
            || lower.Contains("list photos")
            || lower.Contains("see all photos");
    }
}
