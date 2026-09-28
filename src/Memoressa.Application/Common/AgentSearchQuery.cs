using System.Text.RegularExpressions;

namespace Memoressa.Application.Common;

public static partial class AgentSearchQuery
{
    [GeneratedRegex(@"(?<![0-9])(19|20)\d{2}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex FourDigitYearRegex();

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

        return true;
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
