namespace Memoressa.Application.Common;

public static class AgentSearchTermBuilder
{
    public const int MaxKeywordLength = PhotoUserTagRules.MaxTagLength;

    public static IReadOnlyList<string> Build(string query, IReadOnlyList<string>? additionalTerms = null)
    {
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var term in AgentSearchSynonyms.BuildSearchTerms(query))
        {
            terms.Add(term);
        }

        if (additionalTerms is null)
        {
            return terms.ToList();
        }

        foreach (var raw in additionalTerms)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var trimmed = raw.Trim();
            if (trimmed.Length > MaxKeywordLength)
            {
                continue;
            }

            terms.Add(trimmed);
        }

        return terms.ToList();
    }
}
