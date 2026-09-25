using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record PhotoDownloadDto
{
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; init; } = string.Empty;
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("contentType")] public string? ContentType { get; init; }
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
}
