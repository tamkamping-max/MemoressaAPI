using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record ActivityAgendaItemDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("startDate")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateOnly? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
}

public record ActivityAgendaItemRequestDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("startDate")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateOnly? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
}

public record ActivityAlbumDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("type")] public ActivityAlbumType Type { get; init; }
    [JsonPropertyName("activityType")] public ActivityAlbumType ActivityType => Type;
    [JsonPropertyName("status")] public ActivityAlbumStatus Status { get; init; }
    [JsonPropertyName("startDate")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateOnly? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("creatorUserId")] public Guid CreatorUserId { get; init; }
    [JsonPropertyName("familyMemberIds")] public IReadOnlyList<Guid> FamilyMemberIds { get; init; } = [];
    [JsonPropertyName("friendIds")] public IReadOnlyList<string> FriendIds { get; init; } = [];
    [JsonPropertyName("coverPhotoId")] public Guid? CoverPhotoId { get; init; }
    [JsonPropertyName("privacyScope")] public UploadPrivacyScope PrivacyScope { get; init; } = UploadPrivacyScope.Family;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("agenda")] public IReadOnlyList<ActivityAgendaItemDto> Agenda { get; init; } = [];
}

public record UpsertActivityAlbumRequestDto
{
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("type")] public ActivityAlbumType? Type { get; init; }
    [JsonPropertyName("activityType")] public ActivityAlbumType? ActivityType { get; init; }
    [JsonPropertyName("status")] public ActivityAlbumStatus Status { get; init; } = ActivityAlbumStatus.InProgress;
    [JsonPropertyName("startDate")] public DateOnly StartDate { get; init; }
    [JsonPropertyName("endDate")] public DateOnly? EndDate { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
    [JsonPropertyName("creatorUserId")] public Guid? CreatorUserId { get; init; }
    [JsonPropertyName("familyMemberIds")] public IReadOnlyList<Guid> FamilyMemberIds { get; init; } = [];
    [JsonPropertyName("friendIds")] public IReadOnlyList<string> FriendIds { get; init; } = [];
    [JsonPropertyName("coverPhotoId")] public Guid? CoverPhotoId { get; init; }
    [JsonPropertyName("privacyScope")] public UploadPrivacyScope? PrivacyScope { get; init; }
    [JsonPropertyName("agenda")] public IReadOnlyList<ActivityAgendaItemRequestDto> Agenda { get; init; } = [];
    [JsonPropertyName("createdAt")] public DateTime? CreatedAt { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record ActivityAlbumPhotosRequestDto
{
    /// <summary>GUID or App-style <c>photo_{guid}</c> strings.</summary>
    [JsonPropertyName("photoIds")] public IReadOnlyList<string> PhotoIds { get; init; } = [];
}

public record ActivityPhotoIdsDataDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
}

public record ActivityPhotoPreviewDto
{
    [JsonPropertyName("photoId")] public Guid PhotoId { get; init; }
    [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    [JsonPropertyName("uploadedBy")] public Guid UploadedBy { get; init; }
}

public record ActivityPhotosListDataDto
{
    [JsonPropertyName("items")] public IReadOnlyList<PhotoDto> Items { get; init; } = [];
}

public record ActiveActivityTodayCardDto
{
    [JsonPropertyName("sortRank")] public int SortRank { get; init; }
    [JsonPropertyName("subtitle")] public string Subtitle { get; init; } = string.Empty;
    [JsonPropertyName("activity")] public ActivityAlbumDto Activity { get; init; } = null!;
    [JsonPropertyName("photos")] public IReadOnlyList<ActivityPhotoPreviewDto> Photos { get; init; } = [];
}

public record ApiDataResponseDto<T>
{
    [JsonPropertyName("data")] public T Data { get; init; } = default!;
}

public record ActivityAlbumListDataDto
{
    [JsonPropertyName("items")] public IReadOnlyList<ActivityAlbumDto> Items { get; init; } = [];
}

public record ActivityAlbumListPageDataDto
{
    [JsonPropertyName("items")] public IReadOnlyList<ActivityAlbumDto> Items { get; init; } = [];
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; init; }
}

public record ActiveActivityTodayListDataDto
{
    [JsonPropertyName("strategy")] public string Strategy { get; init; } = "empty";
    [JsonPropertyName("items")] public IReadOnlyList<ActiveActivityTodayCardDto> Items { get; init; } = [];
}
