using System.Text.Json;
using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class AiAgentService : IAiAgentService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IGrokAgentService _grokAgent;
    private readonly IPhotoUrlResolver _photoUrls;

    public AiAgentService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IGrokAgentService grokAgent,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _grokAgent = grokAgent;
        _photoUrls = photoUrls;
    }

    public async Task<ServiceResult<AiAgentChatResponseDto>> ChatAsync(
        AiAgentChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<AiAgentChatResponseDto>.Fail("Unauthorized", 401);
        }

        try
        {
            var response = await _grokAgent.ChatAsync(
                ctx.Value.UserId,
                ctx.Value.FamilyId,
                request,
                cancellationToken);
            return ServiceResult<AiAgentChatResponseDto>.Ok(response);
        }
        catch (ApiException ex)
        {
            return ServiceResult<AiAgentChatResponseDto>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<ServiceResult<IReadOnlyList<AiAgentMessageDto>>> GetSessionMessagesAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<AiAgentMessageDto>>.Fail("Unauthorized", 401);
        }

        var sessionExists = await _db.AiChatSessions.AsNoTracking()
            .AnyAsync(
                s => s.Id == sessionId && s.UserId == ctx.Value.UserId && s.FamilyId == ctx.Value.FamilyId,
                cancellationToken);

        if (!sessionExists)
        {
            return ServiceResult<IReadOnlyList<AiAgentMessageDto>>.NotFound("Chat session not found");
        }

        var messages = await _db.AiChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<AiAgentMessageDto>();
        foreach (var message in messages)
        {
            string? thumbnailUrl = null;
            if (message.PhotoId.HasValue)
            {
                var photo = await _db.Photos.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == message.PhotoId.Value, cancellationToken);
                if (photo is not null)
                {
                    thumbnailUrl = await _photoUrls.GetPresignedUrlAsync(photo, thumbnail: true, cancellationToken: cancellationToken);
                }
            }

            var keys = string.IsNullOrWhiteSpace(message.MatchReasonKeysJson)
                ? []
                : JsonSerializer.Deserialize<List<string>>(message.MatchReasonKeysJson) ?? [];

            dtos.Add(new AiAgentMessageDto
            {
                Id = message.Id,
                Role = message.Role,
                Content = message.Content,
                MemoryId = message.MemoryId,
                PhotoId = message.PhotoId,
                ThumbnailUrl = thumbnailUrl,
                MatchReasonKeys = keys,
                CreatedAt = message.CreatedAt
            });
        }

        return ServiceResult<IReadOnlyList<AiAgentMessageDto>>.Ok(dtos);
    }
}
