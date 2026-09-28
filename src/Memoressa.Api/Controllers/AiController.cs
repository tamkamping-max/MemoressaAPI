using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;

    public AiController(IAiService aiService)
    {
        _aiService = aiService;
    }

    [HttpPost("analyze-photos")]
    public async Task<IActionResult> AnalyzePhotos([FromBody] AnalyzePhotosRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _aiService.AnalyzePhotosAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchMemories([FromQuery] string query, CancellationToken cancellationToken)
    {
        var result = await _aiService.SearchMemoriesAsync(query, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("playback")]
    public async Task<IActionResult> GeneratePlayback([FromBody] PlaybackRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _aiService.GeneratePlaybackAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("memories")]
    public async Task<IActionResult> CreateAiMemory([FromBody] CreateAiMemoryRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _aiService.CreateAiMemoryAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("inferences/{photoId:guid}/confirm")]
    public async Task<IActionResult> ConfirmAiInference(Guid photoId, [FromQuery] Guid memberId, CancellationToken cancellationToken)
    {
        var result = await _aiService.ConfirmAiInferenceAsync(photoId, memberId, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("inferences/{photoId:guid}/reject")]
    public async Task<IActionResult> RejectAiInference(Guid photoId, CancellationToken cancellationToken)
    {
        var result = await _aiService.RejectAiInferenceAsync(photoId, cancellationToken);
        return result.ToActionResult();
    }
}
