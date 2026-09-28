using System.Text.RegularExpressions;

namespace Memoressa.Application.Common;

public static partial class AgentSearchQuery
{
    [GeneratedRegex(@"(?<![0-9])(19|20)\d{2}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex FourDigitYearRegex();

    [GeneratedRegex(@"\d{1,2}\s*月", RegexOptions.CultureInvariant)]
    private static partial Regex MonthNumberRegex();

    /// <summary>Extracts 1900–2099 year tokens from the user query (e.g. "2018夏天" → 2018).</summary>
    public static IReadOnlyList<int> ExtractYears(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var years = new HashSet<int>();
        foreach (Match match in FourDigitYearRegex().Matches(query))
        {
            if (int.TryParse(match.Value, out var year) && year is >= 1900 and <= 2099)
            {
                years.Add(year);
            }
        }

        return years.OrderBy(y => y).ToList();
    }

    /// <summary>Short keyword-style query (tag search) — skip Grok and use search-only reply.</summary>
    public static bool IsSimpleSearchPhrase(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 48)
        {
            return false;
        }

        if (trimmed.Contains('?') || trimmed.Contains('？'))
        {
            return false;
        }

        if (FourDigitYearRegex().IsMatch(trimmed))
        {
            return false;
        }

        ReadOnlySpan<char> lower = trimmed.ToLowerInvariant();
        string[] complexHints =
        [
            "什么", "哪些", "怎么", "為什麼", "为什么", "幾時", "何时", "where", "when", "what", "why", "how",
            "tell me", "show me", "find me", "look for", "帮我", "幫我", "找一下", "找一找"
        ];

        foreach (var hint in complexHints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (MonthNumberRegex().IsMatch(trimmed))
        {
            return false;
        }

        if (ContainsPhotoRequestIntent(lower) || ContainsTimeScopeIntent(lower))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsPhotoRequestIntent(ReadOnlySpan<char> lower)
    {
        ReadOnlySpan<string> hints =
        [
            "照片", "相片", "图片", "圖片", "圖照", "的图", "的圖",
            "photo", "photos", "picture", "pictures", "pic", "pics"
        ];

        foreach (var hint in hints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTimeScopeIntent(ReadOnlySpan<char> lower)
    {
        ReadOnlySpan<string> hints =
        [
            "今天", "今日", "昨日", "昨天", "前天", "明日", "明天", "後天", "后天",
            "本周", "本週", "这周", "這週", "上周", "上週", "上月", "上個月", "上个月",
            "月份", "几月", "幾月", "哪天", "哪年", "什么时候", "什麼時候",
            "today", "yesterday", "tomorrow", "month", "week", "weekend", "recent", "latest"
        ];

        foreach (var hint in hints)
        {
            if (lower.Contains(hint, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if (ch is >= '\u4e00' and <= '\u9fff'
                or >= '\u3400' and <= '\u4dbf'
                or >= '\u3040' and <= '\u30ff'
                or >= '\uac00' and <= '\ud7af')
            {
                return true;
            }
        }

        return false;
    }
}
