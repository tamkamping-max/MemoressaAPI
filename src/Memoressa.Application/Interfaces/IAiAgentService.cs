using Memoressa.Application.Common;
using Memoressa.Application.DTOs;

namespace Memoressa.Application.Interfaces;

public interface IAiAgentService
{
    Task<ServiceResult<AiAgentChatResponseDto>> ChatAsync(
        AiAgentChatRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<AiAgentMessageDto>>> GetSessionMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AiAgentSessionDto>> GetCurrentSessionAsync(
        CancellationToken cancellationToken = default);
}
