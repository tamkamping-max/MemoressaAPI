using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/display-devices")]
[Authorize]
public class DisplayDevicesController : ControllerBase
{
    private readonly IDisplayDeviceService _displayDeviceService;

    public DisplayDevicesController(IDisplayDeviceService displayDeviceService)
    {
        _displayDeviceService = displayDeviceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices(CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.GetDevicesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDisplayDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.CreateDeviceAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("bind")]
    public async Task<IActionResult> BindDevice([FromBody] BindDisplayDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.BindDeviceAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpGet("pairing-status")]
    public async Task<IActionResult> GetPairingStatus(
        [FromQuery] string qrCode,
        CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.GetPairingStatusAsync(qrCode, cancellationToken);
        return result.ToActionResult();
    }

    [AllowAnonymous]
    [HttpPost("pairing/register")]
    public async Task<IActionResult> RegisterPairingSession(
        [FromBody] RegisterDisplayDevicePairingRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.RegisterPairingSessionAsync(request.QrCode, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/rename")]
    public async Task<IActionResult> RenameDevice(Guid id, [FromBody] RenameDisplayDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.RenameDeviceAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/unbind")]
    public async Task<IActionResult> UnbindDevice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.UnbindDeviceAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{deviceId:guid}/send-memory")]
    public async Task<IActionResult> SendMemoryToDevice(Guid deviceId, [FromBody] SendMemoryToDeviceRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.SendMemoryToDeviceAsync(deviceId, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("qr-code")]
    public async Task<IActionResult> GenerateQrCode(CancellationToken cancellationToken)
    {
        var result = await _displayDeviceService.GenerateQrCodeAsync(cancellationToken);
        return result.ToActionResult();
    }
}
