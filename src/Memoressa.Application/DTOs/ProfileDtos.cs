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

public record AvatarViewUrlResponseDto
{
    [JsonPropertyName("viewUrl")] public string ViewUrl { get; init; } = string.Empty;
    /// <summary>Stable S3 object key when the avatar is stored privately.</summary>
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
}
