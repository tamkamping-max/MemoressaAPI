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
