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

    public PhotoService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls,
        IS3StorageService s3,
        IOptions<MediaStorageSettings> storageSettings)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
        _s3 = s3;
        _storageSettings = storageSettings.Value;
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

    public async Task<ServiceResult<PhotoDownloadDto>> GetOriginalDownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDownloadDto>.Fail("Unauthorized", 401);
        }

        var photo = await QueryPhotos(ctx.Value.FamilyId).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<PhotoDownloadDto>.NotFound("Photo not found");
        }

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

        string? liveVideoUrl = null;
        DateTime? liveVideoExpiresAt = null;
        if (photo.IsLivePhoto && !string.IsNullOrWhiteSpace(photo.LivePhotoVideoS3Key))
        {
            liveVideoUrl = await _s3.GetPresignedGetUrlAsync(photo.LivePhotoVideoS3Key, expiry, cancellationToken);
            liveVideoExpiresAt = expiresAt;
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
            LivePhotoVideoExpiresAt = liveVideoExpiresAt
        });
    }

    public async Task<ServiceResult> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var photo = await _db.Photos
            .FirstOrDefaultAsync(p => p.Id == id && p.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (photo is null)
        {
            return ServiceResult.NotFound("Photo not found");
        }

        var keys = PhotoObjectKeys.CollectDeleteKeys(photo);
        foreach (var key in keys)
        {
            await _s3.DeleteObjectAsync(key, cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var storageBytes = photo.FileSizeBytes ?? 0;
        if (storageBytes > 0)
        {
            var uploader = await _db.UserAccounts.FirstAsync(u => u.Id == photo.UploadedByUserId, cancellationToken);
            uploader.CloudStorageUsedBytes = Math.Max(0, uploader.CloudStorageUsedBytes - storageBytes);
        }

        _db.Photos.Remove(photo);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult.Ok();
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
            var fromCache = await BuildResponseFromCacheAsync(
                ctx.Value.FamilyId,
                referenceDate,
                cached.Strategy,
                cached.ItemsJson,
                fromCache: true,
                cancellationToken);

            return ServiceResult<TodayMemoriesResponseDto>.Ok(fromCache);
        }

        var built = await ComposeAndPersistTodayMemoriesAsync(
            ctx.Value.FamilyId,
            referenceDate,
            request,
            cancellationToken);

        return ServiceResult<TodayMemoriesResponseDto>.Ok(built);
    }

    private async Task<TodayMemoriesResponseDto> ComposeAndPersistTodayMemoriesAsync(
        Guid familyId,
        DateOnly referenceDate,
        TodayMemoriesRequestDto request,
        CancellationToken cancellationToken)
    {
        var photos = await QueryPhotos(familyId)
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
            referenceDate,
            skeleton.Strategy,
            entries,
            fromCache: false,
            cancellationToken);
    }

    private async Task<TodayMemoriesResponseDto> BuildResponseFromCacheAsync(
        Guid familyId,
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
        var photosById = await QueryPhotos(familyId)
            .Where(p => photoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var entries = new List<TodayMemoriesComposer.SelectionEntry>();
        foreach (var item in cachedItems)
        {
            if (!photosById.TryGetValue(item.PhotoId, out var photo) || photo.IsHidden)
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
            referenceDate,
            strategy,
            entries,
            fromCache,
            cancellationToken);
    }

    private async Task<TodayMemoriesResponseDto> BuildResponseFromEntriesAsync(
        Guid familyId,
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

        var photoEntities = entries.Select(e => e.Photo).ToList();
        var dtos = await _photoUrls.ToDtosAsync(photoEntities, cancellationToken: cancellationToken);
        var items = new List<TodayMemoryPhotoItemDto>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
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

    private IQueryable<Domain.Entities.Photo> QueryPhotos(Guid familyId) =>
        _db.Photos.AsNoTracking()
            .Where(p => p.FamilyId == familyId)
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags);
}
