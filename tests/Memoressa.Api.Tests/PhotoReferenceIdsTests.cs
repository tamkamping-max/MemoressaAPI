using Memoressa.Application.Common;

namespace Memoressa.Api.Tests;

public class PhotoReferenceIdsTests
{
    [Fact]
    public void TryParse_AcceptsGuidAndPhotoPrefix()
    {
        var id = Guid.NewGuid();
        Assert.True(PhotoReferenceIds.TryParse(id.ToString(), out var parsed));
        Assert.Equal(id, parsed);
        Assert.True(PhotoReferenceIds.TryParse($"photo_{id:D}", out parsed));
        Assert.Equal(id, parsed);
    }

    [Fact]
    public void ParseDistinctOrdered_RejectsEmpty()
    {
        var (_, error) = PhotoReferenceIds.ParseDistinctOrdered([]);
        Assert.NotNull(error);
    }
}
