using System.Text.Json.Serialization;

namespace Memoressa.Application.DTOs;

public record PhotoDownloadDto
{
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; init; } = string.Empty;
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("contentType")] public string? ContentType { get; init; }
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
    [JsonPropertyName("isLivePhoto")] public bool IsLivePhoto { get; init; }
    /// <summary>True when a companion video URL is present. Saving only <see cref="DownloadUrl"/> yields a still image; iOS Live Photo restore requires also fetching <see cref="LivePhotoVideoDownloadUrl"/> and pairing in the client.</summary>
    [JsonPropertyName("livePhotoVideoAvailable")] public bool LivePhotoVideoAvailable { get; init; }
    [JsonPropertyName("livePhotoVideoDownloadUrl")] public string? LivePhotoVideoDownloadUrl { get; init; }
    [JsonPropertyName("livePhotoVideoFileName")] public string? LivePhotoVideoFileName { get; init; }
    [JsonPropertyName("livePhotoVideoContentType")] public string? LivePhotoVideoContentType { get; init; }
    [JsonPropertyName("livePhotoVideoExpiresAt")] public DateTime? LivePhotoVideoExpiresAt { get; init; }
}
