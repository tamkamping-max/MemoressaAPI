using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class StoredObjectUrlTests
{
    [Theory]
    [InlineData("avatars/users/x/abc.jpg", true)]
    [InlineData("uploads/family/u/p.jpg", true)]
    [InlineData("https://cdn.example.com/a.jpg", false)]
    [InlineData("http://localhost/a.jpg", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsPresignableObjectKey_DetectsS3Keys(string? value, bool expected) =>
        Assert.Equal(expected, StoredObjectUrl.IsPresignableObjectKey(value));
}
