using System.Text.Json.Serialization;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.DTOs;

public record PhotoAlbumPhotoSummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
}

public record PhotoAlbumDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("userTags")] public IReadOnlyList<string> UserTags { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility Visibility { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid> MemberIds { get; init; } = [];
    [JsonPropertyName("coverPhotoId")] public Guid? CoverPhotoId { get; init; }
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public DateTime UpdatedAt { get; init; }
    [JsonPropertyName("createdBy")] public Guid CreatedBy { get; init; }
    [JsonPropertyName("photos")] public IReadOnlyList<PhotoAlbumPhotoSummaryDto>? Photos { get; init; }
    /// <summary>Present on POST when the album row was newly created (false = find-or-create hit existing set).</summary>
    [JsonPropertyName("created")] public bool? Created { get; init; }
}

public record PhotoAlbumCardDto
{
    [JsonPropertyName("albumId")] public Guid AlbumId { get; init; }
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("userTags")] public IReadOnlyList<string> UserTags { get; init; } = [];
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("coverPhotoId")] public Guid? CoverPhotoId { get; init; }
}

public record PhotoAlbumListPageDto
{
    [JsonPropertyName("items")] public IReadOnlyList<PhotoAlbumCardDto> Items { get; init; } = [];
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; init; }
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
}

public record CreatePhotoAlbumRequestDto
{
    [JsonPropertyName("photoIds")] public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    [JsonPropertyName("userTags")] public IReadOnlyList<string>? UserTags { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility? Visibility { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid>? MemberIds { get; init; }
}

public record UpdatePhotoAlbumRequestDto
{
    [JsonPropertyName("userTags")] public IReadOnlyList<string>? UserTags { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("visibility")] public MemoryVisibility? Visibility { get; init; }
    [JsonPropertyName("memberIds")] public IReadOnlyList<Guid>? MemberIds { get; init; }
    [JsonPropertyName("coverPhotoId")] public Guid? CoverPhotoId { get; init; }
}

public record PatchPhotoAlbumPhotosRequestDto
{
    [JsonPropertyName("addPhotoIds")] public IReadOnlyList<Guid>? AddPhotoIds { get; init; }
    [JsonPropertyName("removePhotoIds")] public IReadOnlyList<Guid>? RemovePhotoIds { get; init; }
}

public record PhotoAlbumCommentDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("albumId")] public Guid AlbumId { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; init; }
    [JsonPropertyName("authorId")] public Guid AuthorId { get; init; }
    [JsonPropertyName("userId")] public Guid UserId { get; init; }
    [JsonPropertyName("authorName")] public string? AuthorName { get; init; }
}

public record AddPhotoAlbumCommentRequestDto
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
