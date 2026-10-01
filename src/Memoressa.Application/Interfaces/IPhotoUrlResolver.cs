using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;

namespace Memoressa.Application.Interfaces;

public enum PhotoUrlPurpose
{
    ApiResponse,
    AiProcessing,
    FramePlayback
}

public interface IPhotoUrlResolver
{
    Task<PhotoDto> ToDtoAsync(Photo photo, PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PhotoDto>> ToDtosAsync(
        IEnumerable<Photo> photos,
        PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
        CancellationToken cancellationToken = default);

    Task<string?> GetPresignedUrlAsync(
        Photo photo,
        bool thumbnail = false,
        PhotoUrlPurpose purpose = PhotoUrlPurpose.ApiResponse,
        CancellationToken cancellationToken = default);
}
