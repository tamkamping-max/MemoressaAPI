using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class ActivityService : IActivityService
{
    private const int PreviewMinPhotos = 4;
    private const int PreviewMaxPhotos = 12;

    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPhotoUrlResolver _photoUrls;

    public ActivityService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
    }

    public async Task<ServiceResult<ApiDataResponseDto<ActivityAlbumListDataDto>>> GetInProgressAsync(
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityAlbumListDataDto>>.Fail("Unauthorized", 401);
        }

        var activities = await QueryActivities(ctx.Value.FamilyId)
            .Where(a => a.Status == ActivityAlbumStatus.InProgress)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

        var items = new List<ActivityAlbumDto>();
        foreach (var activity in activities)
        {
            if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
            {
                continue;
            }

            var creatorActive = await _db.UserAccounts.AsNoTracking()
                .AnyAsync(u => u.Id == activity.CreatorUserId && u.IsActive, cancellationToken);
            if (!creatorActive)
            {
                continue;
            }

            items.Add(ActivityAlbumMapping.ToDto(activity));
        }

        return ServiceResult<ApiDataResponseDto<ActivityAlbumListDataDto>>.Ok(Wrap(items));
    }

    public async Task<ServiceResult<ActivityAlbumDto>> CreateAsync(
        UpsertActivityAlbumRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("Unauthorized", 401);
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return ServiceResult<ActivityAlbumDto>.Fail("title is required", 400);
        }

        var activity = new ActivityAlbum
        {
            FamilyId = ctx.Value.FamilyId,
            ExternalId = ActivityAlbumAccess.NewExternalId(),
            Title = request.Title.Trim(),
            Type = request.Type,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Location = request.Location?.Trim(),
            CreatorUserId = ctx.Value.UserId,
            CoverPhotoId = request.CoverPhotoId
        };

        _db.ActivityAlbums.Add(activity);
        var relationError = await ApplyRelationsAsync(activity, request, ctx.Value.UserId, cancellationToken);
        if (relationError is not null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(relationError, 400);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ReloadActivityGraphAsync(activity.Id, cancellationToken);
        var loaded = await QueryActivities(ctx.Value.FamilyId).FirstAsync(a => a.Id == activity.Id, cancellationToken);
        return ServiceResult<ActivityAlbumDto>.Created(ActivityAlbumMapping.ToDto(loaded));
    }

    public async Task<ServiceResult<ActivityAlbumDto>> UpdateAsync(
        string activityId,
        UpsertActivityAlbumRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("Unauthorized", 401);
        }

        var activity = await ActivityAlbumAccess.ResolveAsync(_db, ctx.Value.FamilyId, activityId, cancellationToken);
        if (activity is null)
        {
            return ServiceResult<ActivityAlbumDto>.NotFound("Activity not found");
        }

        if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
        {
            return ServiceResult<ActivityAlbumDto>.Fail("Forbidden", 403);
        }

        activity.Title = request.Title.Trim();
        activity.Type = request.Type;
        activity.Status = request.Status;
        activity.StartDate = request.StartDate;
        activity.EndDate = request.EndDate;
        activity.Location = request.Location?.Trim();
        activity.CoverPhotoId = request.CoverPhotoId;
        activity.UpdatedAt = DateTime.UtcNow;

        _db.ActivityAgendaItems.RemoveRange(activity.AgendaItems);
        _db.ActivityAlbumFamilyMembers.RemoveRange(activity.FamilyMembers);
        _db.ActivityAlbumFriends.RemoveRange(activity.Friends);
        activity.AgendaItems.Clear();
        activity.FamilyMembers.Clear();
        activity.Friends.Clear();

        var relationError = await ApplyRelationsAsync(activity, request, activity.CreatorUserId, cancellationToken);
        if (relationError is not null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(relationError, 400);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var loaded = await QueryActivities(ctx.Value.FamilyId).FirstAsync(a => a.Id == activity.Id, cancellationToken);
        return ServiceResult<ActivityAlbumDto>.Ok(ActivityAlbumMapping.ToDto(loaded));
    }

    public async Task<ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>> GetActiveTodayAsync(
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>.Fail("Unauthorized", 401);
        }

        var referenceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var activities = await QueryActivities(ctx.Value.FamilyId)
            .Where(a => a.Status == ActivityAlbumStatus.InProgress)
            .Where(a => a.StartDate <= referenceDate && (a.EndDate == null || a.EndDate >= referenceDate))
            .OrderBy(a => a.StartDate)
            .ToListAsync(cancellationToken);

        var cards = new List<ActiveActivityTodayCardDto>();
        foreach (var activity in activities)
        {
            var creator = await _db.UserAccounts.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == activity.CreatorUserId, cancellationToken);
            if (creator is null || !creator.IsActive)
            {
                continue;
            }

            SanitizeParticipants(activity, ctx.Value.FamilyId);
            if (!HasValidParticipant(activity))
            {
                continue;
            }

            if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
            {
                continue;
            }

            var previews = await BuildPhotoPreviewsAsync(activity.Id, cancellationToken);
            cards.Add(new ActiveActivityTodayCardDto
            {
                Subtitle = ActivityAlbumMapping.BuildSubtitle(activity, referenceDate),
                Activity = ActivityAlbumMapping.ToDto(activity),
                Photos = previews
            });
        }

        return ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>.Ok(
            new ApiDataResponseDto<ActiveActivityTodayListDataDto>
            {
                Data = new ActiveActivityTodayListDataDto { Items = cards }
            });
    }

    public async Task<ServiceResult> AttachPhotosAsync(
        string activityId,
        ActivityAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var activity = await ActivityAlbumAccess.ResolveAsync(_db, ctx.Value.FamilyId, activityId, cancellationToken);
        if (activity is null)
        {
            return ServiceResult.NotFound("Activity not found");
        }

        if (!await ActivityAlbumAccess.CanUploadToAsync(_db, activity, ctx.Value.UserId, cancellationToken))
        {
            return ServiceResult.Fail("Activity is not available for photo uploads", 403);
        }

        await LinkPhotosInternalAsync(activity, request.PhotoIds, ctx.Value.FamilyId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task LinkPhotoAfterUploadAsync(
        Guid activityAlbumId,
        Guid photoId,
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var activity = await _db.ActivityAlbums
            .FirstOrDefaultAsync(a => a.Id == activityAlbumId && a.FamilyId == familyId, cancellationToken);
        if (activity is null || activity.Status != ActivityAlbumStatus.InProgress)
        {
            return;
        }

        await LinkPhotosInternalAsync(activity, [photoId], familyId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task LinkPhotosInternalAsync(
        ActivityAlbum activity,
        IReadOnlyList<Guid> photoIds,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        if (photoIds.Count == 0)
        {
            return;
        }

        var validPhotoIds = await _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId && photoIds.Contains(p.Id) && !p.IsHidden)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var existing = await _db.ActivityAlbumPhotos
            .Where(ap => ap.ActivityAlbumId == activity.Id)
            .Select(ap => ap.PhotoId)
            .ToListAsync(cancellationToken);

        var maxSort = await _db.ActivityAlbumPhotos
            .Where(ap => ap.ActivityAlbumId == activity.Id)
            .Select(ap => (int?)ap.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        foreach (var photoId in validPhotoIds.Where(id => !existing.Contains(id)))
        {
            maxSort++;
            _db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
            {
                ActivityAlbumId = activity.Id,
                PhotoId = photoId,
                SortOrder = maxSort
            });
        }
    }

    private async Task<IReadOnlyList<ActivityPhotoPreviewDto>> BuildPhotoPreviewsAsync(
        Guid activityId,
        CancellationToken cancellationToken)
    {
        var photoIds = await _db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => ap.ActivityAlbumId == activityId)
            .Join(
                _db.Photos.AsNoTracking().Where(p => !p.IsHidden),
                ap => ap.PhotoId,
                p => p.Id,
                (ap, p) => new { ap.SortOrder, Photo = p })
            .OrderByDescending(x => x.Photo.TakenAt ?? x.Photo.CreatedAt)
            .Take(PreviewMaxPhotos)
            .Select(x => x.Photo)
            .ToListAsync(cancellationToken);

        if (photoIds.Count == 0)
        {
            return [];
        }

        var photos = photoIds;
        var dtos = await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken);
        var previews = new List<ActivityPhotoPreviewDto>(dtos.Count);
        for (var i = 0; i < dtos.Count; i++)
        {
            previews.Add(new ActivityPhotoPreviewDto
            {
                PhotoId = photos[i].Id,
                RemoteUrl = dtos[i].RemoteUrl,
                ThumbnailUrl = dtos[i].ThumbnailUrl,
                TakenAt = photos[i].TakenAt ?? photos[i].CreatedAt
            });
        }

        return previews;
    }

    private static void SanitizeParticipants(ActivityAlbum activity, Guid familyId)
    {
        var validMemberIds = activity.FamilyMembers
            .Where(fm => fm.FamilyMember?.FamilyId == familyId)
            .Select(fm => fm.FamilyMemberId)
            .ToHashSet();

        activity.FamilyMembers = activity.FamilyMembers
            .Where(fm => validMemberIds.Contains(fm.FamilyMemberId))
            .ToList();

        activity.Friends = activity.Friends
            .Where(f => f.Friend is null || f.Friend.OwnerUserId == activity.CreatorUserId)
            .ToList();
    }

    private static bool HasValidParticipant(ActivityAlbum activity) =>
        activity.FamilyMembers.Count > 0 || activity.Friends.Count > 0;

    private async Task<string?> ApplyRelationsAsync(
        ActivityAlbum activity,
        UpsertActivityAlbumRequestDto request,
        Guid creatorUserId,
        CancellationToken cancellationToken)
    {
        var memberIds = request.FamilyMemberIds.Distinct().ToList();
        if (memberIds.Count > 0)
        {
            var validCount = await _db.FamilyMembers.CountAsync(
                m => m.FamilyId == activity.FamilyId && memberIds.Contains(m.Id),
                cancellationToken);
            if (validCount != memberIds.Count)
            {
                return "One or more familyMemberIds are invalid for this family";
            }

            foreach (var memberId in memberIds)
            {
                activity.FamilyMembers.Add(new ActivityAlbumFamilyMember { FamilyMemberId = memberId });
            }
        }

        var order = 0;
        foreach (var item in request.Agenda)
        {
            activity.AgendaItems.Add(new ActivityAgendaItem
            {
                ExternalId = string.IsNullOrWhiteSpace(item.Id) ? null : item.Id.Trim(),
                Title = item.Title.Trim(),
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                Location = item.Location?.Trim(),
                SortOrder = order++
            });
        }

        var ownerFriends = await _db.Friends.AsNoTracking()
            .Where(f => f.OwnerUserId == creatorUserId)
            .ToListAsync(cancellationToken);

        foreach (var friendRef in request.FriendIds.Distinct(StringComparer.Ordinal))
        {
            var trimmed = friendRef.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            Guid? friendId = null;
            if (Guid.TryParse(trimmed, out var parsed))
            {
                friendId = ownerFriends.FirstOrDefault(f => f.Id == parsed)?.Id;
            }
            else
            {
                friendId = ownerFriends.FirstOrDefault(f => f.Id.ToString() == trimmed || f.Name == trimmed)?.Id;
            }

            activity.Friends.Add(new ActivityAlbumFriend
            {
                FriendId = friendId,
                FriendReference = trimmed
            });
        }

        if (request.CoverPhotoId.HasValue)
        {
            var coverOk = await _db.Photos.AnyAsync(
                p => p.Id == request.CoverPhotoId.Value && p.FamilyId == activity.FamilyId,
                cancellationToken);
            if (!coverOk)
            {
                return "coverPhotoId is not a valid photo in this family";
            }
        }

        return null;
    }

    private IQueryable<ActivityAlbum> QueryActivities(Guid familyId) =>
        _db.ActivityAlbums.AsNoTracking()
            .Where(a => a.FamilyId == familyId)
            .Include(a => a.AgendaItems)
            .Include(a => a.FamilyMembers)
            .ThenInclude(fm => fm.FamilyMember)
            .Include(a => a.Friends)
            .ThenInclude(f => f.Friend);

    private async Task ReloadActivityGraphAsync(Guid activityId, CancellationToken cancellationToken)
    {
        _ = await _db.ActivityAlbums
            .Include(a => a.AgendaItems)
            .Include(a => a.FamilyMembers)
            .Include(a => a.Friends)
            .FirstAsync(a => a.Id == activityId, cancellationToken);
    }

    private static ApiDataResponseDto<ActivityAlbumListDataDto> Wrap(IReadOnlyList<ActivityAlbumDto> items) =>
        new()
        {
            Data = new ActivityAlbumListDataDto { Items = items }
        };
}
