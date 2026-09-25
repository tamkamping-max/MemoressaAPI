using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class TodayMemoriesCacheCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public sealed record CachedItem(
        Guid PhotoId,
        string Reason,
        int? YearsAgo,
        TodayMemoryOccasionKind? OccasionKind);

    public static string SerializeEntries(IReadOnlyList<TodayMemoriesComposer.SelectionEntry> entries)
    {
        var payload = entries.Select(i => new CachedItem(
            i.Photo.Id,
            i.Reason,
            i.YearsAgo,
            i.OccasionKind)).ToList();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static string Serialize(IReadOnlyList<TodayMemoryPhotoItemDto> items)
    {
        var payload = items.Select(i => new CachedItem(
            i.Photo.Id,
            i.Reason,
            i.YearsAgo,
            i.OccasionKind)).ToList();

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public static IReadOnlyList<CachedItem> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<CachedItem>>(json, JsonOptions) ?? [];
    }
}
