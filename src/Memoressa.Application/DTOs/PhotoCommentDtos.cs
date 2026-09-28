using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record PhotoCommentDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("authorId")] public Guid AuthorId { get; init; }
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("authorName")] public string? AuthorName { get; init; }
}

public record AddPhotoCommentRequestDto
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
