using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;

    public ProfileController(IProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpPost("avatars/upload-start")]
    public async Task<IActionResult> StartAvatarUpload(
        [FromBody] AvatarUploadStartRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.StartAvatarUploadAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("avatars/view-url")]
    public async Task<IActionResult> GetAvatarViewUrl(
        [FromQuery] Guid? friendId,
        [FromQuery] Guid? friendUserId,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetAvatarViewUrlAsync(friendId, friendUserId, cancellationToken);
        return result.ToActionResult();
    }
}
