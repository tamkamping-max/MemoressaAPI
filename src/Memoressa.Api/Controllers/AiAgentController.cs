using Memoressa.Api.Extensions;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Memoressa.Api.Controllers;

[ApiController]
[Route("api/v1/ai/agent")]
[Authorize]
public class AiAgentController : ControllerBase
{
    private readonly IAiAgentService _aiAgentService;

    public AiAgentController(IAiAgentService aiAgentService)
    {
        _aiAgentService = aiAgentService;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] AiAgentChatRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _aiAgentService.ChatAsync(request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("session")]
    public async Task<IActionResult> GetCurrentSession(CancellationToken cancellationToken)
    {
        var result = await _aiAgentService.GetCurrentSessionAsync(cancellationToken);
        return result.ToActionResult();
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await _aiAgentService.GetSessionMessagesAsync(sessionId, cancellationToken);
        return result.ToActionResult();
    }
}
