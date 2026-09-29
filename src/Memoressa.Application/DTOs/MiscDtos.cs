using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record AiAnalysisResultDto
{
    [JsonPropertyName("photoCount")] public int PhotoCount { get; init; }
    [JsonPropertyName("potentialMembers")] public int PotentialMembers { get; init; }
    [JsonPropertyName("potentialMemories")] public int PotentialMemories { get; init; }
    [JsonPropertyName("potentialEvents")] public int PotentialEvents { get; init; }
    [JsonPropertyName("earliestDate")] public DateTime? EarliestDate { get; init; }
    [JsonPropertyName("latestDate")] public DateTime? LatestDate { get; init; }
    [JsonPropertyName("suggestedMemberNames")] public IReadOnlyList<string> SuggestedMemberNames { get; init; } = [];
}

public record AnalyzePhotosRequestDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record SearchResultDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("memoryId")] public Guid? MemoryId { get; init; }
    [JsonPropertyName("photoAlbumId")] public Guid? PhotoAlbumId { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("thumbnailPath")] public string? ThumbnailPath { get; init; }
    [JsonPropertyName("matchReasons")] public IReadOnlyList<string> MatchReasons { get; init; } = [];
    [JsonPropertyName("relevanceScore")] public double RelevanceScore { get; init; }
}

public record PlaybackItemDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("assetPath")] public string AssetPath { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("memoryId")] public Guid? MemoryId { get; init; }
    [JsonPropertyName("memoryWeatherSummary")] public string? MemoryWeatherSummary { get; init; }
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
    [JsonPropertyName("memberNames")] public IReadOnlyList<string> MemberNames { get; init; } = [];
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("transition")] public TransitionType Transition { get; init; } = TransitionType.Fade;
    [JsonPropertyName("displayDurationSeconds")] public int DisplayDurationSeconds { get; init; } = 5;
}

public record PlaybackRequestDto
{
    [JsonPropertyName("aiCurated")] public bool AiCurated { get; init; } = true;
    [JsonPropertyName("memberId")] public Guid? MemberId { get; init; }
    [JsonPropertyName("year")] public int? Year { get; init; }
    [JsonPropertyName("generation")] public Generation? Generation { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid>? PhotoIds { get; init; }
}

public record NotificationDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("isRead")] public bool IsRead { get; init; }
}

public record FriendDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("frameLinked")] public bool FrameLinked { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = "accepted";
    [JsonPropertyName("sharedActivityCount")] public int SharedActivityCount { get; init; }
    [JsonPropertyName("sharedMemoryCount")] public int SharedMemoryCount { get; init; }
}

public record FriendInviteDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("nickname")] public string? Nickname { get; init; }
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
}

public record CreateFriendInviteRequestDto
{
    [JsonPropertyName("email")] public string Email { get; init; } = string.Empty;
}

public record CreateFriendRequestDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("friendUserId")] public Guid? FriendUserId { get; init; }
}

public record UpdateFriendRequestDto
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; init; }
    [JsonPropertyName("frameLinked")] public bool? FrameLinked { get; init; }
}

public record JournalTagDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("labelKey")] public string LabelKey { get; init; } = string.Empty;
    [JsonPropertyName("colorArgb")] public int ColorArgb { get; init; }
    [JsonPropertyName("isCustom")] public bool IsCustom { get; init; } = true;
}

public record CreateJournalTagRequestDto
{
    [JsonPropertyName("labelKey")] public string LabelKey { get; init; } = string.Empty;
    [JsonPropertyName("colorArgb")] public int ColorArgb { get; init; }
}

public record UpdateJournalTagRequestDto
{
    [JsonPropertyName("labelKey")] public string? LabelKey { get; init; }
    [JsonPropertyName("colorArgb")] public int? ColorArgb { get; init; }
}

public record SharedAlbumDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("externalId")] public string ExternalId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("subtitle")] public string? Subtitle { get; init; }
    [JsonPropertyName("albumType")] public SharedAlbumType AlbumType { get; init; }
    [JsonPropertyName("isOwn")] public bool IsOwn { get; init; }
}

public record CreateSharedAlbumRequestDto
{
    [JsonPropertyName("externalId")] public string ExternalId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("subtitle")] public string? Subtitle { get; init; }
    [JsonPropertyName("albumType")] public SharedAlbumType AlbumType { get; init; } = SharedAlbumType.Custom;
}
