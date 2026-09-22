using Memoressa.Application.DTOs;

namespace Memoressa.Application.Interfaces;

public interface IOpenAiAgentService
{
    Task<AiAgentChatResponseDto> ChatAsync(
        Guid userId,
        Guid familyId,
        AiAgentChatRequestDto request,
        CancellationToken cancellationToken = default);
}
