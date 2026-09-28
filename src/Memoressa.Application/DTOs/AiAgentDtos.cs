using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record AiAgentChatRequestDto
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("sessionId")] public Guid? SessionId { get; init; }
    [JsonPropertyName("locale")] public string? Locale { get; init; }
}

public record AiAgentChatResponseDto
{
    [JsonPropertyName("sessionId")] public Guid SessionId { get; init; }
    [JsonPropertyName("reply")] public string Reply { get; init; } = string.Empty;
    [JsonPropertyName("memoryId")] public Guid? MemoryId { get; init; }
    [JsonPropertyName("photoId")] public Guid? PhotoId { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("matchReasonKeys")] public IReadOnlyList<string> MatchReasonKeys { get; init; } = [];
    [JsonPropertyName("relatedMemories")] public IReadOnlyList<SearchResultDto> RelatedMemories { get; init; } = [];
}

public record AiAgentMessageDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
    [JsonPropertyName("memoryId")] public Guid? MemoryId { get; init; }
    [JsonPropertyName("photoId")] public Guid? PhotoId { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("matchReasonKeys")] public IReadOnlyList<string> MatchReasonKeys { get; init; } = [];
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record AiAgentSessionDto
{
    [JsonPropertyName("sessionId")] public Guid? SessionId { get; init; }
    [JsonPropertyName("messages")] public IReadOnlyList<AiAgentMessageDto> Messages { get; init; } = [];
}

public static class AiAgentMatchReasons
{
    public static readonly IReadOnlyList<string> AllowedKeys =
    [
        "familyRelation",
        "faceMatch",
        "summer2018",
        "birthdayEvent",
        "childGrowth",
        "familyGathering",
        "locationTokyo",
        "travelEvent",
        "multiGeneration",
        "semantic",
        "aiCurated",
        "highEmotional"
    ];
}
