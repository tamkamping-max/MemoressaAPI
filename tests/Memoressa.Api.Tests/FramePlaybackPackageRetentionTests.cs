using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class FramePlaybackPackageRetentionTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("{}", true)]
    [InlineData("{\"source\":\"family\"}", true)]
    [InlineData("not-json", true)]
    [InlineData("{\"isFriendShare\":true}", false)]
    [InlineData("{\"source\":\"friend\"}", false)]
    [InlineData("{\"source\":\"Friend\"}", false)]
    public void ShouldRemoveOnDeviceUnbind(string? packageJson, bool expected) =>
        Assert.Equal(expected, FramePlaybackPackageRetention.ShouldRemoveOnDeviceUnbind(packageJson!));
}
