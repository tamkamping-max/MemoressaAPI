using System.Text.Json.Serialization;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.DTOs;

/// <summary>Lightweight photo metadata for feed collages and batch APIs.</summary>
public record PhotoSummaryDto
{
    [JsonPropertyName("id")] public Guid Id { get; init; }
    [JsonPropertyName("thumbnailUrl")] public string? ThumbnailUrl { get; init; }
    [JsonPropertyName("remoteUrl")] public string? RemoteUrl { get; init; }
    [JsonPropertyName("fullUrl")] public string? FullUrl { get; init; }
    [JsonPropertyName("takenAt")] public DateTime? TakenAt { get; init; }
    [JsonPropertyName("uploadedBy")] public Guid UploadedBy { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public record PhotoBatchRequestDto
{
    [JsonPropertyName("ids")] public IReadOnlyList<string> Ids { get; init; } = [];
}

public record PhotoBatchResponseDto
{
    [JsonPropertyName("items")] public IReadOnlyList<PhotoSummaryDto> Items { get; init; } = [];
}

public static class PhotoSummaryMapping
{
    public static PhotoSummaryDto FromPhoto(Photo photo, PhotoDto dto) => new()
    {
        Id = photo.Id,
        ThumbnailUrl = dto.ThumbnailUrl,
        RemoteUrl = dto.RemoteUrl,
        FullUrl = dto.FullUrl,
        TakenAt = photo.TakenAt ?? photo.CreatedAt,
        UploadedBy = dto.UploadedBy
    };

    public static PhotoSummaryDto FromDto(PhotoDto dto) => new()
    {
        Id = dto.Id,
        ThumbnailUrl = dto.ThumbnailUrl,
        RemoteUrl = dto.RemoteUrl,
        FullUrl = dto.FullUrl,
        TakenAt = dto.TakenAt,
        UploadedBy = dto.UploadedBy
    };
}
