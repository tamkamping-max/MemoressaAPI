namespace Memoressa.Application.Common;

/// <summary>Bilingual synonym groups for AI Agent substring search (tags and text).</summary>
public static class AgentSearchSynonyms
{
    private static readonly string[][] Groups =
    [
        ["開心", "开心", "happy", "joy", "joyful"],
        ["難過", "难过", "sad", "unhappy"],
        ["生日", "birthday"],
        ["旅行", "旅遊", "旅游", "travel", "trip"],
        ["家庭", "family"],
        ["聚餐", "吃飯", "吃饭", "dinner", "meal"],
        ["海邊", "海边", "beach", "ocean"],
        ["婚禮", "婚礼", "wedding"],
        ["寶寶", "宝宝", "baby"],
        ["寵物", "宠物", "pet", "dog", "cat", "狗狗", "貓咪", "猫咪"],
        ["夏天", "summer"],
        ["冬天", "winter"],
        ["春天", "spring"],
        ["秋天", "autumn", "fall"],
    ];

    /// <summary>
    /// Returns the user query plus synonym terms when any group member appears in the query.
    /// </summary>
    public static IReadOnlyList<string> BuildSearchTerms(string query)
    {
        var trimmed = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        var terms = new List<string> { trimmed };

        foreach (var group in Groups)
        {
            if (!group.Any(member => trimmed.Contains(member, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            foreach (var member in group)
            {
                if (string.IsNullOrWhiteSpace(member))
                {
                    continue;
                }

                if (!terms.Any(t => t.Equals(member, StringComparison.OrdinalIgnoreCase)))
                {
                    terms.Add(member);
                }
            }
        }

        return terms;
    }
}
