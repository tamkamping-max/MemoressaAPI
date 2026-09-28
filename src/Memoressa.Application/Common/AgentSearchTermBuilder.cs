namespace Memoressa.Application.Common;

public static class AgentSearchTermBuilder
{
    public const int MaxKeywordLength = PhotoUserTagRules.MaxTagLength;

    public static IReadOnlyList<string> Build(string query, IReadOnlyList<string>? additionalTerms = null)
    {
        var terms = new List<string>();
        foreach (var term in AgentSearchSynonyms.BuildSearchTerms(query))
        {
            AddDistinct(terms, term);
        }

        if (additionalTerms is null)
        {
            return terms;
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

            AddDistinct(terms, trimmed);
        }

        return terms;
    }

    /// <summary>Caps DB/Grok substring fan-out; always keeps the first (user) term.</summary>
    public static IReadOnlyList<string> Limit(IReadOnlyList<string> terms, int maxTerms)
    {
        if (terms.Count <= maxTerms || maxTerms < 1)
        {
            return terms;
        }

        return terms.Take(maxTerms).ToList();
    }

    private static void AddDistinct(List<string> terms, string value)
    {
        if (terms.Any(t => t.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        terms.Add(value);
    }
}
