using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record PhotoDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("assetPath")] public string AssetPath { get; init; } = string.Empty;
    [JsonPropertyName("thumbnailPath")] public string? ThumbnailPath { get; init; }
    [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("aiTags")] public IReadOnlyList<string> AiTags { get; init; } = [];
    [JsonPropertyName("eventId")] public string? EventId { get; init; }
    [JsonPropertyName("uploadedBy")] public Guid UploadedBy { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("isHidden")] public bool IsHidden { get; init; }
    [JsonPropertyName("isDuplicate")] public bool IsDuplicate { get; init; }
    [JsonPropertyName("isSimilar")] public bool IsSimilar { get; init; }
    [JsonPropertyName("isBlurry")] public bool IsBlurry { get; init; }
    [JsonPropertyName("isScreenshot")] public bool IsScreenshot { get; init; }
    [JsonPropertyName("isAiInferred")] public bool IsAiInferred { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility Visibility { get; init; }
}

public record UpdatePhotoRequestDto
{
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid>? MemberIds { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility? Visibility { get; init; }
    [JsonPropertyName("isHidden")] public bool? IsHidden { get; init; }
}

public record StartUploadRequestDto
{
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("contentType")] public string ContentType { get; init; } = "image/jpeg";
    [JsonPropertyName("fileSizeBytes")] public long FileSizeBytes { get; init; }
    [JsonPropertyName("mediaKind")] public MediaKind MediaKind { get; init; } = MediaKind.Photo;
    [JsonPropertyName("privacyScope")] public UploadPrivacyScope PrivacyScope { get; init; } = UploadPrivacyScope.Family;
    [JsonPropertyName("sharedAlbumId")] public Guid? SharedAlbumId { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
}

public record UploadSessionDto
{
    [JsonPropertyName("sessionId")] public Guid SessionId { get; init; }
    [JsonPropertyName("presignedUrl")] public string PresignedUrl { get; init; } = string.Empty;
    [JsonPropertyName("s3Key")] public string S3Key { get; init; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
}
