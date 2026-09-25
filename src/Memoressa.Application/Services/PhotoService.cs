using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class PhotoService : IPhotoService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPhotoUrlResolver _photoUrls;

    public PhotoService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoDto>>.Fail("Unauthorized", 401);
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId).OrderByDescending(p => p.TakenAt).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PhotoDto>>.Ok(await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<PhotoDto>> GetPhotoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDto>.Fail("Unauthorized", 401);
        }

        var photo = await QueryPhotos(ctx.Value.FamilyId).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<PhotoDto>.NotFound("Photo not found");
        }

        return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(photo, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoDto>>.Fail("Unauthorized", 401);
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId)
            .Where(p => p.TakenAt.HasValue && p.TakenAt.Value.Date == date.Date)
            .OrderBy(p => p.TakenAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<PhotoDto>>.Ok(await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosByMemberAsync(
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoDto>>.Fail("Unauthorized", 401);
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId)
            .Where(p => p.PhotoMembers.Any(pm => pm.FamilyMemberId == memberId))
            .OrderByDescending(p => p.TakenAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<PhotoDto>>.Ok(await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<PhotoDto>> UpdatePhotoAsync(
        Guid id,
        UpdatePhotoRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDto>.Fail("Unauthorized", 401);
        }

        var photo = await _db.Photos
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags)
            .FirstOrDefaultAsync(p => p.Id == id && p.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (photo is null)
        {
            return ServiceResult<PhotoDto>.NotFound("Photo not found");
        }

        if (request.Description is not null)
        {
            photo.Description = request.Description;
        }

        if (request.Location is not null)
        {
            photo.Location = request.Location;
        }

        if (request.Visibility.HasValue)
        {
            photo.Visibility = request.Visibility.Value;
        }

        if (request.IsHidden.HasValue)
        {
            photo.IsHidden = request.IsHidden.Value;
        }

        if (request.MemberIds is not null)
        {
            _db.PhotoMembers.RemoveRange(photo.PhotoMembers);
            foreach (var memberId in request.MemberIds)
            {
                _db.PhotoMembers.Add(new Domain.Entities.PhotoMember
                {
                    PhotoId = photo.Id,
                    FamilyMemberId = memberId
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        var updated = await QueryPhotos(ctx.Value.FamilyId).FirstAsync(p => p.Id == id, cancellationToken);
        return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(updated, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult> HidePhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await UpdatePhotoAsync(id, new UpdatePhotoRequestDto { IsHidden = true }, cancellationToken);
        return result.Success ? ServiceResult.Ok() : ServiceResult.Fail(result.Error ?? "Failed", result.StatusCode);
    }

    public async Task<ServiceResult<PhotoTimelinePageDto>> GetTimelinePhotosAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoTimelinePageDto>.Fail("Unauthorized", 401);
        }

        TimelineCursor? decodedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            decodedCursor = TimelineCursor.TryDecode(cursor);
            if (decodedCursor is null)
            {
                return ServiceResult<PhotoTimelinePageDto>.Fail("Invalid cursor", 400);
            }
        }

        var pageSize = PhotoTimelinePagination.NormalizeLimit(limit);
        var takeCount = pageSize + 1;

        var query = QueryPhotos(ctx.Value.FamilyId).Where(p => !p.IsHidden);

        if (decodedCursor is not null)
        {
            var cursorDate = decodedCursor.SortAtUtc;
            var cursorId = decodedCursor.PhotoId;
            query = query.Where(p =>
                (p.TakenAt ?? p.CreatedAt) < cursorDate
                || ((p.TakenAt ?? p.CreatedAt) == cursorDate && p.Id.CompareTo(cursorId) < 0));
        }

        var page = await query
            .OrderByDescending(p => p.TakenAt ?? p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > pageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var last = page[^1];
            nextCursor = new TimelineCursor(last.TakenAt ?? last.CreatedAt, last.Id).Encode();
        }

        var items = await _photoUrls.ToDtosAsync(page, cancellationToken: cancellationToken);
        return ServiceResult<PhotoTimelinePageDto>.Ok(new PhotoTimelinePageDto
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = hasMore
        });
    }

    private IQueryable<Domain.Entities.Photo> QueryPhotos(Guid familyId) =>
        _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId)
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags);
}
