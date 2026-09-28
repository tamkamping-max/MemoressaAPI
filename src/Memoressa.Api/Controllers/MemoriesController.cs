using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/memories")]
[Authorize]
public class MemoriesController : ControllerBase
{
    private readonly IMemoryService _memoryService;

    public MemoriesController(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMemories(CancellationToken cancellationToken)
    {
        var result = await _memoryService.GetMemoriesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("ai-curated")]
    public async Task<IActionResult> GetAiCuratedMemories(CancellationToken cancellationToken)
    {
        var result = await _memoryService.GetAiCuratedMemoriesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("today")]
    public async Task<IActionResult> GetTodayMemories(CancellationToken cancellationToken)
    {
        var result = await _memoryService.GetTodayMemoriesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("today/regenerate")]
    public async Task<IActionResult> RegenerateTodayHighlight(CancellationToken cancellationToken)
    {
        var result = await _memoryService.RegenerateTodayHighlightAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("years-ago-today")]
    public async Task<IActionResult> GetYearsAgoTodayMemories(CancellationToken cancellationToken)
    {
        var result = await _memoryService.GetYearsAgoTodayMemoriesAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetMemoryById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _memoryService.GetMemoryByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> CreateMemory([FromBody] CreateMemoryRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _memoryService.CreateMemoryAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMemory(Guid id, [FromBody] UpdateMemoryRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _memoryService.UpdateMemoryAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteMemory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _memoryService.DeleteMemoryAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("filter")]
    public async Task<IActionResult> FilterMemories([FromBody] MemoryFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _memoryService.FilterMemoriesAsync(filter, cancellationToken);
        return result.ToActionResult();
    }
}
