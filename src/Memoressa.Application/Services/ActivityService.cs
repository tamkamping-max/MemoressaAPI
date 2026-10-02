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
    private readonly IAvatarUrlResolver _avatarUrls;

    public ActivityService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls,
        IAvatarUrlResolver avatarUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
        _avatarUrls = avatarUrls;
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

        var accessible = await LoadAccessibleActivitiesAsync(
            ctx.Value.UserId,
            ctx.Value.FamilyId,
            includeStatuses,
            excludeStatuses,
            requireActiveCreator: true,
            cancellationToken);

        var ordered = accessible
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToList();

        if (decodedCursor is not null)
        {
            var cursorDate = decodedCursor.SortAtUtc;
            var cursorId = decodedCursor.PhotoId;
            ordered = ordered
                .Where(a =>
                    a.CreatedAt < cursorDate
                    || (a.CreatedAt == cursorDate && a.Id.CompareTo(cursorId) < 0))
                .ToList();
        }

        var slice = ordered.Take(pageSize + 1).ToList();
        var hasMore = slice.Count > pageSize;
        if (hasMore)
        {
            slice.RemoveAt(slice.Count - 1);
        }

        var creatorSummaries = await LoadCreatorSummariesAsync(slice, cancellationToken);
        var page = slice
            .Select(a => MapActivityDto(a, ctx.Value.UserId, creatorSummaries))
            .ToList();

        string? nextCursor = null;
        if (hasMore && slice.Count > 0)
        {
            var tail = slice[^1];
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

        var activities = await LoadAccessibleActivitiesAsync(
            ctx.Value.UserId,
            ctx.Value.FamilyId,
            includeStatuses: [ActivityAlbumStatus.InProgress],
            excludeStatuses: [],
            requireActiveCreator: true,
            cancellationToken);

        var orderedActivities = activities.OrderByDescending(a => a.StartDate).ToList();
        var creatorSummaries = await LoadCreatorSummariesAsync(orderedActivities, cancellationToken);
        var items = orderedActivities
            .Select(a => MapActivityDto(a, ctx.Value.UserId, creatorSummaries))
            .ToList();

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
            await LinkPhotosInternalAsync(activity, request.PhotoIds, ctx.Value.UserId, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await ReloadActivityGraphAsync(activity.Id, cancellationToken);
        var loaded = await QueryActivities(ctx.Value.FamilyId).FirstAsync(a => a.Id == activity.Id, cancellationToken);
        return ServiceResult<ActivityAlbumDto>.Created(
            await MapActivityDtoAsync(loaded, ctx.Value.UserId, includeCreatorSummary: true, cancellationToken));
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

        var resolved = await ActivityAlbumAccess.ResolveForUpdateAccessibleAsync(
            _db,
            ctx.Value.UserId,
            activityId,
            cancellationToken,
            creatorUserId: request.CreatorUserId);
        if (resolved.Activity is null)
        {
            return ServiceResult<ActivityAlbumDto>.Fail(
                resolved.Error ?? "Activity not found",
                resolved.StatusCode);
        }

        var activity = resolved.Activity;
        if (activity.CreatorUserId != ctx.Value.UserId)
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

        var loaded = await QueryActivitiesGraph().FirstAsync(a => a.Id == activity.Id, cancellationToken);
        return ServiceResult<ActivityAlbumDto>.Ok(
            await MapActivityDtoAsync(loaded, ctx.Value.UserId, includeCreatorSummary: true, cancellationToken));
    }

    public async Task<ServiceResult> DeleteAsync(string activityId, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var resolved = await ActivityAlbumAccess.ResolveForUpdateAccessibleAsync(
            _db,
            ctx.Value.UserId,
            activityId,
            cancellationToken);
        if (resolved.Activity is null)
        {
            return ServiceResult.Fail(resolved.Error ?? "Activity not found", resolved.StatusCode);
        }

        var activity = resolved.Activity;
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

        var activities = await LoadAccessibleActivitiesAsync(
            ctx.Value.UserId,
            ctx.Value.FamilyId,
            includeStatuses: [ActivityAlbumStatus.InProgress],
            excludeStatuses: [],
            requireActiveCreator: true,
            cancellationToken);

        activities = activities
            .Where(a => a.StartDate <= referenceDate && (a.EndDate == null || a.EndDate >= referenceDate))
            .ToList();

        var ranked = new List<(ActivityAlbum Activity, int Tier, int StartProximity)>();

        foreach (var activity in activities)
        {
            SanitizeParticipants(activity, activity.FamilyId);
            if (!HasValidParticipant(activity))
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

        var creatorSummaries = await LoadCreatorSummariesAsync(
            ordered.Select(x => x.Activity).ToList(),
            cancellationToken);

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
                Activity = MapActivityDto(entry.Activity, ctx.Value.UserId, creatorSummaries),
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
        Guid? creatorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.Fail("Unauthorized", 401);
        }

        var resolved = await ActivityAlbumAccess.ResolveAccessibleAsync(
            _db,
            ctx.Value.UserId,
            activityId,
            cancellationToken,
            creatorUserId,
            ActivityAlbumAmbiguityPolicy.FailIfAmbiguous);
        if (resolved.Activity is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotosListDataDto>>.Fail(
                resolved.Error ?? "Activity not found",
                resolved.StatusCode);
        }

        var activity = resolved.Activity;

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

        var resolved = await ActivityAlbumAccess.ResolveForUpdateAccessibleAsync(
            _db,
            ctx.Value.UserId,
            activityId,
            cancellationToken);
        if (resolved.Activity is null)
        {
            return ServiceResult.Fail(resolved.Error ?? "Activity not found", resolved.StatusCode);
        }

        var activity = resolved.Activity;

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

        await LinkPhotosInternalAsync(activity, photoIds, ctx.Value.UserId, cancellationToken);
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

        var resolved = await ActivityAlbumAccess.ResolveForUpdateAccessibleAsync(
            _db,
            ctx.Value.UserId,
            activityId,
            cancellationToken);
        if (resolved.Activity is null)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail(
                resolved.Error ?? "Activity not found",
                resolved.StatusCode);
        }

        var activity = resolved.Activity;
        if (activity.CreatorUserId != ctx.Value.UserId)
        {
            return ServiceResult<ApiDataResponseDto<ActivityPhotoIdsDataDto>>.Fail("Forbidden", 403);
        }

        var accessible = await ResolveAccessiblePhotoIdsAsync(
            activity.FamilyId,
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
            .FirstOrDefaultAsync(a => a.Id == activityAlbumId, cancellationToken);
        if (activity is null || activity.Status != ActivityAlbumStatus.InProgress)
        {
            return;
        }

        var photo = await _db.Photos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return;
        }

        await LinkPhotosInternalAsync(activity, [photoId], photo.UploadedByUserId, cancellationToken);
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
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (photoIds.Count == 0)
        {
            return;
        }

        var validPhotoIds = await PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking()
                    .Where(p => photoIds.Contains(p.Id) && !p.IsHidden),
                viewerUserId)
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
                FullUrl = dtos[i].FullUrl,
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

            if (!friendId.HasValue)
            {
                return "One or more friendIds are invalid for this user";
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

    private async Task<List<ActivityAlbum>> LoadAccessibleActivitiesAsync(
        Guid userId,
        Guid homeFamilyId,
        HashSet<ActivityAlbumStatus> includeStatuses,
        HashSet<ActivityAlbumStatus> excludeStatuses,
        bool requireActiveCreator,
        CancellationToken cancellationToken)
    {
        var home = await QueryActivities(homeFamilyId).ToListAsync(cancellationToken);
        home = home
            .Where(a => ActivityAlbumListFilters.Matches(a.Status, includeStatuses, excludeStatuses))
            .ToList();

        var homeIds = home.Select(a => a.Id).ToHashSet();
        var crossFamilyIds = await QueryCrossFamilyParticipantActivityIdsAsync(userId, homeFamilyId, cancellationToken);
        crossFamilyIds.RemoveWhere(homeIds.Contains);

        var crossFamily = crossFamilyIds.Count == 0
            ? []
            : await QueryActivitiesGraph()
                .Where(a => crossFamilyIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

        crossFamily = crossFamily
            .Where(a => ActivityAlbumListFilters.Matches(a.Status, includeStatuses, excludeStatuses))
            .ToList();

        var merged = home.Concat(crossFamily).GroupBy(a => a.Id).Select(g => g.First()).ToList();
        var result = new List<ActivityAlbum>(merged.Count);

        foreach (var activity in merged)
        {
            if (!await ActivityAlbumAccess.CanAccessAsync(_db, activity, userId, cancellationToken))
            {
                continue;
            }

            if (requireActiveCreator)
            {
                var creatorActive = await _db.UserAccounts.AsNoTracking()
                    .AnyAsync(u => u.Id == activity.CreatorUserId && u.IsActive, cancellationToken);
                if (!creatorActive)
                {
                    continue;
                }
            }

            result.Add(activity);
        }

        return result;
    }

    private async Task<HashSet<Guid>> QueryCrossFamilyParticipantActivityIdsAsync(
        Guid userId,
        Guid homeFamilyId,
        CancellationToken cancellationToken)
    {
        var friendActivityIds = await (
            from af in _db.ActivityAlbumFriends.AsNoTracking()
            join f in _db.Friends.AsNoTracking() on af.FriendId equals f.Id
            join a in _db.ActivityAlbums.AsNoTracking() on af.ActivityAlbumId equals a.Id
            where f.FriendUserId == userId && a.FamilyId != homeFamilyId
            select a.Id).Distinct().ToListAsync(cancellationToken);

        var linkedMemberActivityIds = await (
            from afm in _db.ActivityAlbumFamilyMembers.AsNoTracking()
            join fm in _db.FamilyMembers.AsNoTracking() on afm.FamilyMemberId equals fm.Id
            join a in _db.ActivityAlbums.AsNoTracking() on afm.ActivityAlbumId equals a.Id
            where fm.LinkedUserId == userId && a.FamilyId != homeFamilyId
            select a.Id).Distinct().ToListAsync(cancellationToken);

        var set = new HashSet<Guid>(friendActivityIds);
        set.UnionWith(linkedMemberActivityIds);
        return set;
    }

    private static ActivityAlbumDto MapActivityDto(
        ActivityAlbum activity,
        Guid viewerUserId,
        IReadOnlyDictionary<Guid, ActivityAlbumCreatorSummary> creatorSummaries)
    {
        ActivityAlbumCreatorSummary? creator = null;
        if (creatorSummaries.TryGetValue(activity.CreatorUserId, out var summary))
        {
            creator = summary;
        }

        return ActivityAlbumMapping.ToDto(activity, viewerUserId, creator);
    }

    private async Task<ActivityAlbumDto> MapActivityDtoAsync(
        ActivityAlbum activity,
        Guid viewerUserId,
        bool includeCreatorSummary,
        CancellationToken cancellationToken)
    {
        ActivityAlbumCreatorSummary? creator = null;
        if (includeCreatorSummary)
        {
            var map = await LoadCreatorSummariesAsync([activity], cancellationToken);
            if (map.TryGetValue(activity.CreatorUserId, out var summary))
            {
                creator = summary;
            }
        }

        return ActivityAlbumMapping.ToDto(activity, viewerUserId, creator);
    }

    private async Task<IReadOnlyDictionary<Guid, ActivityAlbumCreatorSummary>> LoadCreatorSummariesAsync(
        IReadOnlyList<ActivityAlbum> activities,
        CancellationToken cancellationToken)
    {
        if (activities.Count == 0)
        {
            return new Dictionary<Guid, ActivityAlbumCreatorSummary>();
        }

        var creatorIds = activities.Select(a => a.CreatorUserId).Distinct().ToList();
        var users = await _db.UserAccounts.AsNoTracking()
            .Where(u => creatorIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, ActivityAlbumCreatorSummary>(users.Count);
        foreach (var user in users)
        {
            var displayName = user.Nickname ?? user.Email ?? string.Empty;
            var avatarUrl = await _avatarUrls.ResolveForResponseAsync(user.AvatarUrl, cancellationToken);
            result[user.Id] = new ActivityAlbumCreatorSummary(displayName, avatarUrl);
        }

        return result;
    }

    private IQueryable<ActivityAlbum> QueryActivitiesGraph() =>
        _db.ActivityAlbums.AsNoTracking()
            .Include(a => a.AgendaItems)
            .Include(a => a.FamilyMembers)
            .ThenInclude(fm => fm.FamilyMember)
            .Include(a => a.Friends)
            .ThenInclude(f => f.Friend);

    private IQueryable<ActivityAlbum> QueryActivities(Guid familyId) =>
        QueryActivitiesGraph().Where(a => a.FamilyId == familyId);

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
