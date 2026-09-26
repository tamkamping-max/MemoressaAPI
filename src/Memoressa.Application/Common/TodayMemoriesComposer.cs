using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class TodayMemoriesComposer
{
    public sealed record SelectionEntry(
        Photo Photo,
        string Reason,
        int? YearsAgo = null,
        TodayMemoryOccasionKind? OccasionKind = null);

    public static (TodayMemoriesResponseDto Response, IReadOnlyList<SelectionEntry> Entries) Compose(
        IReadOnlyList<Photo> allPhotos,
        DateOnly referenceDate,
        TodayMemoriesRequestDto request,
        IReadOnlyList<Guid> familyBirthdayMemberIds,
        Random random)
    {
        var eligible = allPhotos
            .Where(TodayMemoriesCacheRefresh.IsEligiblePhoto)
            .ToList();

        if (eligible.Count < TodayMemoriesConstants.MinPhotos)
        {
            return (Empty(referenceDate), []);
        }

        var usedIds = new HashSet<Guid>();
        var yearsAgo = SelectYearsAgoToday(eligible, referenceDate, random, usedIds);
        var contextInserts = SelectContextPhotos(
            eligible,
            referenceDate,
            request,
            familyBirthdayMemberIds,
            random,
            usedIds);
        var travelInserts = SelectTravelPhotos(eligible, request, random, usedIds);
        var merged = Interleave(yearsAgo, contextInserts.Concat(travelInserts).ToList(), random, usedIds);

        TodayMemoriesStrategy strategy;
        if (merged.Count == 0)
        {
            merged = SelectRandomFallback(eligible, random, usedIds);
            strategy = merged.Count >= TodayMemoriesConstants.MinPhotos
                ? TodayMemoriesStrategy.RandomFallback
                : TodayMemoriesStrategy.Empty;
        }
        else
        {
            strategy = TodayMemoriesStrategy.YearsAgoToday;
            if (merged.Count < TodayMemoriesConstants.MinPhotos)
            {
                PadToMinimum(merged, eligible, random, usedIds);
            }
        }

        if (merged.Count < TodayMemoriesConstants.MinPhotos)
        {
            return (Empty(referenceDate), []);
        }

        if (merged.Count > TodayMemoriesConstants.MaxPhotos)
        {
            merged = merged.Take(TodayMemoriesConstants.MaxPhotos).ToList();
        }

        var response = new TodayMemoriesResponseDto
        {
            ReferenceDate = referenceDate,
            Strategy = strategy,
            Items = []
        };

        return (response, merged);
    }

    private static TodayMemoriesResponseDto Empty(DateOnly referenceDate) =>
        new()
        {
            ReferenceDate = referenceDate,
            Strategy = TodayMemoriesStrategy.Empty,
            Items = []
        };

    private static bool HasDisplayableAsset(Photo photo) => TodayMemoriesCacheRefresh.IsEligiblePhoto(photo);

    private static DateOnly EffectiveDate(Photo photo)
    {
        var dt = photo.TakenAt ?? photo.CreatedAt;
        return DateOnly.FromDateTime(dt);
    }

    private static List<SelectionEntry> SelectYearsAgoToday(
        IReadOnlyList<Photo> photos,
        DateOnly referenceDate,
        Random random,
        HashSet<Guid> usedIds)
    {
        var candidates = photos
            .Where(p =>
            {
                var d = EffectiveDate(p);
                return d.Month == referenceDate.Month
                       && d.Day == referenceDate.Day
                       && d.Year < referenceDate.Year;
            })
            .ToList();

        var byYear = candidates
            .GroupBy(p => EffectiveDate(p).Year)
            .OrderBy(g => g.Key)
            .ToList();

        var result = new List<SelectionEntry>();
        foreach (var group in byYear)
        {
            var pool = group.ToList();
            var maxTake = Math.Min(TodayMemoriesConstants.MaxPhotosPerYear, pool.Count);
            var minTake = Math.Min(TodayMemoriesConstants.MinPhotosPerYear, pool.Count);
            var take = pool.Count <= TodayMemoriesConstants.MaxPhotosPerYear
                ? pool.Count
                : random.Next(minTake, maxTake + 1);

            var picked = pool.OrderBy(_ => random.Next()).Take(take).OrderBy(p => EffectiveDate(p)).ThenBy(p => p.Id);
            var yearsAgo = referenceDate.Year - group.Key;
            foreach (var photo in picked)
            {
                if (usedIds.Add(photo.Id))
                {
                    result.Add(new SelectionEntry(photo, "yearsAgoToday", yearsAgo));
                }
            }
        }

        return result;
    }

    private static List<SelectionEntry> SelectContextPhotos(
        IReadOnlyList<Photo> photos,
        DateOnly referenceDate,
        TodayMemoriesRequestDto request,
        IReadOnlyList<Guid> familyBirthdayMemberIds,
        Random random,
        HashSet<Guid> usedIds)
    {
        var pool = new List<(Photo Photo, TodayMemoryOccasionKind Kind)>();

        foreach (var memberId in familyBirthdayMemberIds)
        {
            foreach (var photo in photos.Where(p =>
                         p.PhotoMembers.Any(pm => pm.FamilyMemberId == memberId)))
            {
                pool.Add((photo, TodayMemoryOccasionKind.Birthday));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CurrentLocation))
        {
            foreach (var photo in MatchLocation(photos, request.CurrentLocation))
            {
                pool.Add((photo, TodayMemoryOccasionKind.Location));
            }
        }

        foreach (var occasion in request.Occasions)
        {
            switch (occasion.Kind)
            {
                case TodayMemoryOccasionKind.Birthday when occasion.FamilyMemberId.HasValue:
                    foreach (var photo in photos.Where(p =>
                                 p.PhotoMembers.Any(pm => pm.FamilyMemberId == occasion.FamilyMemberId.Value)))
                    {
                        pool.Add((photo, TodayMemoryOccasionKind.Birthday));
                    }

                    break;
                case TodayMemoryOccasionKind.FriendBirthday:
                case TodayMemoryOccasionKind.Holiday:
                case TodayMemoryOccasionKind.Weather:
                case TodayMemoryOccasionKind.Custom:
                    if (!string.IsNullOrWhiteSpace(occasion.Label))
                    {
                        foreach (var photo in MatchText(photos, occasion.Label))
                        {
                            pool.Add((photo, occasion.Kind));
                        }
                    }

                    break;
                case TodayMemoryOccasionKind.Location:
                    if (!string.IsNullOrWhiteSpace(occasion.Label))
                    {
                        foreach (var photo in MatchLocation(photos, occasion.Label))
                        {
                            pool.Add((photo, TodayMemoryOccasionKind.Location));
                        }
                    }

                    break;
            }
        }

        var distinct = pool
            .GroupBy(x => x.Photo.Id)
            .Select(g => g.First())
            .Where(x => !usedIds.Contains(x.Photo.Id))
            .OrderBy(_ => random.Next())
            .ToList();

        if (distinct.Count == 0)
        {
            return [];
        }

        var maxContext = Math.Min(TodayMemoriesConstants.MaxContextInserts, distinct.Count);
        var minContext = Math.Min(TodayMemoriesConstants.MinContextInserts, distinct.Count);
        var targetCount = random.Next(minContext, maxContext + 1);

        return distinct.Take(targetCount).Select(x =>
                new SelectionEntry(x.Photo, ReasonForKind(x.Kind), OccasionKind: x.Kind))
            .ToList();
    }

    private static List<SelectionEntry> SelectTravelPhotos(
        IReadOnlyList<Photo> photos,
        TodayMemoriesRequestDto request,
        Random random,
        HashSet<Guid> usedIds)
    {
        if (!request.IsTraveling || string.IsNullOrWhiteSpace(request.TravelLocation))
        {
            return [];
        }

        var pool = MatchLocation(photos, request.TravelLocation)
            .Where(p => !usedIds.Contains(p.Id))
            .OrderBy(_ => random.Next())
            .ToList();

        if (pool.Count == 0)
        {
            return [];
        }

        var maxTravel = Math.Min(TodayMemoriesConstants.MaxTravelInserts, pool.Count);
        var minTravel = Math.Min(TodayMemoriesConstants.MinTravelInserts, pool.Count);
        var target = random.Next(minTravel, maxTravel + 1);

        return pool.Take(target).Select(p =>
                new SelectionEntry(p, "travel", OccasionKind: TodayMemoryOccasionKind.Location))
            .ToList();
    }

    private static List<SelectionEntry> SelectRandomFallback(
        IReadOnlyList<Photo> photos,
        Random random,
        HashSet<Guid> usedIds)
    {
        var pool = photos.Where(p => !usedIds.Contains(p.Id)).OrderBy(_ => random.Next()).ToList();
        if (pool.Count < TodayMemoriesConstants.FallbackMinPhotos)
        {
            return [];
        }

        var count = random.Next(
            TodayMemoriesConstants.FallbackMinPhotos,
            Math.Min(TodayMemoriesConstants.FallbackMaxPhotos, pool.Count) + 1);

        return pool.Take(count)
            .Select(p => new SelectionEntry(p, "randomFallback"))
            .ToList();
    }

    private static void PadToMinimum(
        List<SelectionEntry> merged,
        IReadOnlyList<Photo> photos,
        Random random,
        HashSet<Guid> usedIds)
    {
        var pool = photos.Where(p => !usedIds.Contains(p.Id)).OrderBy(_ => random.Next()).ToList();
        foreach (var photo in pool)
        {
            if (merged.Count >= TodayMemoriesConstants.MinPhotos)
            {
                break;
            }

            if (usedIds.Add(photo.Id))
            {
                merged.Add(new SelectionEntry(photo, "filler"));
            }
        }
    }

    private static List<SelectionEntry> Interleave(
        List<SelectionEntry> core,
        List<SelectionEntry> inserts,
        Random random,
        HashSet<Guid> usedIds)
    {
        var result = new List<SelectionEntry>(core);
        foreach (var entry in inserts)
        {
            if (!usedIds.Add(entry.Photo.Id))
            {
                continue;
            }

            var index = result.Count == 0 ? 0 : random.Next(0, result.Count + 1);
            result.Insert(index, entry);
        }

        return result;
    }

    private static IEnumerable<Photo> MatchLocation(IReadOnlyList<Photo> photos, string locationText)
    {
        var needle = locationText.Trim();
        return photos.Where(p =>
            !string.IsNullOrWhiteSpace(p.Location)
            && p.Location.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Photo> MatchText(IReadOnlyList<Photo> photos, string text)
    {
        var needle = text.Trim();
        return photos.Where(p =>
            (!string.IsNullOrWhiteSpace(p.Location)
             && p.Location.Contains(needle, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(p.Description)
                && p.Description.Contains(needle, StringComparison.OrdinalIgnoreCase))
            || p.AiTags.Any(t => t.Tag.Contains(needle, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ReasonForKind(TodayMemoryOccasionKind kind) =>
        kind switch
        {
            TodayMemoryOccasionKind.Birthday => "birthday",
            TodayMemoryOccasionKind.FriendBirthday => "friendBirthday",
            TodayMemoryOccasionKind.Holiday => "holiday",
            TodayMemoryOccasionKind.Weather => "weather",
            TodayMemoryOccasionKind.Location => "location",
            _ => "occasion"
        };
}
