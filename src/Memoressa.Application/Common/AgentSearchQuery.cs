using System.Text.RegularExpressions;

namespace Memoressa.Application.Common;

public static partial class AgentSearchQuery
{
    [GeneratedRegex(@"(?<![0-9])(19|20)\d{2}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex FourDigitYearRegex();

    [GeneratedRegex(@"\d{1,2}\s*月", RegexOptions.CultureInvariant)]
    private static partial Regex MonthNumberRegex();

    [GeneratedRegex(@"(?<![0-9])(?<month>1[0-2]|0?[1-9])\s*(月|月份)", RegexOptions.CultureInvariant)]
    private static partial Regex MonthWithDigitsRegex();

    /// <summary>Parse year/month/day intent (e.g. 9月, 今年, 今天).</summary>
    public static AgentCalendarFilter ExtractCalendarFilter(string query, DateTime referenceDate)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return default;
        }

        var trimmed = query.Trim();
        int? year = null;
        int? month = null;
        int? day = null;

        var explicitYears = ExtractYears(trimmed);
        if (explicitYears.Count > 0)
        {
            year = explicitYears[0];
        }

        foreach (Match match in MonthWithDigitsRegex().Matches(trimmed))
        {
            if (int.TryParse(match.Groups["month"].Value, out var mo) && mo is >= 1 and <= 12)
            {
                month = mo;
            }
        }

        month ??= TryParseChineseMonthName(trimmed);

        var lower = trimmed.ToLowerInvariant();
        if (year is null && (trimmed.Contains("今年", StringComparison.Ordinal) || lower.Contains("this year")))
        {
            year = referenceDate.Year;
        }

        if (month.HasValue && year is null)
        {
            year = referenceDate.Year;
        }

        var mentionsToday = trimmed.Contains("今天", StringComparison.Ordinal)
            || trimmed.Contains("今日", StringComparison.Ordinal)
            || lower.Contains("today");
        var mentionsMonthWord = trimmed.Contains('月') || lower.Contains("month");

        if (mentionsToday)
        {
            if (!month.HasValue && mentionsMonthWord)
            {
                year ??= referenceDate.Year;
                month = referenceDate.Month;
            }
            else if (!month.HasValue && !mentionsMonthWord)
            {
                year = referenceDate.Year;
                month = referenceDate.Month;
                day = referenceDate.Day;
            }
        }

        if (month is null
            && (trimmed.Contains("本月", StringComparison.Ordinal)
                || trimmed.Contains("這個月", StringComparison.Ordinal)
                || trimmed.Contains("这个月", StringComparison.Ordinal)
                || lower.Contains("this month")))
        {
            month = referenceDate.Month;
            year ??= referenceDate.Year;
        }

        month ??= TryParseEnglishMonthName(lower);

        if (month.HasValue && year is null)
        {
            year = referenceDate.Year;
        }

        return new AgentCalendarFilter(year, month, day);
    }

    private static int? TryParseChineseMonthName(string query)
    {
        ReadOnlySpan<(string Token, int Month)> names =
        [
            ("十二月", 12), ("十一月", 11), ("十月", 10),
            ("九月", 9), ("八月", 8), ("七月", 7), ("六月", 6),
            ("五月", 5), ("四月", 4), ("三月", 3), ("二月", 2), ("一月", 1),
            ("正月", 1),
        ];

        foreach (var (token, mo) in names)
        {
            if (query.Contains(token, StringComparison.Ordinal))
            {
                return mo;
            }
        }

        return null;
    }

    private static int? TryParseEnglishMonthName(ReadOnlySpan<char> lower)
    {
        ReadOnlySpan<(string Name, int Month)> names =
        [
            ("january", 1), ("february", 2), ("march", 3), ("april", 4),
            ("may", 5), ("june", 6), ("july", 7), ("august", 8),
            ("september", 9), ("october", 10), ("november", 11), ("december", 12),
            ("sep", 9), ("sept", 9), ("oct", 10), ("nov", 11), ("dec", 12),
        ];

        foreach (var (name, mo) in names)
        {
            if (lower.Contains(name, StringComparison.Ordinal))
            {
                return mo;
            }
        }

        return null;
    }

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
