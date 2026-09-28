using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class AiService : IAiService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAiOrchestrationService _ai;

    public AiService(IMemoressaDbContext db, ICurrentUserService currentUser, IAiOrchestrationService ai)
    {
        _db = db;
        _currentUser = currentUser;
        _ai = ai;
    }

    public async Task<ServiceResult<AiAnalysisResultDto>> AnalyzePhotosAsync(
        AnalyzePhotosRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<AiAnalysisResultDto>.Fail("Unauthorized", 401);
        }

        var result = await _ai.AnalyzePhotosAsync(ctx.Value.UserId, ctx.Value.FamilyId, request.PhotoIds, cancellationToken);
        return ServiceResult<AiAnalysisResultDto>.Ok(result);
    }

    public async Task<ServiceResult<IReadOnlyList<SearchResultDto>>> SearchMemoriesAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<SearchResultDto>>.Fail("Unauthorized", 401);
        }

        var results = await _ai.SearchMemoriesAsync(ctx.Value.FamilyId, query, cancellationToken);
        return ServiceResult<IReadOnlyList<SearchResultDto>>.Ok(results);
    }

    public async Task<ServiceResult<IReadOnlyList<PlaybackItemDto>>> GeneratePlaybackAsync(
        PlaybackRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PlaybackItemDto>>.Fail("Unauthorized", 401);
        }

        var items = await _ai.GeneratePlaybackAsync(ctx.Value.FamilyId, request, cancellationToken);
        return ServiceResult<IReadOnlyList<PlaybackItemDto>>.Ok(items);
    }

    public async Task<ServiceResult<MemoryDto>> CreateAiMemoryAsync(
        CreateAiMemoryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<MemoryDto>.Fail("Unauthorized", 401);
        }

        try
        {
            var memory = await _ai.CreateAiMemoryAsync(ctx.Value.UserId, ctx.Value.FamilyId, request.PhotoIds, cancellationToken);
            return ServiceResult<MemoryDto>.Ok(memory);
        }
        catch (ApiException ex)
        {
            return ServiceResult<MemoryDto>.Fail(ex.Message, ex.StatusCode);
        }
    }

    public async Task<ServiceResult> ConfirmAiInferenceAsync(
        Guid photoId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var inference = await _db.PhotoAiInferences
            .FirstOrDefaultAsync(i => i.PhotoId == photoId && i.SuggestedMemberId == memberId, cancellationToken);

        if (inference is null)
        {
            return ServiceResult.NotFound("Inference not found");
        }

        inference.Status = AiInferenceStatus.Confirmed;
        var photo = await _db.Photos.Include(p => p.PhotoMembers)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (photo is not null && photo.PhotoMembers.All(pm => pm.FamilyMemberId != memberId))
        {
            _db.PhotoMembers.Add(new Domain.Entities.PhotoMember { PhotoId = photoId, FamilyMemberId = memberId });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RejectAiInferenceAsync(Guid photoId, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var inferences = await _db.PhotoAiInferences.Where(i => i.PhotoId == photoId).ToListAsync(cancellationToken);
        if (inferences.Count == 0)
        {
            return ServiceResult.NotFound("Inference not found");
        }

        foreach (var inference in inferences)
        {
            inference.Status = AiInferenceStatus.Rejected;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
