using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record DisplayDeviceDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("qrCode")] public string QrCode { get; init; } = string.Empty;
    [JsonPropertyName("status")] public DisplayDeviceStatus Status { get; init; }
    [JsonPropertyName("currentMemoryId")] public Guid? CurrentMemoryId { get; init; }
    [JsonPropertyName("lastSeen")] public DateTime? LastSeen { get; init; }
}

public record CreateDisplayDeviceRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

public record BindDisplayDeviceRequestDto
{
    [JsonPropertyName("qrCode")] public string QrCode { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public record DisplayDevicePairingStatusDto
{
    [JsonPropertyName("isBound")] public bool IsBound { get; init; }
    [JsonPropertyName("canBind")] public bool CanBind { get; init; }
    [JsonPropertyName("deviceId")] public Guid? DeviceId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public record RegisterDisplayDevicePairingRequestDto
{
    [JsonPropertyName("qrCode")] public string QrCode { get; init; } = string.Empty;
}

public record RenameDisplayDeviceRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
}

public record SendMemoryToDeviceRequestDto
{
    [JsonPropertyName("memoryId")] public Guid MemoryId { get; init; }
    [JsonPropertyName("playNow")] public bool PlayNow { get; init; } = true;
    [JsonPropertyName("packageTitle")] public string? PackageTitle { get; init; }
}

public record SendActivityToDeviceRequestDto
{
    [JsonPropertyName("activityId")] public string ActivityId { get; init; } = string.Empty;
    [JsonPropertyName("playNow")] public bool PlayNow { get; init; } = true;
    [JsonPropertyName("packageTitle")] public string? PackageTitle { get; init; }
}

public record DisplayFrameQueueItemDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("memoryId")] public Guid? MemoryId { get; init; }
    [JsonPropertyName("activityId")] public string? ActivityId { get; init; }
    [JsonPropertyName("commandType")] public FrameCommandType CommandType { get; init; }
    [JsonPropertyName("status")] public FrameCommandStatus Status { get; init; }
    [JsonPropertyName("playNow")] public bool PlayNow { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record FrameDevicePhotoMediaDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
}

public record FrameCommandDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("displayDeviceId")] public Guid DisplayDeviceId { get; init; }
    [JsonPropertyName("commandType")] public FrameCommandType CommandType { get; init; }
    [JsonPropertyName("status")] public FrameCommandStatus Status { get; init; }
    [JsonPropertyName("payloadJson")] public string PayloadJson { get; init; } = "{}";
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record FramePlaybackPackageDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("displayDeviceId")] public Guid DisplayDeviceId { get; init; }
    [JsonPropertyName("familyId")] public Guid FamilyId { get; init; }
    [JsonPropertyName("externalId")] public string? ExternalId { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("packageJson")] public string PackageJson { get; init; } = "{}";
    [JsonPropertyName("isActive")] public bool IsActive { get; init; }
    [JsonPropertyName("sortOrder")] public int SortOrder { get; init; }
}

public record CreatePlaybackPackageRequestDto
{
    [JsonPropertyName("externalId")] public string? ExternalId { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("packageJson")] public string PackageJson { get; init; } = "{}";
    [JsonPropertyName("isActive")] public bool IsActive { get; init; } = true;
    [JsonPropertyName("sortOrder")] public int SortOrder { get; init; }
}

public record EnsurePlaybackPackageRequestDto
{
    [JsonPropertyName("externalId")] public string ExternalId { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("packageJson")] public string PackageJson { get; init; } = "{}";
    [JsonPropertyName("isActive")] public bool IsActive { get; init; } = true;
    [JsonPropertyName("sortOrder")] public int SortOrder { get; init; }
}

public record FrameCommentDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("packageId")] public Guid PackageId { get; init; }
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("authorName")] public string? AuthorName { get; init; }
    [JsonPropertyName("authorAvatarUrl")] public string? AuthorAvatarUrl { get; init; }
}

public record AddFrameCommentRequestDto
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public record UpdateDeviceStatusRequestDto
{
    [JsonPropertyName("deviceId")] public Guid DeviceId { get; init; }
    [JsonPropertyName("status")] public DisplayDeviceStatus Status { get; init; }
    [JsonPropertyName("currentMemoryId")] public Guid? CurrentMemoryId { get; init; }
}

public record AckFrameCommandRequestDto
{
    [JsonPropertyName("success")] public bool Success { get; init; } = true;
    [JsonPropertyName("errorMessage")] public string? ErrorMessage { get; init; }
}
