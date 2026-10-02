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
    private const int PreviewDefaultPhotos = 12;
    private const int PreviewMaxPhotos = 100;
    private const int ActivityPhotosDefaultLimit = 200;
    private const int ActivityPhotosMaxLimit = 500;
    private const int ActiveTodayDefaultLimit = 8;
    private const int ActiveTodayMaxLimit = 20;

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

    public async Task<ServiceResult<ApiDataResponseDto<ActivityAlbumListPageDataDto>>> ListAsync(
        string? status = null,
        string? excludeStatus = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityAlbumListPageDataDto>>.Fail("Unauthorized", 401);
        }

        var includeStatuses = ActivityAlbumListFilters.ParseStatusInclude(status);
        var excludeStatuses = ActivityAlbumListFilters.ParseStatusExclude(excludeStatus);
        var pageSize = ActivityListPagination.NormalizeLimit(limit);

        TimelineCursor? decodedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            decodedCursor = TimelineCursor.TryDecode(cursor);
            if (decodedCursor is null)
            {
                return ServiceResult<ApiDataResponseDto<ActivityAlbumListPageDataDto>>.Fail("Invalid cursor", 400);
            }
        }

        var query = QueryActivities(ctx.Value.FamilyId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .AsQueryable();

        if (includeStatuses.Count > 0)
        {
            query = query.Where(a => includeStatuses.Contains(a.Status));
        }

        if (excludeStatuses.Count > 0)
        {
            query = query.Where(a => !excludeStatuses.Contains(a.Status));
        }

        if (decodedCursor is not null)
        {
            var cursorDate = decodedCursor.SortAtUtc;
            var cursorId = decodedCursor.PhotoId;
            query = query.Where(a =>
                a.CreatedAt < cursorDate
                || (a.CreatedAt == cursorDate && a.Id.CompareTo(cursorId) < 0));
        }

        var page = new List<ActivityAlbumDto>();
        var batchSize = Math.Max(pageSize + 1, 20);
        var skip = 0;

        while (page.Count < pageSize + 1)
        {
            var batch = await query.Skip(skip).Take(batchSize).ToListAsync(cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            skip += batch.Count;
            foreach (var activity in batch)
            {
                if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
                {
                    continue;
                }

                page.Add(ActivityAlbumMapping.ToDto(activity));
                if (page.Count >= pageSize + 1)
                {
                    break;
                }
            }

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        var hasMore = page.Count > pageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        string? nextCursor = null;
        if (hasMore && page.Count > 0)
        {
            var tailExternalId = page[^1].Id;
            var tail = await QueryActivities(ctx.Value.FamilyId)
                .FirstAsync(a => a.ExternalId == tailExternalId, cancellationToken);
            nextCursor = new TimelineCursor(tail.CreatedAt, tail.Id).Encode();
        }

        return ServiceResult<ApiDataResponseDto<ActivityAlbumListPageDataDto>>.Ok(
            new ApiDataResponseDto<ActivityAlbumListPageDataDto>
            {
                Data = new ActivityAlbumListPageDataDto
                {
                    Items = page,
                    NextCursor = nextCursor
                }
            });
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

        var validationError = ActivityAlbumRequestRules.ValidateUpsert(request);
        if (validationError is not null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(validationError, 400);
        }

        var (ok, activityType, _) = ActivityAlbumRequestRules.ResolveType(request);
        if (!ok)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("type is required", 400);
        }

        var creatorUserId = ctx.Value.UserId;
        if (request.CreatorUserId.HasValue && request.CreatorUserId.Value != ctx.Value.UserId)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("creatorUserId must match the authenticated user", 400);
        }

        var activity = new ActivityAlbum
        {
            FamilyId = ctx.Value.FamilyId,
            ExternalId = ActivityAlbumAccess.NewExternalId(),
            Title = request.Title.Trim(),
            Type = activityType,
            Status = request.Status,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Location = request.Location?.Trim(),
            CreatorUserId = creatorUserId,
            CoverPhotoId = request.CoverPhotoId,
            PrivacyScope = request.PrivacyScope ?? UploadPrivacyScope.Family
        };

        if (request.CreatedAt.HasValue)
        {
            activity.CreatedAt = NormalizeClientCreatedAt(request.CreatedAt.Value);
            activity.UpdatedAt = activity.CreatedAt;
        }

        _db.ActivityAlbums.Add(activity);
        var relationError = await ApplyRelationsAsync(activity, request, ctx.Value.UserId, cancellationToken);
        if (relationError is not null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(relationError, 400);
        }

        if (request.PhotoIds.Count > 0)
        {
            await LinkPhotosInternalAsync(activity, request.PhotoIds, ctx.Value.FamilyId, cancellationToken);
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

        var activity = await ActivityAlbumAccess.ResolveForUpdateAsync(_db, ctx.Value.FamilyId, activityId, cancellationToken);
        if (activity is null)
        {
            return ServiceResult<ActivityAlbumDto>.NotFound("Activity not found");
        }

        if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
        {
            return ServiceResult<ActivityAlbumDto>.Fail("Forbidden", 403);
        }

        var validationError = ActivityAlbumRequestRules.ValidateUpsert(request);
        if (validationError is not null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(validationError, 400);
        }

        var (ok, activityType, _) = ActivityAlbumRequestRules.ResolveType(request);
        if (!ok)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("type is required", 400);
        }

        if (request.CreatorUserId.HasValue
            && request.CreatorUserId.Value != activity.CreatorUserId
            && request.CreatorUserId.Value != ctx.Value.UserId)
        {
            return ServiceResult<ActivityAlbumDto>.Fail("creatorUserId cannot be changed to another user", 400);
        }

        activity.Title = request.Title.Trim();
        activity.Type = activityType;
        activity.Status = request.Status;
        activity.StartDate = request.StartDate;
        activity.EndDate = request.EndDate;
        activity.Location = request.Location?.Trim();
        activity.CoverPhotoId = request.CoverPhotoId;
        if (request.PrivacyScope.HasValue)
        {
            activity.PrivacyScope = request.PrivacyScope.Value;
        }

        activity.UpdatedAt = DateTime.UtcNow;

        if (_db.Database.IsRelational())
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var relationError = await ReplaceActivityRelationsAsync(
                    activity,
                    request,
                    activity.CreatorUserId,
                    cancellationToken);
                if (relationError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<ActivityAlbumDto>.Fail(relationError, 400);
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<ActivityAlbumDto>.Fail(
                    "Activity could not be saved because related data changed; refresh and try again",
                    409);
            }
        }
        else
        {
            var relationError = await ReplaceActivityRelationsAsync(
                activity,
                request,
                activity.CreatorUserId,
                cancellationToken);
            if (relationError is not null)
            {
                return ServiceResult<ActivityAlbumDto>.Fail(relationError, 400);
            }

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult<ActivityAlbumDto>.Fail(
                    "Activity could not be saved because related data changed; refresh and try again",
                    409);
            }
        }

        var loaded = await QueryActivities(ctx.Value.FamilyId).FirstAsync(a => a.Id == activity.Id, cancellationToken);
        return ServiceResult<ActivityAlbumDto>.Ok(ActivityAlbumMapping.ToDto(loaded));
    }

    public async Task<ServiceResult> DeleteAsync(string activityId, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var activity = await ActivityAlbumAccess.ResolveForUpdateAsync(
            _db,
            ctx.Value.FamilyId,
            activityId,
            cancellationToken);
        if (activity is null)
        {
            return ServiceResult.NotFound("Activity not found");
        }

        if (activity.CreatorUserId != ctx.Value.UserId)
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        if (_db.Database.IsRelational())
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await DeleteActivityAndLinksAsync(activity, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await DeleteActivityAndLinksAsync(activity, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>> GetActiveTodayAsync(
        DateOnly? date,
        int? limit = null,
        int? photoLimit = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>.Fail("Unauthorized", 401);
        }

        var referenceDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var take = limit ?? ActiveTodayDefaultLimit;
        if (take < 1)
        {
            take = ActiveTodayDefaultLimit;
        }

        if (take > ActiveTodayMaxLimit)
        {
            take = ActiveTodayMaxLimit;
        }

        var previewTake = photoLimit ?? PreviewDefaultPhotos;
        if (previewTake < 1)
        {
            previewTake = PreviewDefaultPhotos;
        }

        if (previewTake > PreviewMaxPhotos)
        {
            previewTake = PreviewMaxPhotos;
        }

        var activities = await QueryActivities(ctx.Value.FamilyId)
            .Where(a => a.Status == ActivityAlbumStatus.InProgress)
            .Where(a => a.StartDate <= referenceDate && (a.EndDate == null || a.EndDate >= referenceDate))
            .ToListAsync(cancellationToken);

        var ranked = new List<(ActivityAlbum Activity, int Tier, int StartProximity)>();

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

            var isCreator = activity.CreatorUserId == ctx.Value.UserId;
            var tier = isCreator ? 0 : 1;
            var startProximity = Math.Abs(referenceDate.DayNumber - activity.StartDate.DayNumber);
            ranked.Add((activity, tier, startProximity));
        }

        var ordered = ranked
            .OrderBy(x => x.Tier)
            .ThenBy(x => x.StartProximity)
            .ThenBy(x => x.Activity.StartDate)
            .ThenBy(x => x.Activity.Title)
            .Take(take)
            .ToList();

        var cards = new List<ActiveActivityTodayCardDto>(ordered.Count);
        var rank = 1;
        foreach (var entry in ordered)
        {
            var previews = await BuildPhotoPreviewsAsync(
                entry.Activity.Id,
                ctx.Value.UserId,
                previewTake,
                cancellationToken);
            cards.Add(new ActiveActivityTodayCardDto
            {
                SortRank = rank++,
                Subtitle = ActivityAlbumMapping.BuildSubtitle(entry.Activity, referenceDate),
                Activity = ActivityAlbumMapping.ToDto(entry.Activity),
                Photos = previews
            });
        }

        var strategy = cards.Count switch
        {
            0 => "empty",
            1 => "singleActive",
            _ => "multiActive"
        };

        return ServiceResult<ApiDataResponseDto<ActiveActivityTodayListDataDto>>.Ok(
            new ApiDataResponseDto<ActiveActivityTodayListDataDto>
            {
                Data = new ActiveActivityTodayListDataDto
                {
                    Strategy = strategy,
                    Items = cards
                }
            });
    }

    public async Task<ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>> GetActivityPhotosAsync(
        string activityId,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.Fail("Unauthorized", 401);
        }

        var activity = await ActivityAlbumAccess.ResolveAsync(_db, ctx.Value.FamilyId, activityId, cancellationToken);
        if (activity is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.NotFound("Activity not found");
        }

        if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, ctx.Value.UserId, cancellationToken))
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.Fail("Forbidden", 403);
        }

        var take = limit ?? ActivityPhotosDefaultLimit;
        if (take < 1)
        {
            take = ActivityPhotosDefaultLimit;
        }

        if (take > ActivityPhotosMaxLimit)
        {
            take = ActivityPhotosMaxLimit;
        }

        var photos = await LoadLinkedPhotosAsync(activity.Id, ctx.Value.UserId, take, cancellationToken);
        var dtos = await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken);

        return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.Ok(
            new ApiDataResponseDto<ActivityPhotosListDataDto>
            {
                Data = new ActivityPhotosListDataDto { Items = dtos }
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

        var (photoIds, parseError) = PhotoReferenceIds.ParseDistinctOrdered(request.PhotoIds);
        if (parseError is not null)
        {
            return ServiceResult.Fail(parseError, 400);
        }

        if (!await ActivityAlbumAccess.CanLinkPhotosAsync(
                _db,
                activity,
                ctx.Value.UserId,
                photoIds,
                cancellationToken))
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        await LinkPhotosInternalAsync(activity, photoIds, ctx.Value.FamilyId, cancellationToken);
        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>> ReplaceActivityPhotosAsync(
        string activityId,
        ActivityAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail("Unauthorized", 401);
        }

        var (photoIds, parseError) = PhotoReferenceIds.ParseDistinctOrdered(request.PhotoIds);
        if (parseError is not null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail(parseError, 400);
        }

        if (photoIds.Count == 0)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail(
                "At least one photo is required",
                400);
        }

        var activity = await ActivityAlbumAccess.ResolveForUpdateAsync(
            _db,
            ctx.Value.FamilyId,
            activityId,
            cancellationToken);
        if (activity is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.NotFound("Activity not found");
        }

        if (activity.CreatorUserId != ctx.Value.UserId)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail("Forbidden", 403);
        }

        var accessible = await ResolveAccessiblePhotoIdsAsync(
            ctx.Value.FamilyId,
            ctx.Value.UserId,
            photoIds,
            cancellationToken);
        if (accessible.Error is not null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail(accessible.Error, 400);
        }

        var desired = accessible.Ids;
        var existingLinks = await _db.ActivityAlbumPhotos
            .Where(ap => ap.ActivityAlbumId == activity.Id)
            .ToListAsync(cancellationToken);

        foreach (var link in existingLinks.Where(l => !desired.Contains(l.PhotoId)))
        {
            _db.ActivityAlbumPhotos.Remove(link);
        }

        var linked = existingLinks.Select(l => l.PhotoId).ToHashSet();
        for (var i = 0; i < desired.Count; i++)
        {
            var photoId = desired[i];
            var row = existingLinks.FirstOrDefault(l => l.PhotoId == photoId);
            if (row is null)
            {
                _db.ActivityAlbumPhotos.Add(new ActivityAlbumPhoto
                {
                    ActivityAlbumId = activity.Id,
                    PhotoId = photoId,
                    SortOrder = i
                });
            }
            else
            {
                row.SortOrder = i;
            }
        }

        activity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Ok(
            new ApiDataResponseDto<ActivityPhotoIdsDataDto>
            {
                Data = new ActivityPhotoIdsDataDto { PhotoIds = desired }
            });
    }

    public async Task LinkPhotoAfterUploadAsync(
        Guid activityAlbumId,
        Guid photoId,
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await StageActivityPhotoLinkAsync(activityAlbumId, photoId, familyId, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task StageActivityPhotoLinkAsync(
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
        activity.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<(List<Guid> Ids, string? Error)> ResolveAccessiblePhotoIdsAsync(
        Guid familyId,
        Guid viewerUserId,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        var photos = await PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking()
                    .Where(p => p.FamilyId == familyId && photoIds.Contains(p.Id) && !p.IsHidden),
                viewerUserId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (photos.Count != photoIds.Count)
        {
            return ([], "One or more photoIds are unknown or inaccessible");
        }

        var order = photoIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var ordered = photos.OrderBy(id => order[id]).ToList();
        return (ordered, null);
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
        Guid viewerUserId,
        int take,
        CancellationToken cancellationToken)
    {
        var photos = await LoadLinkedPhotosAsync(activityId, viewerUserId, take, cancellationToken);
        if (photos.Count == 0)
        {
            return [];
        }

        var dtos = await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken);
        var previews = new List<ActivityPhotoPreviewDto>(dtos.Count);
        for (var i = 0; i < dtos.Count; i++)
        {
            previews.Add(new ActivityPhotoPreviewDto
            {
                PhotoId = photos[i].Id,
                RemoteUrl = dtos[i].RemoteUrl,
                ThumbnailUrl = dtos[i].ThumbnailUrl,
                TakenAt = photos[i].TakenAt ?? photos[i].CreatedAt,
                UploadedBy = dtos[i].UploadedBy
            });
        }

        return previews;
    }

    private async Task<List<Photo>> LoadLinkedPhotosAsync(
        Guid activityId,
        Guid viewerUserId,
        int take,
        CancellationToken cancellationToken)
    {
        var links = await _db.ActivityAlbumPhotos.AsNoTracking()
            .Where(ap => ap.ActivityAlbumId == activityId)
            .OrderByDescending(ap => ap.SortOrder)
            .ThenByDescending(ap => ap.CreatedAt)
            .Take(take * 2)
            .Select(ap => new { ap.PhotoId, ap.SortOrder, ap.CreatedAt })
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return [];
        }

        var photoIds = links.Select(l => l.PhotoId).ToList();
        var photos = await PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking()
                    .Where(p => photoIds.Contains(p.Id) && !p.IsHidden)
                    .Include(p => p.UploadedBy)
                    .Include(p => p.PhotoMembers)
                    .Include(p => p.UserTags)
                    .Include(p => p.AiTags),
                viewerUserId)
            .ToListAsync(cancellationToken);

        var order = links
            .Select((l, index) => (l.PhotoId, Index: index))
            .ToDictionary(x => x.PhotoId, x => x.Index);

        return photos
            .OrderBy(p => order.GetValueOrDefault(p.Id, int.MaxValue))
            .Take(take)
            .ToList();
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
        activity.CreatorUserId != Guid.Empty
        || activity.FamilyMembers.Count > 0
        || activity.Friends.Count > 0;

    private async Task<string?> ReplaceActivityRelationsAsync(
        ActivityAlbum activity,
        UpsertActivityAlbumRequestDto request,
        Guid creatorUserId,
        CancellationToken cancellationToken)
    {
        var albumId = activity.Id;

        // Included join rows must leave the tracker before Clear(); otherwise EF marks them Deleted/Modified
        // and SaveChanges re-issues DELETE/UPDATE after ExecuteDelete already removed DB rows.
        DetachActivityRelationEntries(albumId, includeDeletedState: true);
        activity.AgendaItems.Clear();
        activity.FamilyMembers.Clear();
        activity.Friends.Clear();

        if (_db.Database.IsRelational())
        {
            await _db.ActivityAgendaItems
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.ActivityAlbumFamilyMembers
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.ActivityAlbumFriends
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            await DeleteActivityRelationsViaTrackerAsync(albumId, cancellationToken);
            DetachActivityRelationEntries(albumId, includeDeletedState: false);
        }

        return await ApplyRelationsAsync(activity, request, creatorUserId, cancellationToken);
    }

    private async Task DeleteActivityAndLinksAsync(ActivityAlbum activity, CancellationToken cancellationToken)
    {
        var albumId = activity.Id;
        DetachActivityRelationEntries(albumId, includeDeletedState: true);
        DetachActivityPhotoLinkEntries(albumId);

        if (_db.Database.IsRelational())
        {
            await _db.ActivityAlbumPhotos
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.ActivityAgendaItems
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.ActivityAlbumFamilyMembers
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
            await _db.ActivityAlbumFriends
                .Where(x => x.ActivityAlbumId == albumId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var photoLinks = await _db.ActivityAlbumPhotos
                .Where(x => x.ActivityAlbumId == albumId)
                .ToListAsync(cancellationToken);
            if (photoLinks.Count > 0)
            {
                _db.ActivityAlbumPhotos.RemoveRange(photoLinks);
            }

            await DeleteActivityRelationsViaTrackerAsync(albumId, cancellationToken);
            DetachActivityRelationEntries(albumId, includeDeletedState: false);
        }

        _db.ActivityAlbums.Remove(activity);
    }

    private void DetachActivityPhotoLinkEntries(Guid activityAlbumId)
    {
        if (_db is not DbContext context)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ActivityAlbumPhoto>()
                     .Where(e => e.Entity.ActivityAlbumId == activityAlbumId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task DeleteActivityRelationsViaTrackerAsync(Guid albumId, CancellationToken cancellationToken)
    {
        var agenda = await _db.ActivityAgendaItems
            .Where(x => x.ActivityAlbumId == albumId)
            .ToListAsync(cancellationToken);
        if (agenda.Count > 0)
        {
            _db.ActivityAgendaItems.RemoveRange(agenda);
        }

        var members = await _db.ActivityAlbumFamilyMembers
            .Where(x => x.ActivityAlbumId == albumId)
            .ToListAsync(cancellationToken);
        if (members.Count > 0)
        {
            _db.ActivityAlbumFamilyMembers.RemoveRange(members);
        }

        var friends = await _db.ActivityAlbumFriends
            .Where(x => x.ActivityAlbumId == albumId)
            .ToListAsync(cancellationToken);
        if (friends.Count > 0)
        {
            _db.ActivityAlbumFriends.RemoveRange(friends);
        }
    }

    private void DetachActivityRelationEntries(Guid activityAlbumId, bool includeDeletedState)
    {
        if (_db is not DbContext context)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ActivityAgendaItem>()
                     .Where(e => e.Entity.ActivityAlbumId == activityAlbumId
                                 && (includeDeletedState || e.State != EntityState.Deleted))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in context.ChangeTracker.Entries<ActivityAlbumFamilyMember>()
                     .Where(e => e.Entity.ActivityAlbumId == activityAlbumId
                                 && (includeDeletedState || e.State != EntityState.Deleted))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in context.ChangeTracker.Entries<ActivityAlbumFriend>()
                     .Where(e => e.Entity.ActivityAlbumId == activityAlbumId
                                 && (includeDeletedState || e.State != EntityState.Deleted))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

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
                _db.ActivityAlbumFamilyMembers.Add(new ActivityAlbumFamilyMember
                {
                    ActivityAlbumId = activity.Id,
                    FamilyMemberId = memberId
                });
            }
        }

        var order = 0;
        foreach (var item in request.Agenda ?? [])
        {
            _db.ActivityAgendaItems.Add(new ActivityAgendaItem
            {
                ActivityAlbumId = activity.Id,
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

            _db.ActivityAlbumFriends.Add(new ActivityAlbumFriend
            {
                ActivityAlbumId = activity.Id,
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

    private static DateTime NormalizeClientCreatedAt(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var now = DateTime.UtcNow;
        if (utc > now.AddMinutes(5))
        {
            return now;
        }

        return utc;
    }
}
