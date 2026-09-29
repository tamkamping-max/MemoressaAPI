using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record AvatarUploadStartRequestDto
{
    [JsonPropertyName("contentType")] public string ContentType { get; init; } = string.Empty;
    [JsonPropertyName("purpose")] public string Purpose { get; init; } = string.Empty;
    [JsonPropertyName("familyMemberId")] public Guid? FamilyMemberId { get; init; }
}

public record AvatarUploadStartResponseDto
{
    [JsonPropertyName("uploadUrl")] public string UploadUrl { get; init; } = string.Empty;
    [JsonPropertyName("avatarUrl")] public string AvatarUrl { get; init; } = string.Empty;
}
