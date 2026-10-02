using Memoressa.Application.Common;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class ActivityAlbumListFiltersTests
{
    [Fact]
    public void ParseStatusExclude_AcceptsInProgressCamelCase()
    {
        var exclude = ActivityAlbumListFilters.ParseStatusExclude("inProgress");
        Assert.Contains(ActivityAlbumStatus.InProgress, exclude);
    }

    [Fact]
    public void Matches_RespectsIncludeAndExclude()
    {
        var include = ActivityAlbumListFilters.ParseStatusInclude("completed,cancelled");
        var exclude = ActivityAlbumListFilters.ParseStatusExclude("inProgress");

        Assert.True(ActivityAlbumListFilters.Matches(ActivityAlbumStatus.Completed, include, exclude));
        Assert.False(ActivityAlbumListFilters.Matches(ActivityAlbumStatus.InProgress, include, exclude));
        Assert.False(ActivityAlbumListFilters.Matches(ActivityAlbumStatus.InProgress, [], exclude));
    }
}
