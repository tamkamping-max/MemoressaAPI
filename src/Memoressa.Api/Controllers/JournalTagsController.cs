using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/journal-tags")]
[Authorize]
public class JournalTagsController : ControllerBase
{
    private readonly IJournalTagService _journalTagService;

    public JournalTagsController(IJournalTagService journalTagService)
    {
        _journalTagService = journalTagService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTags(CancellationToken cancellationToken)
    {
        var result = await _journalTagService.GetTagsAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateTag(
        [FromBody] CreateJournalTagRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _journalTagService.CreateTagAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTag(
        Guid id,
        [FromBody] UpdateJournalTagRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _journalTagService.UpdateTagAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTag(Guid id, CancellationToken cancellationToken)
    {
        var result = await _journalTagService.DeleteTagAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
