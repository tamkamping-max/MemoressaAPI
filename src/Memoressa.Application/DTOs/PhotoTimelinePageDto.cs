using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record PhotoTimelinePageDto
{
    [JsonPropertyName("items")] public IReadOnlyList<PhotoDto> Items { get; init; } = [];
    [JsonPropertyName("albumCards")] public IReadOnlyList<PhotoAlbumCardDto> AlbumCards { get; init; } = [];
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; init; }
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
    [JsonIgnore]
    public string? ETag { get; init; }
}
