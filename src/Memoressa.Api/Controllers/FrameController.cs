using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/frame")]
[Authorize]
public class FrameController : ControllerBase
{
    private readonly IFrameService _frameService;

    public FrameController(IFrameService frameService)
    {
        _frameService = frameService;
    }

    [HttpGet("devices/{deviceId:guid}/playback-packages")]
    public async Task<IActionResult> GetPlaybackPackages(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _frameService.GetPlaybackPackagesAsync(deviceId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("devices/{deviceId:guid}/playback-packages")]
    public async Task<IActionResult> CreatePlaybackPackage(Guid deviceId, [FromBody] CreatePlaybackPackageRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _frameService.CreatePlaybackPackageAsync(deviceId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("devices/{deviceId:guid}/playback-packages/ensure")]
    public async Task<IActionResult> EnsurePlaybackPackage(
        Guid deviceId,
        [FromBody] EnsurePlaybackPackageRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _frameService.EnsurePlaybackPackageAsync(deviceId, request, cancellationToken);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("devices/{deviceId:guid}/photos/{photoId:guid}/media")]
    public async Task<IActionResult> GetDevicePhotoMedia(
        Guid deviceId,
        Guid photoId,
        CancellationToken cancellationToken)
    {
        var result = await _frameService.GetDevicePhotoMediaAsync(deviceId, photoId, cancellationToken);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpDelete("devices/{deviceId:guid}/playback-packages/{packageId:guid}")]
    public async Task<IActionResult> DeletePlaybackPackage(
        Guid deviceId,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var result = await _frameService.DeletePlaybackPackageAsync(deviceId, packageId, cancellationToken);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpDelete("devices/{deviceId:guid}/playback-packages/by-memory/{memoryId:guid}")]
    public async Task<IActionResult> DeletePlaybackPackageByMemory(
        Guid deviceId,
        Guid memoryId,
        CancellationToken cancellationToken)
    {
        var result = await _frameService.DeletePlaybackPackageByMemoryAsync(deviceId, memoryId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("playback-packages/{packageId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid packageId, CancellationToken cancellationToken)
    {
        var result = await _frameService.GetCommentsAsync(packageId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("playback-packages/{packageId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid packageId, [FromBody] AddFrameCommentRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _frameService.AddCommentAsync(packageId, request, cancellationToken);
        return result.ToActionResult();
    }
}
