using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class UploadMetadataTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  Taipei  ", "Taipei")]
    public void NormalizeOptionalText_TrimsOrNulls(string? input, string? expected) =>
        Assert.Equal(expected, UploadMetadata.NormalizeOptionalText(input));
}
