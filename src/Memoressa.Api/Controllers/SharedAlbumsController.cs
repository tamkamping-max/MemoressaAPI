using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/shared-albums")]
[Authorize]
public class SharedAlbumsController : ControllerBase
{
    private readonly ISharedAlbumService _sharedAlbumService;

    public SharedAlbumsController(ISharedAlbumService sharedAlbumService)
    {
        _sharedAlbumService = sharedAlbumService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAlbums(CancellationToken cancellationToken)
    {
        var result = await _sharedAlbumService.GetAlbumsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("external/{externalId}")]
    public async Task<IActionResult> GetAlbumByExternalId(string externalId, CancellationToken cancellationToken)
    {
        var result = await _sharedAlbumService.GetAlbumByExternalIdAsync(externalId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateAlbum([FromBody] CreateSharedAlbumRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _sharedAlbumService.CreateAlbumAsync(request, cancellationToken);
        return result.ToActionResult();
    }
}
