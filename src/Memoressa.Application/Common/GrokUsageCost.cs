using System.Text.Json;

namespace Memoressa.Application.Common;

public readonly record struct GrokUsage(int PromptTokens, int CompletionTokens)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}

public static class GrokUsageCost
{
    public static GrokUsage? TryParseUsage(JsonElement chatCompletionRoot)
    {
        if (!chatCompletionRoot.TryGetProperty("usage", out var usage))
        {
            return null;
        }

        if (!usage.TryGetProperty("prompt_tokens", out var promptEl)
            || !usage.TryGetProperty("completion_tokens", out var completionEl))
        {
            return null;
        }

        var prompt = promptEl.GetInt32();
        var completion = completionEl.GetInt32();
        if (prompt < 0 || completion < 0)
        {
            return null;
        }

        return new GrokUsage(prompt, completion);
    }

    /// <summary>USD from xAI-style per-million token rates (e.g. grok-4.7 default input $2 / output $6).</summary>
    public static decimal ComputeUsd(
        GrokUsage usage,
        decimal inputUsdPerMillionTokens,
        decimal outputUsdPerMillionTokens)
    {
        var input = usage.PromptTokens * inputUsdPerMillionTokens / 1_000_000m;
        var output = usage.CompletionTokens * outputUsdPerMillionTokens / 1_000_000m;
        return decimal.Round(input + output, 6, MidpointRounding.AwayFromZero);
    }
}
