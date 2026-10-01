using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PhotoUserTagRulesTests
{
    [Fact]
    public void ResolveReplacePayload_PrefersUserTagsOverAiTags()
    {
        var tags = PhotoUserTagRules.ResolveReplacePayload(
            ["family"],
            ["ignored"]);

        Assert.Equal(["family"], tags);
    }

    [Fact]
    public void ResolveReplacePayload_AcceptsLegacyAiTagsOnly()
    {
        var tags = PhotoUserTagRules.ResolveReplacePayload(null, ["travel", " travel "]);

        Assert.Equal(["travel"], tags);
    }

    [Fact]
    public void NormalizeReplaceList_DedupesCaseInsensitive()
    {
        var tags = PhotoUserTagRules.NormalizeReplaceList(["Family", "family", "  ", "pet"]);

        Assert.Equal(["Family", "pet"], tags);
    }

    [Fact]
    public void TryNormalizeSingle_TrimsAndRejectsEmpty()
    {
        Assert.False(PhotoUserTagRules.TryNormalizeSingle("   ", out _));
        Assert.True(PhotoUserTagRules.TryNormalizeSingle("  holiday  ", out var tag));
        Assert.Equal("holiday", tag);
    }
}
