using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class PhotoUserTagLibraryService : IPhotoUserTagLibraryService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public PhotoUserTagLibraryService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoUserTagLibraryEntryDto>>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<PhotoUserTagLibraryEntryDto>>.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var entries = await _db.UserPhotoTagLibraryEntries.AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Tag)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<PhotoUserTagLibraryEntryDto>>.Ok(
            entries.Select(e => e.ToDto()).ToList());
    }

    public async Task<ServiceResult<PhotoUserTagLibraryEntryDto>> CreateEntryAsync(
        CreatePhotoUserTagLibraryEntryRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<PhotoUserTagLibraryEntryDto>.Fail("Unauthorized", 401);
        }

        if (!PhotoUserTagRules.TryNormalizeSingle(request.Tag, out var tag))
        {
            return ServiceResult<PhotoUserTagLibraryEntryDto>.Fail("tag is required");
        }

        var userId = _currentUser.UserId.Value;
        var existing = await _db.UserPhotoTagLibraryEntries.AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken);

        var match = existing.FirstOrDefault(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return ServiceResult<PhotoUserTagLibraryEntryDto>.Ok(match.ToDto());
        }

        var entry = new UserPhotoTagLibraryEntry
        {
            UserId = userId,
            Tag = tag
        };

        _db.UserPhotoTagLibraryEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<PhotoUserTagLibraryEntryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult> DeleteEntryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var entry = await _db.UserPhotoTagLibraryEntries.FirstOrDefaultAsync(
            e => e.Id == id && e.UserId == _currentUser.UserId.Value,
            cancellationToken);

        if (entry is null)
        {
            return ServiceResult.NotFound("Photo tag not found");
        }

        _db.UserPhotoTagLibraryEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }
}
