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
    /// <summary>Still/original object size when known (DB or S3 HeadObject). Helps App download progress.</summary>
    [JsonPropertyName("contentLength")] public long? ContentLength { get; init; }
    [JsonPropertyName("livePhotoVideoContentLength")] public long? LivePhotoVideoContentLength { get; init; }
}

public record PhotoDownloadBatchRequestDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record PhotoDownloadBatchItemDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("download")] public PhotoDownloadDto? Download { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public record PhotoDownloadBatchResponseDto
{
    [JsonPropertyName("items")] public IReadOnlyList<PhotoDownloadBatchItemDto> Items { get; init; } = [];
}

public record PhotoDeleteBatchRequestDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record PhotoDeleteBatchFailureDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("error")] public string Error { get; init; } = string.Empty;
    [JsonPropertyName("statusCode")] public int StatusCode { get; init; }
}

public record PhotoDeleteBatchResponseDto
{
    [JsonPropertyName("deletedIds")] public IReadOnlyList<Guid> DeletedIds { get; init; } = [];
    [JsonPropertyName("failures")] public IReadOnlyList<PhotoDeleteBatchFailureDto> Failures { get; init; } = [];
}
