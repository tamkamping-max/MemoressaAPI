using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Api.Tests;

public class TodayMemoriesComposerTests
{
    [Fact]
    public void Compose_PrefersYearsAgoTodayOrderedByYear()
    {
        var refDate = new DateOnly(2026, 3, 15);
        var photos = new List<Photo>
        {
            CreatePhoto(new DateTime(2020, 3, 15), "2020"),
            CreatePhoto(new DateTime(2022, 3, 15), "2022-a"),
            CreatePhoto(new DateTime(2022, 3, 15, 12, 0, 0), "2022-b"),
            CreatePhoto(new DateTime(2024, 3, 15), "2024"),
            CreatePhoto(new DateTime(2026, 1, 1), "other")
        };

        var (_, entries) = TodayMemoriesComposer.Compose(
            photos,
            refDate,
            new TodayMemoriesRequestDto(),
            [],
            new Random(42));

        Assert.True(entries.Count >= 4);
        var anniversary = entries.Where(e => e.Reason == "yearsAgoToday").ToList();
        Assert.NotEmpty(anniversary);
        for (var i = 1; i < anniversary.Count; i++)
        {
            Assert.True(anniversary[i].YearsAgo <= anniversary[i - 1].YearsAgo);
        }
    }

    [Fact]
    public void Compose_ReturnsEmptyWhenNotEnoughPhotos()
    {
        var refDate = new DateOnly(2026, 3, 15);
        var photos = new List<Photo>
        {
            CreatePhoto(new DateTime(2020, 3, 15), "a"),
            CreatePhoto(new DateTime(2021, 3, 15), "b")
        };

        var (response, entries) = TodayMemoriesComposer.Compose(
            photos,
            refDate,
            new TodayMemoriesRequestDto(),
            [],
            new Random(1));

        Assert.Empty(entries);
        Assert.Equal(TodayMemoriesStrategy.Empty, response.Strategy);
    }

    [Fact]
    public void Compose_UsesRandomFallbackWhenNoAnniversaryMatches()
    {
        var refDate = new DateOnly(2026, 3, 15);
        var photos = Enumerable.Range(0, 8)
            .Select(i => CreatePhoto(new DateTime(2025, 1, 1).AddDays(i), $"p{i}"))
            .ToList();

        var (response, entries) = TodayMemoriesComposer.Compose(
            photos,
            refDate,
            new TodayMemoriesRequestDto(),
            [],
            new Random(99));

        Assert.True(entries.Count is >= 4 and <= 10);
        Assert.Equal(TodayMemoriesStrategy.RandomFallback, response.Strategy);
    }

    private static Photo CreatePhoto(DateTime takenAt, string location)
    {
        return new Photo
        {
            Id = Guid.NewGuid(),
            S3Key = $"uploads/{location}.jpg",
            TakenAt = takenAt,
            Location = location,
            CreatedAt = takenAt
        };
    }
}
