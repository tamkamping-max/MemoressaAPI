using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class JournalTagService : IJournalTagService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public JournalTagService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<JournalTagDto>>> GetTagsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<JournalTagDto>>.Fail("Unauthorized", 401);
        }

        var tags = await _db.JournalTags.AsNoTracking()
            .Where(t => t.OwnerUserId == _currentUser.UserId.Value)
            .OrderBy(t => t.LabelKey)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<JournalTagDto>>.Ok(tags.Select(t => t.ToDto()).ToList());
    }

    public async Task<ServiceResult<JournalTagDto>> CreateTagAsync(
        CreateJournalTagRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<JournalTagDto>.Fail("Unauthorized", 401);
        }

        var labelKey = request.LabelKey.Trim();
        if (string.IsNullOrWhiteSpace(labelKey))
        {
            return ServiceResult<JournalTagDto>.Fail("labelKey is required");
        }

        var tag = new JournalTag
        {
            OwnerUserId = _currentUser.UserId.Value,
            LabelKey = labelKey,
            ColorArgb = request.ColorArgb,
            IsCustom = true
        };

        _db.JournalTags.Add(tag);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<JournalTagDto>.Ok(tag.ToDto());
    }

    public async Task<ServiceResult<JournalTagDto>> UpdateTagAsync(
        Guid id,
        UpdateJournalTagRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<JournalTagDto>.Fail("Unauthorized", 401);
        }

        var tag = await _db.JournalTags.FirstOrDefaultAsync(
            t => t.Id == id && t.OwnerUserId == _currentUser.UserId.Value,
            cancellationToken);

        if (tag is null)
        {
            return ServiceResult<JournalTagDto>.NotFound("Journal tag not found");
        }

        if (request.LabelKey is not null)
        {
            var labelKey = request.LabelKey.Trim();
            if (string.IsNullOrWhiteSpace(labelKey))
            {
                return ServiceResult<JournalTagDto>.Fail("labelKey cannot be empty");
            }

            tag.LabelKey = labelKey;
        }

        if (request.ColorArgb.HasValue)
        {
            tag.ColorArgb = request.ColorArgb.Value;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<JournalTagDto>.Ok(tag.ToDto());
    }

    public async Task<ServiceResult> DeleteTagAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var tag = await _db.JournalTags.FirstOrDefaultAsync(
            t => t.Id == id && t.OwnerUserId == _currentUser.UserId.Value,
            cancellationToken);

        if (tag is null)
        {
            return ServiceResult.NotFound("Journal tag not found");
        }

        _db.JournalTags.Remove(tag);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
