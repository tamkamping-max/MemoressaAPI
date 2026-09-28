using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TodayMemoryOccasionKind
{
    Birthday,
    FriendBirthday,
    Holiday,
    Weather,
    Location,
    Custom
}

public record TodayMemoryOccasionDto
{
    [JsonPropertyName("kind")] public TodayMemoryOccasionKind Kind { get; init; }
    [JsonPropertyName("familyMemberId")] public Guid? FamilyMemberId { get; init; }
    /// <summary>Holiday name, weather summary, friend name, place label, etc.</summary>
    [JsonPropertyName("label")] public string? Label { get; init; }
}

public record TodayMemoriesRequestDto
{
    /// <summary>
    /// Calendar date for &quot;today&quot; (defaults to UTC date).
    /// MemoressaApp: home 今日回憶 and 回憶 tab share this request; use the same <c>date</c> on both.
    /// </summary>
    [JsonPropertyName("date")] public DateOnly? Date { get; init; }
    [JsonPropertyName("currentLocation")] public string? CurrentLocation { get; init; }
    [JsonPropertyName("isTraveling")] public bool IsTraveling { get; init; }
    /// <summary>When traveling, match photos whose location contains this text.</summary>
    [JsonPropertyName("travelLocation")] public string? TravelLocation { get; init; }
    [JsonPropertyName("occasions")] public IReadOnlyList<TodayMemoryOccasionDto> Occasions { get; init; } = [];
}

public record TodayMemoryPhotoItemDto
{
    [JsonPropertyName("photo")] public PhotoDto Photo { get; init; } = null!;
    [JsonPropertyName("reason")] public string Reason { get; init; } = string.Empty;
    [JsonPropertyName("yearsAgo")] public int? YearsAgo { get; init; }
    [JsonPropertyName("occasionKind")] public TodayMemoryOccasionKind? OccasionKind { get; init; }
}

public record TodayMemoriesResponseDto
{
    [JsonPropertyName("items")] public IReadOnlyList<TodayMemoryPhotoItemDto> Items { get; init; } = [];
    [JsonPropertyName("strategy")] public TodayMemoriesStrategy Strategy { get; init; }
    [JsonPropertyName("referenceDate")] public DateOnly ReferenceDate { get; init; }
    /// <summary>True when returning a snapshot created on an earlier call the same calendar day.</summary>
    [JsonPropertyName("fromCache")] public bool FromCache { get; init; }
}
