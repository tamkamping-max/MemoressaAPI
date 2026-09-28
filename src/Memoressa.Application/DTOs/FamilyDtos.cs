using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record FamilyMemberDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("birthDate")] public DateTime? BirthDate { get; init; }
    [JsonPropertyName("generation")] public Generation Generation { get; init; }
    [JsonPropertyName("relationship")] public string? Relationship { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("faceRecognitionEnabled")] public bool FaceRecognitionEnabled { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record CreateFamilyMemberRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("birthDate")] public DateTime? BirthDate { get; init; }
    [JsonPropertyName("generation")] public Generation Generation { get; init; } = Generation.Self;
    [JsonPropertyName("relationship")] public string? Relationship { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("faceRecognitionEnabled")] public bool FaceRecognitionEnabled { get; init; } = true;
}

public record UpdateFamilyMemberRequestDto : CreateFamilyMemberRequestDto;

public record FamilyMomentDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("eventType")] public EventType EventType { get; init; }
    [JsonPropertyName("date")] public DateTime Date { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("isAiDiscovered")] public bool IsAiDiscovered { get; init; }
}

public record CreateFamilyMomentRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("eventType")] public EventType EventType { get; init; }
    [JsonPropertyName("date")] public DateTime Date { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public record UpdateFamilyMomentRequestDto : CreateFamilyMomentRequestDto;
