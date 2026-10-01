using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

/// <summary>
/// Maps internal compose/cache codes to MemoressaApp-facing copy and stable JSON slugs.
/// </summary>
public static class TodayMemoriesPresentation
{
    public const string StrategyOnThisDay = "onThisDay";
    public const string StrategyCuratedFlashback = "curatedFlashback";
    public const string StrategyEmpty = "empty";

    public const string ReasonOnThisDay = "onThisDay";
    public const string ReasonCuratedFlashback = "curatedFlashback";
    public const string ReasonExtraPick = "extraPick";

    public static string StrategySlug(TodayMemoriesStrategy strategy) =>
        strategy switch
        {
            TodayMemoriesStrategy.YearsAgoToday => StrategyOnThisDay,
            TodayMemoriesStrategy.RandomFallback => StrategyCuratedFlashback,
            _ => StrategyEmpty
        };

    public static string StrategyLabel(TodayMemoriesStrategy strategy) =>
        strategy switch
        {
            TodayMemoriesStrategy.YearsAgoToday => "往年今日",
            TodayMemoriesStrategy.RandomFallback => "精選回顧",
            _ => "暫無回憶"
        };

    public static TodayMemoriesStrategy StrategyFromSlug(string? slug) =>
        slug switch
        {
            StrategyOnThisDay or "yearsAgoToday" => TodayMemoriesStrategy.YearsAgoToday,
            StrategyCuratedFlashback or "randomFallback" => TodayMemoriesStrategy.RandomFallback,
            _ => TodayMemoriesStrategy.Empty
        };

    public static string ReasonSlug(string internalReason) =>
        internalReason switch
        {
            "yearsAgoToday" => ReasonOnThisDay,
            "randomFallback" => ReasonCuratedFlashback,
            "filler" => ReasonExtraPick,
            _ => internalReason
        };

    public static string ReasonLabel(
        string internalReason,
        int? yearsAgo = null,
        TodayMemoryOccasionKind? occasionKind = null) =>
        internalReason switch
        {
            "yearsAgoToday" when yearsAgo is > 0 => $"{yearsAgo} 年前的今天",
            "yearsAgoToday" => "往年的今天",
            "randomFallback" => "精選回顧",
            "filler" => "更多回憶",
            "birthday" => "生日",
            "friendBirthday" => "好友生日",
            "holiday" => "節日",
            "weather" => "天氣",
            "location" => "地點",
            "travel" => "旅行",
            "occasion" => OccasionKindLabel(occasionKind) ?? "特別時刻",
            _ => internalReason
        };

    public static TodayMemoriesResponseDto Present(TodayMemoriesResponseDto response)
    {
        var items = response.Items
            .Select(item => new TodayMemoryPhotoItemDto
            {
                Photo = item.Photo,
                Reason = ReasonSlug(item.Reason),
                ReasonLabel = ReasonLabel(
                    NormalizeInternalReason(item.Reason),
                    item.YearsAgo,
                    item.OccasionKind),
                YearsAgo = item.YearsAgo,
                OccasionKind = item.OccasionKind
            })
            .ToList();

        return response with
        {
            StrategyLabel = StrategyLabel(response.Strategy),
            Items = items
        };
    }

    private static string NormalizeInternalReason(string reason) =>
        reason switch
        {
            ReasonOnThisDay => "yearsAgoToday",
            ReasonCuratedFlashback => "randomFallback",
            ReasonExtraPick => "filler",
            _ => reason
        };

    private static string? OccasionKindLabel(TodayMemoryOccasionKind? kind) =>
        kind switch
        {
            TodayMemoryOccasionKind.Birthday => "生日",
            TodayMemoryOccasionKind.FriendBirthday => "好友生日",
            TodayMemoryOccasionKind.Holiday => "節日",
            TodayMemoryOccasionKind.Weather => "天氣",
            TodayMemoryOccasionKind.Location => "地點",
            TodayMemoryOccasionKind.Custom => "自訂",
            _ => null
        };
}
