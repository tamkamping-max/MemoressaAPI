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
}
