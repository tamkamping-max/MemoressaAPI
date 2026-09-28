using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/photo-tags/library")]
[Authorize]
public class PhotoUserTagLibraryController : ControllerBase
{
    private readonly IPhotoUserTagLibraryService _libraryService;

    public PhotoUserTagLibraryController(IPhotoUserTagLibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLibrary(CancellationToken cancellationToken)
    {
        var result = await _libraryService.GetEntriesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateEntry(
        [FromBody] CreatePhotoUserTagLibraryEntryRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _libraryService.CreateEntryAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, CancellationToken cancellationToken)
    {
        var result = await _libraryService.DeleteEntryAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
