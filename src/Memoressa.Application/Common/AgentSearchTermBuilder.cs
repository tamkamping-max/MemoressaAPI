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

    /// <summary>At most two terms for DB: user query plus one cross-script synonym when available.</summary>
    public static IReadOnlyList<string> ForDatabaseSearch(
        string query,
        IReadOnlyList<string>? additionalTerms,
        int maxTerms)
    {
        var built = Build(query, additionalTerms);
        if (built.Count == 0 || maxTerms < 1)
        {
            return built;
        }

        if (maxTerms == 1)
        {
            return [built[0]];
        }

        var compact = new List<string> { built[0] };
        var cross = PickCrossLocaleSynonym(built[0], built);
        if (cross is not null)
        {
            compact.Add(cross);
        }

        return Limit(compact, Math.Min(maxTerms, 2));
    }

    private static string? PickCrossLocaleSynonym(string primary, IReadOnlyList<string> candidates)
    {
        var primaryHasCjk = AgentSearchQuery.ContainsCjk(primary);
        foreach (var candidate in candidates.Skip(1))
        {
            if (candidate.Equals(primary, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AgentSearchQuery.ContainsCjk(candidate) != primaryHasCjk)
            {
                return candidate;
            }
        }

        return candidates.Count > 1 ? candidates[1] : null;
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
