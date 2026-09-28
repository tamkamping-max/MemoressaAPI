using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.Tests;

public class ActivityAlbumRequestRulesTests
{
    [Fact]
    public void ResolveType_AcceptsActivityTypeAlias()
    {
        var request = new UpsertActivityAlbumRequestDto
        {
            Title = "Trip",
            ActivityType = ActivityAlbumType.Other,
            StartDate = new DateOnly(2026, 1, 1)
        };

        var (ok, type, error) = ActivityAlbumRequestRules.ResolveType(request);

        Assert.True(ok);
        Assert.Equal(ActivityAlbumType.Other, type);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateUpsert_RejectsEmptyAgendaTitle()
    {
        var request = new UpsertActivityAlbumRequestDto
        {
            Title = "Trip",
            Type = ActivityAlbumType.Travel,
            StartDate = new DateOnly(2026, 1, 1),
            Agenda = [new ActivityAgendaItemRequestDto { Title = "  ", StartDate = new DateOnly(2026, 1, 2) }]
        };

        var error = ActivityAlbumRequestRules.ValidateUpsert(request);

        Assert.NotNull(error);
    }
}
