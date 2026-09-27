using System.Text.Json.Serialization;
using Memoressa.Application.Json;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record PhotoUploaderDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}

public record PhotoDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("assetPath")] public string AssetPath { get; init; } = string.Empty;
    [JsonPropertyName("thumbnailPath")] public string? ThumbnailPath { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; init; }
    [JsonPropertyName("fullUrl")] public string? FullUrl { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("aiTags")] public IReadOnlyList<string> AiTags { get; init; } = [];
    [JsonPropertyName("eventId")] public string? EventId { get; init; }
    [JsonPropertyName("uploadedBy")] public Guid UploadedBy { get; init; }
    [JsonPropertyName("uploaderNickname")] public string? UploaderNickname { get; init; }
    [JsonPropertyName("uploaderEmail")] public string? UploaderEmail { get; init; }
    [JsonPropertyName("uploader")] public PhotoUploaderDto? Uploader { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("isHidden")] public bool IsHidden { get; init; }
    [JsonPropertyName("isDuplicate")] public bool IsDuplicate { get; init; }
    [JsonPropertyName("isSimilar")] public bool IsSimilar { get; init; }
    [JsonPropertyName("isBlurry")] public bool IsBlurry { get; init; }
    [JsonPropertyName("isScreenshot")] public bool IsScreenshot { get; init; }
    [JsonPropertyName("isAiInferred")] public bool IsAiInferred { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility Visibility { get; init; }
    [JsonPropertyName("originalFileName")] public string? OriginalFileName { get; init; }
    [JsonPropertyName("isLivePhoto")] public bool IsLivePhoto { get; init; }
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
    /// <summary>Compressed display file name (e.g. abc.jpg).</summary>
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("contentType")] public string ContentType { get; init; } = "image/jpeg";
    /// <summary>True original file name (e.g. IMG_1234.HEIC). Used for S3 full object and download.</summary>
    [JsonPropertyName("originalFileName")] public string OriginalFileName { get; init; } = string.Empty;
    [JsonPropertyName("originalContentType")] public string OriginalContentType { get; init; } = string.Empty;
    [JsonPropertyName("fileSizeBytes")] public long FileSizeBytes { get; init; }
    /// <summary>When <see cref="IsLivePhoto"/> is true, size of the companion video in bytes (counts toward quota).</summary>
    [JsonPropertyName("livePhotoVideoFileSizeBytes")] public long LivePhotoVideoFileSizeBytes { get; init; }
    /// <summary>User choice: upload Live Photo pair (still + video). When false, only still original is stored and charged.</summary>
    [JsonPropertyName("isLivePhoto")] public bool IsLivePhoto { get; init; }
    [JsonPropertyName("livePhotoVideoFileName")] public string? LivePhotoVideoFileName { get; init; }
    [JsonPropertyName("livePhotoVideoContentType")] public string? LivePhotoVideoContentType { get; init; }
    [JsonPropertyName("mediaKind")] public MediaKind MediaKind { get; init; } = MediaKind.Photo;
    [JsonConverter(typeof(UploadPrivacyScopeJsonConverter))]
    [JsonPropertyName("privacyScope")] public UploadPrivacyScope PrivacyScope { get; init; } = UploadPrivacyScope.Family;
    [JsonPropertyName("sharedAlbumId")] public Guid? SharedAlbumId { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    /// <summary>Link completed upload to an in-progress activity album (external id, e.g. act_...).</summary>
    [JsonPropertyName("activityAlbumId")] public string? ActivityAlbumId { get; init; }
    /// <summary>When true, client uploads only full + thumbnail; compressed display uses the full original object.</summary>
    [JsonPropertyName("compressedUsesFullOriginal")] public bool CompressedUsesFullOriginal { get; init; }
}

/// <summary>Optional metadata applied when the photo row is created on complete.</summary>
public record CompleteUploadRequestDto
{
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
}

public record UploadPartTargetDto
{
    [JsonPropertyName("presignedUrl")] public string PresignedUrl { get; init; } = string.Empty;
    [JsonPropertyName("objectKey")] public string ObjectKey { get; init; } = string.Empty;
}

public record StartUploadResponseDto
{
    [JsonPropertyName("sessionId")] public Guid SessionId { get; init; }
    /// <summary>Presigned PUT and session TTL; App may compute expiry as response time + this value.</summary>
    [JsonPropertyName("presignedUrlExpiryMinutes")] public int PresignedUrlExpiryMinutes { get; init; }
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
    [JsonPropertyName("compressedUsesFullOriginal")] public bool CompressedUsesFullOriginal { get; init; }
    [JsonPropertyName("uploads")] public StartUploadTargetsDto Uploads { get; init; } = new();
}

public record StartUploadTargetsDto
{
    [JsonPropertyName("full")] public UploadPartTargetDto Full { get; init; } = new();
    [JsonPropertyName("compressed")] public UploadPartTargetDto? Compressed { get; init; }
    [JsonPropertyName("thumbnail")] public UploadPartTargetDto Thumbnail { get; init; } = new();
    [JsonPropertyName("livePhotoVideo")] public UploadPartTargetDto? LivePhotoVideo { get; init; }
}

public record StorageUsageDto
{
    [JsonPropertyName("usedBytes")] public long UsedBytes { get; init; }
    [JsonPropertyName("limitBytes")] public long LimitBytes { get; init; }
}

public record IncompleteUploadSessionDto
{
    [JsonPropertyName("sessionId")] public Guid SessionId { get; init; }
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("presignedUrlExpiryMinutes")] public int PresignedUrlExpiryMinutes { get; init; }
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; init; }
    [JsonPropertyName("status")] public UploadSessionStatus Status { get; init; }
    [JsonPropertyName("originalStillFileSizeBytes")] public long OriginalStillFileSizeBytes { get; init; }
    [JsonPropertyName("livePhotoVideoFileSizeBytes")] public long LivePhotoVideoFileSizeBytes { get; init; }
    [JsonPropertyName("quotaReservedBytes")] public long QuotaReservedBytes { get; init; }
    [JsonPropertyName("fullOriginalFileSizeBytes")] public long FullOriginalFileSizeBytes { get; init; }
    [JsonPropertyName("compressedUsesFullOriginal")] public bool CompressedUsesFullOriginal { get; init; }
}
