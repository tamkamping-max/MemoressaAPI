using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class TimelineCursorTests
{
    [Fact]
    public void EncodeDecode_RoundTrips()
    {
        var original = new TimelineCursor(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc), Guid.NewGuid());
        var encoded = original.Encode();
        var decoded = TimelineCursor.TryDecode(encoded);

        Assert.NotNull(decoded);
        Assert.Equal(original.SortAtUtc, decoded!.SortAtUtc);
        Assert.Equal(original.PhotoId, decoded.PhotoId);
    }

    [Fact]
    public void TryDecode_ReturnsNullForGarbage()
    {
        Assert.Null(TimelineCursor.TryDecode("not-a-valid-cursor"));
    }

    [Fact]
    public void NormalizeLimit_ClampsToMax()
    {
        Assert.Equal(20, PhotoTimelinePagination.NormalizeLimit(null));
        Assert.Equal(20, PhotoTimelinePagination.NormalizeLimit(20));
        Assert.Equal(50, PhotoTimelinePagination.NormalizeLimit(999));
    }
}
