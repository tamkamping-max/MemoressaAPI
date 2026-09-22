using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record MemoryDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("type")] public MemoryType Type { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("videoIds")] public IReadOnlyList<Guid> VideoIds { get; init; } = [];
    [JsonPropertyName("textContent")] public string? TextContent { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("startDate")] public DateTime? StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateTime? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("eventType")] public EventType? EventType { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("isAiGenerated")] public bool IsAiGenerated { get; init; }
    [JsonPropertyName("backgroundMusicId")] public string? BackgroundMusicId { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility Visibility { get; init; }
    [JsonPropertyName("weatherSummary")] public string? WeatherSummary { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record CreateMemoryRequestDto
{
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("type")] public MemoryType Type { get; init; } = MemoryType.Photo;
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("videoIds")] public IReadOnlyList<Guid> VideoIds { get; init; } = [];
    [JsonPropertyName("textContent")] public string? TextContent { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("startDate")] public DateTime? StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateTime? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("eventType")] public EventType? EventType { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("backgroundMusicId")] public string? BackgroundMusicId { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility Visibility { get; init; } = MemoryVisibility.Family;
    [JsonPropertyName("weatherSummary")] public string? WeatherSummary { get; init; }
}

public record UpdateMemoryRequestDto : CreateMemoryRequestDto;

public record MemoryFilterDto
{
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("memberId")] public Guid? MemberId { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("eventType")] public EventType? EventType { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("type")] public MemoryType? Type { get; init; }
}

public record CreateAiMemoryRequestDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}
