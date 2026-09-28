using Memoressa.Infrastructure.Data;

namespace Memoressa.Api.Tests;

public class EfTextSearchTests
{
    [Fact]
    public void ToLikePattern_EscapesWildcards()
    {
        Assert.Equal("%100\\%\\_%", EfTextSearch.ToLikePattern("100%_"));
    }
}
