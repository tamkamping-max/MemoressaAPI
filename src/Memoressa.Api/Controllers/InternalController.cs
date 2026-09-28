using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/internal")]
[AllowAnonymous]
public class InternalController : ControllerBase
{
    private readonly IInternalRealtimeService _internalRealtimeService;

    public InternalController(IInternalRealtimeService internalRealtimeService)
    {
        _internalRealtimeService = internalRealtimeService;
    }

    [HttpPost("devices/status")]
    public async Task<IActionResult> UpdateDeviceStatus([FromBody] UpdateDeviceStatusRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _internalRealtimeService.UpdateDeviceStatusAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("devices/{deviceId:guid}/commands")]
    public async Task<IActionResult> GetPendingCommands(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _internalRealtimeService.GetPendingCommandsAsync(deviceId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("commands/{commandId:guid}/ack")]
    public async Task<IActionResult> AcknowledgeCommand(Guid commandId, [FromBody] AckFrameCommandRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _internalRealtimeService.AcknowledgeCommandAsync(commandId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("devices/{deviceId:guid}/playback-packages")]
    public async Task<IActionResult> GetDevicePlaybackPackages(Guid deviceId, CancellationToken cancellationToken)
    {
        var result = await _internalRealtimeService.GetDevicePlaybackPackagesAsync(deviceId, cancellationToken);
        return result.ToActionResult();
    }
}
