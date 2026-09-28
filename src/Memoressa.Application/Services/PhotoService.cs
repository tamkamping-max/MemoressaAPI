using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Application.Services;

public class PhotoService : IPhotoService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPhotoUrlResolver _photoUrls;
    private readonly IS3StorageService _s3;
    private readonly MediaStorageSettings _storageSettings;
    private readonly IPhotoAlbumService _photoAlbums;

    public PhotoService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls,
        IS3StorageService s3,
        IOptions<MediaStorageSettings> storageSettings,
        IPhotoAlbumService photoAlbums)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
        _s3 = s3;
        _storageSettings = storageSettings.Value;
        _photoAlbums = photoAlbums;
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoDto>>.Fail("Unauthorized", 401);
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId).OrderByDescending(p => p.TakenAt).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<PhotoDto>>.Ok(await _photoUrls.ToDtosAsync(photos, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<PhotoDto>> GetPhotoByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDto>.Fail("Unauthorized", 401);
        }

        var photo = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<PhotoDto>.NotFound("Photo not found");
        }

        var dto = await _photoUrls.ToDtoAsync(photo, cancellationToken: cancellationToken);
        var albumContext = await _photoAlbums.TryGetPrimaryAlbumForPhotoAsync(
            photo.Id,
            ctx.Value.FamilyId,
            cancellationToken);

        if (albumContext is not null)
        {
            dto = dto with
            {
                AlbumId = albumContext.Value.AlbumId,
                AlbumUserTags = albumContext.Value.UserTags,
                AlbumDescription = albumContext.Value.Description
            };
        }

        return ServiceResult<PhotoDto>.Ok(dto);
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoDto>>> GetPhotosByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoDto>>.Fail("Unauthorized", 401);
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
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

        var photos = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
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

        if (photo is null || !PhotoViewerAccess.CanView(photo, ctx.Value.UserId))
        {
            return ServiceResult<PhotoDto>.NotFound("Photo not found");
        }

        if (!PhotoViewerAccess.IsUploader(photo, ctx.Value.UserId))
        {
            return ServiceResult<PhotoDto>.Fail("Only the uploader can edit this photo", 403);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        if (request.Description is not null)
        {
            photo.Description = request.Description;
        }

        if (request.Location is not null)
        {
            photo.Location = request.Location;
        }

        if (request.TakenAt.HasValue)
        {
            photo.TakenAt = request.TakenAt;
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
            var distinctMemberIds = request.MemberIds.Distinct().ToList();
            if (distinctMemberIds.Count > 0)
            {
                var validCount = await _db.FamilyMembers.AsNoTracking()
                    .CountAsync(
                        m => m.FamilyId == photo.FamilyId && distinctMemberIds.Contains(m.Id),
                        cancellationToken);

                if (validCount != distinctMemberIds.Count)
                {
                    return ServiceResult<PhotoDto>.Fail(
                        "One or more family members are invalid for this photo",
                        403);
                }
            }

            await ReplacePhotoMembersAsync(photo, distinctMemberIds, cancellationToken);
        }

        var userTagReplace = PhotoUserTagRules.ResolveReplacePayload(request.UserTags, request.AiTags);
        if (userTagReplace is not null)
        {
            await ReplaceUserTagsAsync(photo, userTagReplace, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var updated = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId).FirstAsync(p => p.Id == id, cancellationToken);
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

        var query = QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId).Where(p => !p.IsHidden);

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

    public async Task<ServiceResult<PhotoDownloadDto>> GetOriginalDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDownloadDto>.Fail("Unauthorized", 401);
        }

        var photo = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<PhotoDownloadDto>.NotFound("Photo not found");
        }

        return await BuildOriginalDownloadDtoAsync(photo, cancellationToken);
    }

    public async Task<ServiceResult<PhotoDownloadBatchResponseDto>> GetDownloadBatchAsync(
        PhotoDownloadBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDownloadBatchResponseDto>.Fail("Unauthorized", 401);
        }

        var photoIds = request.PhotoIds?.Distinct().ToList() ?? [];
        if (photoIds.Count == 0)
        {
            return ServiceResult<PhotoDownloadBatchResponseDto>.Fail("photoIds must contain at least one id");
        }

        if (photoIds.Count > PhotoBatchLimits.MaxPhotoIdsPerBatch)
        {
            return ServiceResult<PhotoDownloadBatchResponseDto>.Fail(
                $"At most {PhotoBatchLimits.MaxPhotoIdsPerBatch} photoIds per request");
        }

        var photos = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
            .Where(p => photoIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var photoById = photos.ToDictionary(p => p.Id);
        var items = new List<PhotoDownloadBatchItemDto>();

        foreach (var photoId in photoIds)
        {
            if (!photoById.TryGetValue(photoId, out var photo))
            {
                items.Add(new PhotoDownloadBatchItemDto
                {
                    PhotoId = photoId,
                    Error = "Photo not found"
                });
                continue;
            }

            var downloadResult = await BuildOriginalDownloadDtoAsync(photo, cancellationToken);
            if (!downloadResult.Success || downloadResult.Data is null)
            {
                items.Add(new PhotoDownloadBatchItemDto
                {
                    PhotoId = photoId,
                    Error = downloadResult.Error ?? "Download unavailable"
                });
                continue;
            }

            items.Add(new PhotoDownloadBatchItemDto
            {
                PhotoId = photoId,
                Download = downloadResult.Data
            });
        }

        return ServiceResult<PhotoDownloadBatchResponseDto>.Ok(new PhotoDownloadBatchResponseDto { Items = items });
    }

    public async Task<ServiceResult> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var outcome = await TryDeletePhotoAsync(id, ctx.Value.FamilyId, ctx.Value.UserId, cancellationToken);
        return outcome.Success
            ? ServiceResult.Ok()
            : ServiceResult.Fail(outcome.Error ?? "Delete failed", outcome.StatusCode);
    }

    public async Task<ServiceResult<PhotoDeleteBatchResponseDto>> DeletePhotosBatchAsync(
        PhotoDeleteBatchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDeleteBatchResponseDto>.Fail("Unauthorized", 401);
        }

        var photoIds = request.PhotoIds?.Distinct().ToList() ?? [];
        if (photoIds.Count == 0)
        {
            return ServiceResult<PhotoDeleteBatchResponseDto>.Fail("photoIds must contain at least one id");
        }

        if (photoIds.Count > PhotoBatchLimits.MaxPhotoIdsPerBatch)
        {
            return ServiceResult<PhotoDeleteBatchResponseDto>.Fail(
                $"At most {PhotoBatchLimits.MaxPhotoIdsPerBatch} photoIds per request");
        }

        var deletedIds = new List<Guid>();
        var failures = new List<PhotoDeleteBatchFailureDto>();

        foreach (var photoId in photoIds)
        {
            var outcome = await TryDeletePhotoAsync(photoId, ctx.Value.FamilyId, ctx.Value.UserId, cancellationToken);
            if (outcome.Success)
            {
                deletedIds.Add(photoId);
            }
            else
            {
                failures.Add(new PhotoDeleteBatchFailureDto
                {
                    PhotoId = photoId,
                    Error = outcome.Error ?? "Delete failed",
                    StatusCode = outcome.StatusCode
                });
            }
        }

        return ServiceResult<PhotoDeleteBatchResponseDto>.Ok(new PhotoDeleteBatchResponseDto
        {
            DeletedIds = deletedIds,
            Failures = failures
        });
    }

    public async Task<ServiceResult<TodayMemoriesResponseDto>> GetTodayMemoriesAsync(
        TodayMemoriesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<TodayMemoriesResponseDto>.Fail("Unauthorized", 401);
        }

        var referenceDate = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var cached = await _db.TodayMemoriesCaches.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.FamilyId == ctx.Value.FamilyId && c.CacheDate == referenceDate,
                cancellationToken);

        if (cached is not null)
        {
            var eligibleCount = await CountEligiblePhotosAsync(ctx.Value.FamilyId, ctx.Value.UserId, cancellationToken);
            if (!TodayMemoriesCacheRefresh.ShouldRecomposeEmptyCache(cached, eligibleCount))
            {
                var fromCache = await BuildResponseFromCacheAsync(
                    ctx.Value.FamilyId,
                    ctx.Value.UserId,
                    referenceDate,
                    cached.Strategy,
                    cached.ItemsJson,
                    fromCache: true,
                    cancellationToken);

                return ServiceResult<TodayMemoriesResponseDto>.Ok(fromCache);
            }

            var stale = await _db.TodayMemoriesCaches
                .FirstOrDefaultAsync(
                    c => c.FamilyId == ctx.Value.FamilyId && c.CacheDate == referenceDate,
                    cancellationToken);
            if (stale is not null)
            {
                _db.TodayMemoriesCaches.Remove(stale);
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var built = await ComposeAndPersistTodayMemoriesAsync(
            ctx.Value.FamilyId,
            ctx.Value.UserId,
            referenceDate,
            request,
            cancellationToken);

        return ServiceResult<TodayMemoriesResponseDto>.Ok(built);
    }

    private async Task<TodayMemoriesResponseDto> ComposeAndPersistTodayMemoriesAsync(
        Guid familyId,
        Guid viewerUserId,
        DateOnly referenceDate,
        TodayMemoriesRequestDto request,
        CancellationToken cancellationToken)
    {
        var photos = await QueryPhotos(familyId, viewerUserId)
            .Include(p => p.AiTags)
            .ToListAsync(cancellationToken);

        var birthdayMemberIds = await _db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == familyId && m.BirthDate != null)
            .ToListAsync(cancellationToken);

        var birthdayIds = birthdayMemberIds
            .Where(m =>
            {
                var birth = DateOnly.FromDateTime(m.BirthDate!.Value);
                return birth.Month == referenceDate.Month && birth.Day == referenceDate.Day;
            })
            .Select(m => m.Id)
            .ToList();

        var random = new Random(HashCode.Combine(familyId, referenceDate.Year, referenceDate.Month, referenceDate.Day));
        var (skeleton, entries) = TodayMemoriesComposer.Compose(
            photos,
            referenceDate,
            request,
            birthdayIds,
            random);

        var cache = new TodayMemoriesCache
        {
            FamilyId = familyId,
            CacheDate = referenceDate,
            Strategy = skeleton.Strategy,
            ItemsJson = entries.Count == 0
                ? "[]"
                : TodayMemoriesCacheCodec.SerializeEntries(entries)
        };

        _db.TodayMemoriesCaches.Add(cache);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existing = await _db.TodayMemoriesCaches.AsNoTracking()
                .FirstAsync(
                    c => c.FamilyId == familyId && c.CacheDate == referenceDate,
                    cancellationToken);

            return await BuildResponseFromCacheAsync(
                familyId,
                viewerUserId,
                referenceDate,
                existing.Strategy,
                existing.ItemsJson,
                fromCache: true,
                cancellationToken);
        }

        if (entries.Count == 0)
        {
            return new TodayMemoriesResponseDto
            {
                ReferenceDate = referenceDate,
                Strategy = skeleton.Strategy,
                Items = [],
                FromCache = false
            };
        }

        return await BuildResponseFromEntriesAsync(
            familyId,
            viewerUserId,
            referenceDate,
            skeleton.Strategy,
            entries,
            fromCache: false,
            cancellationToken);
    }

    private async Task<TodayMemoriesResponseDto> BuildResponseFromCacheAsync(
        Guid familyId,
        Guid viewerUserId,
        DateOnly referenceDate,
        TodayMemoriesStrategy strategy,
        string itemsJson,
        bool fromCache,
        CancellationToken cancellationToken)
    {
        var cachedItems = TodayMemoriesCacheCodec.Deserialize(itemsJson);
        if (cachedItems.Count == 0)
        {
            return new TodayMemoriesResponseDto
            {
                ReferenceDate = referenceDate,
                Strategy = strategy,
                Items = [],
                FromCache = fromCache
            };
        }

        var photoIds = cachedItems.Select(i => i.PhotoId).ToList();
        var photosById = await QueryPhotos(familyId, viewerUserId)
            .Where(p => photoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var entries = new List<TodayMemoriesComposer.SelectionEntry>();
        foreach (var item in cachedItems)
        {
            if (!photosById.TryGetValue(item.PhotoId, out var photo) || photo.IsHidden)
            {
                continue;
            }

            if (!PhotoViewerAccess.CanView(photo, viewerUserId))
            {
                continue;
            }

            entries.Add(new TodayMemoriesComposer.SelectionEntry(
                photo,
                item.Reason,
                item.YearsAgo,
                item.OccasionKind));
        }

        return await BuildResponseFromEntriesAsync(
            familyId,
            viewerUserId,
            referenceDate,
            strategy,
            entries,
            fromCache,
            cancellationToken);
    }

    private async Task<TodayMemoriesResponseDto> BuildResponseFromEntriesAsync(
        Guid familyId,
        Guid viewerUserId,
        DateOnly referenceDate,
        TodayMemoriesStrategy strategy,
        IReadOnlyList<TodayMemoriesComposer.SelectionEntry> entries,
        bool fromCache,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return new TodayMemoriesResponseDto
            {
                ReferenceDate = referenceDate,
                Strategy = strategy == TodayMemoriesStrategy.Empty ? strategy : TodayMemoriesStrategy.Empty,
                Items = [],
                FromCache = fromCache
            };
        }

        var visibleEntries = entries
            .Where(e => e.Photo.FamilyId == familyId && PhotoViewerAccess.CanView(e.Photo, viewerUserId))
            .ToList();

        if (visibleEntries.Count == 0)
        {
            return new TodayMemoriesResponseDto
            {
                ReferenceDate = referenceDate,
                Strategy = strategy == TodayMemoriesStrategy.Empty ? strategy : TodayMemoriesStrategy.Empty,
                Items = [],
                FromCache = fromCache
            };
        }

        var photoEntities = visibleEntries.Select(e => e.Photo).ToList();
        var dtos = await _photoUrls.ToDtosAsync(photoEntities, cancellationToken: cancellationToken);
        var items = new List<TodayMemoryPhotoItemDto>(visibleEntries.Count);
        for (var i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            items.Add(new TodayMemoryPhotoItemDto
            {
                Photo = dtos[i],
                Reason = entry.Reason,
                YearsAgo = entry.YearsAgo,
                OccasionKind = entry.OccasionKind
            });
        }

        return new TodayMemoriesResponseDto
        {
            ReferenceDate = referenceDate,
            Strategy = strategy,
            Items = items,
            FromCache = fromCache
        };
    }

    private async Task<(bool Success, string? Error, int StatusCode)> TryDeletePhotoAsync(
        Guid id,
        Guid familyId,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        var photo = await _db.Photos
            .FirstOrDefaultAsync(p => p.Id == id && p.FamilyId == familyId, cancellationToken);

        if (photo is null || !PhotoViewerAccess.CanView(photo, viewerUserId))
        {
            return (false, "Photo not found", 404);
        }

        if (!PhotoViewerAccess.IsUploader(photo, viewerUserId))
        {
            return (false, "Only the uploader can delete this photo", 403);
        }

        var keys = PhotoObjectKeys.CollectDeleteKeys(photo);
        foreach (var key in keys)
        {
            await _s3.DeleteObjectAsync(key, cancellationToken);
        }

        var todayMemoriesCaches = await _db.TodayMemoriesCaches
            .Where(c => c.FamilyId == familyId)
            .ToListAsync(cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var storageBytes = photo.FileSizeBytes ?? 0;
        if (storageBytes > 0)
        {
            var uploader = await _db.UserAccounts.FirstAsync(u => u.Id == photo.UploadedByUserId, cancellationToken);
            uploader.CloudStorageUsedBytes = Math.Max(0, uploader.CloudStorageUsedBytes - storageBytes);
        }

        _db.Photos.Remove(photo);

        PhotoDeletionCleanup.PruneTodayMemoriesCaches(todayMemoriesCaches, id);

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (true, null, 200);
    }

    private async Task<ServiceResult<PhotoDownloadDto>> BuildOriginalDownloadDtoAsync(
        Photo photo,
        CancellationToken cancellationToken)
    {
        var fullKey = PhotoObjectKeys.ResolveFullObjectKey(photo);
        if (string.IsNullOrWhiteSpace(fullKey))
        {
            return ServiceResult<PhotoDownloadDto>.Fail("Photo has no stored original", 404);
        }

        var expiry = TimeSpan.FromMinutes(_storageSettings.DownloadPresignedUrlExpiryMinutes);
        var expiresAt = DateTime.UtcNow.Add(expiry);
        var downloadUrl = await _s3.GetPresignedGetUrlAsync(fullKey, expiry, cancellationToken);
        var fileName = PhotoObjectKeys.GetOriginalDownloadFileName(photo) ?? "photo";
        var contentType = string.IsNullOrWhiteSpace(photo.OriginalContentType)
            ? photo.ContentType ?? "image/jpeg"
            : photo.OriginalContentType;

        long? contentLength = photo.OriginalStillFileSizeBytes ?? photo.FileSizeBytes;
        if (!contentLength.HasValue || contentLength.Value <= 0)
        {
            contentLength = await _s3.GetObjectSizeBytesAsync(fullKey, cancellationToken);
        }

        string? liveVideoUrl = null;
        DateTime? liveVideoExpiresAt = null;
        long? liveVideoContentLength = null;
        if (photo.IsLivePhoto && !string.IsNullOrWhiteSpace(photo.LivePhotoVideoS3Key))
        {
            liveVideoUrl = await _s3.GetPresignedGetUrlAsync(photo.LivePhotoVideoS3Key, expiry, cancellationToken);
            liveVideoExpiresAt = expiresAt;
            liveVideoContentLength = photo.LivePhotoVideoFileSizeBytes > 0
                ? photo.LivePhotoVideoFileSizeBytes
                : await _s3.GetObjectSizeBytesAsync(photo.LivePhotoVideoS3Key, cancellationToken);
        }

        return ServiceResult<PhotoDownloadDto>.Ok(new PhotoDownloadDto
        {
            DownloadUrl = downloadUrl,
            FileName = fileName,
            ContentType = contentType,
            ExpiresAt = expiresAt,
            IsLivePhoto = photo.IsLivePhoto,
            LivePhotoVideoAvailable = photo.IsLivePhoto && !string.IsNullOrWhiteSpace(liveVideoUrl),
            LivePhotoVideoDownloadUrl = liveVideoUrl,
            LivePhotoVideoFileName = photo.LivePhotoVideoFileName,
            LivePhotoVideoContentType = photo.LivePhotoVideoContentType,
            LivePhotoVideoExpiresAt = liveVideoExpiresAt,
            ContentLength = contentLength,
            LivePhotoVideoContentLength = liveVideoContentLength
        });
    }

    private async Task<int> CountEligiblePhotosAsync(
        Guid familyId,
        Guid viewerUserId,
        CancellationToken cancellationToken) =>
        await PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId),
                viewerUserId)
            .CountAsync(
                p => !p.IsHidden
                     && (p.S3Key != null && p.S3Key != "" || p.LocalAssetPath != null && p.LocalAssetPath != ""),
                cancellationToken);

    private IQueryable<Domain.Entities.Photo> QueryPhotos(Guid familyId, Guid viewerUserId) =>
        PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId),
                viewerUserId)
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags)
            .Include(p => p.UploadedBy)
            .Include(p => p.UserTags);

    public async Task<ServiceResult<IReadOnlyList<PhotoCommentDto>>> GetPhotoCommentsAsync(
        Guid photoId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoCommentDto>>.Fail("Unauthorized", 401);
        }

        var photoExists = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
            .AnyAsync(p => p.Id == photoId, cancellationToken);
        if (!photoExists)
        {
            return ServiceResult<IReadOnlyList<PhotoCommentDto>>.NotFound("Photo not found");
        }

        var comments = await _db.PhotoComments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.PhotoId == photoId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<PhotoCommentDto>>.Ok(comments.Select(c => c.ToDto()).ToList());
    }

    public async Task<ServiceResult<PhotoCommentDto>> AddPhotoCommentAsync(
        Guid photoId,
        AddPhotoCommentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoCommentDto>.Fail("Unauthorized", 401);
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0)
        {
            return ServiceResult<PhotoCommentDto>.Fail("message is required");
        }

        if (message.Length > 4000)
        {
            message = message[..4000];
        }

        var photo = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
            .FirstOrDefaultAsync(p => p.Id == photoId, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<PhotoCommentDto>.NotFound("Photo not found");
        }

        var comment = new PhotoComment
        {
            PhotoId = photoId,
            UserId = ctx.Value.UserId,
            Message = message
        };

        _db.PhotoComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.PhotoComments.AsNoTracking()
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id, cancellationToken);

        return ServiceResult<PhotoCommentDto>.Ok(saved.ToDto());
    }

    public async Task<ServiceResult> DeletePhotoCommentAsync(
        Guid photoId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var photoExists = await QueryPhotos(ctx.Value.FamilyId, ctx.Value.UserId)
            .AnyAsync(p => p.Id == photoId, cancellationToken);
        if (!photoExists)
        {
            return ServiceResult.NotFound("Photo not found");
        }

        var comment = await _db.PhotoComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PhotoId == photoId, cancellationToken);
        if (comment is null)
        {
            return ServiceResult.NotFound("Comment not found");
        }

        if (comment.UserId != ctx.Value.UserId)
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        _db.PhotoComments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    private async Task ReplacePhotoMembersAsync(
        Photo photo,
        IReadOnlyList<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PhotoMembers
            .Where(pm => pm.PhotoId == photo.Id)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.PhotoMembers.RemoveRange(existing);
        }

        photo.PhotoMembers.Clear();

        foreach (var memberId in memberIds.Distinct())
        {
            var member = new PhotoMember
            {
                PhotoId = photo.Id,
                FamilyMemberId = memberId
            };
            _db.PhotoMembers.Add(member);
            photo.PhotoMembers.Add(member);
        }
    }

    private async Task ReplaceUserTagsAsync(
        Photo photo,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PhotoUserTags
            .Where(t => t.PhotoId == photo.Id)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.PhotoUserTags.RemoveRange(existing);
        }

        foreach (var tag in tags)
        {
            _db.PhotoUserTags.Add(new PhotoUserTag
            {
                PhotoId = photo.Id,
                Tag = tag
            });
        }
    }
}
