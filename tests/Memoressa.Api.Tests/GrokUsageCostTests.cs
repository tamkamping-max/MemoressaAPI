using System.Text.Json;
using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class GrokUsageCostTests
{
    [Fact]
    public void TryParseUsage_ReadsPromptAndCompletion()
    {
        using var doc = JsonDocument.Parse("""
            {
              "usage": {
                "prompt_tokens": 812,
                "completion_tokens": 142,
                "total_tokens": 954
              }
            }
            """);

        var usage = GrokUsageCost.TryParseUsage(doc.RootElement);
        Assert.NotNull(usage);
        Assert.Equal(812, usage.Value.PromptTokens);
        Assert.Equal(142, usage.Value.CompletionTokens);
    }

    [Fact]
    public void ComputeUsd_UsesPerMillionRates()
    {
        var usage = new GrokUsage(1_000_000, 1_000_000);
        var usd = GrokUsageCost.ComputeUsd(usage, inputUsdPerMillionTokens: 2m, outputUsdPerMillionTokens: 6m);
        Assert.Equal(8m, usd);
    }

    [Fact]
    public void ComputeUsd_MatchesTypicalVisionCall()
    {
        var usage = new GrokUsage(650, 120);
        var usd = GrokUsageCost.ComputeUsd(usage, 2m, 6m);
        Assert.Equal(0.002020m, usd);
    }
}
