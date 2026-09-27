using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Memoressa.Application.Services;

public class UploadService : IUploadService
{
    private const string PhotosOnlyMessage = "僅支援照片";

    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IS3StorageService _s3;
    private readonly IPhotoUrlResolver _photoUrls;
    private readonly MediaStorageSettings _storageSettings;
    private readonly StorageQuotaSettings _quotaSettings;
    private readonly IActivityService _activities;

    public UploadService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IS3StorageService s3,
        IPhotoUrlResolver photoUrls,
        IOptions<MediaStorageSettings> storageSettings,
        IOptions<StorageQuotaSettings> quotaSettings,
        IActivityService activities)
    {
        _db = db;
        _currentUser = currentUser;
        _s3 = s3;
        _photoUrls = photoUrls;
        _storageSettings = storageSettings.Value;
        _quotaSettings = quotaSettings.Value;
        _activities = activities;
    }

    public async Task<ServiceResult<StartUploadResponseDto>> StartUploadAsync(
        StartUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<StartUploadResponseDto>.Fail("Unauthorized", 401);
        }

        if (request.MediaKind != MediaKind.Photo
            || request.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<StartUploadResponseDto>.Fail(PhotosOnlyMessage, 400);
        }

        if (request.FileSizeBytes <= 0)
        {
            return ServiceResult<StartUploadResponseDto>.Fail("fileSizeBytes must be the original still image size in bytes", 400);
        }

        if (!request.IsLivePhoto
            && (request.LivePhotoVideoFileSizeBytes > 0
                || !string.IsNullOrWhiteSpace(request.LivePhotoVideoFileName)
                || !string.IsNullOrWhiteSpace(request.LivePhotoVideoContentType)))
        {
            return ServiceResult<StartUploadResponseDto>.Fail(
                "Omit Live Photo fields when isLivePhoto is false, or set isLivePhoto true to upload the video",
                400);
        }

        long liveVideoSizeBytes = 0;
        if (request.IsLivePhoto)
        {
            if (request.LivePhotoVideoFileSizeBytes <= 0)
            {
                return ServiceResult<StartUploadResponseDto>.Fail(
                    "livePhotoVideoFileSizeBytes is required when isLivePhoto is true",
                    400);
            }

            liveVideoSizeBytes = request.LivePhotoVideoFileSizeBytes;
        }

        var quotaBytes = request.FileSizeBytes + liveVideoSizeBytes;
        var quotaCheck = await CheckQuotaAsync(ctx.Value.UserId, quotaBytes, cancellationToken);
        if (!quotaCheck.Allowed)
        {
            return ServiceResult<StartUploadResponseDto>.Fail(quotaCheck.Error!, quotaCheck.StatusCode);
        }

        Guid? activityAlbumId = null;
        if (!string.IsNullOrWhiteSpace(request.ActivityAlbumId))
        {
            var activity = await ActivityAlbumAccess.ResolveAsync(
                _db,
                ctx.Value.FamilyId,
                request.ActivityAlbumId,
                cancellationToken);
            if (activity is null)
            {
                return ServiceResult<StartUploadResponseDto>.NotFound("Activity not found");
            }

            if (!await ActivityAlbumAccess.CanUploadToAsync(_db, activity, ctx.Value.UserId, cancellationToken))
            {
                return ServiceResult<StartUploadResponseDto>.Fail(
                    "Activity is not in progress or you cannot upload to it",
                    403);
            }

            activityAlbumId = activity.Id;
        }

        var originalFileNameInput = string.IsNullOrWhiteSpace(request.OriginalFileName)
            ? request.FileName
            : request.OriginalFileName;
        if (!PhotoUploadKeys.TryNormalizeOriginalFileName(originalFileNameInput, out _))
        {
            return ServiceResult<StartUploadResponseDto>.Fail(
                "originalFileName must be a supported still image (jpg, jpeg, heic, heif, png)",
                400);
        }

        var originalContentType = string.IsNullOrWhiteSpace(request.OriginalContentType)
            ? request.ContentType
            : request.OriginalContentType;
        if (!PhotoUploadKeys.IsAllowedOriginalContentType(originalContentType))
        {
            return ServiceResult<StartUploadResponseDto>.Fail(
                "originalContentType must match the true original (e.g. image/heic, image/jpeg)",
                400);
        }

        string? liveVideoFileName = null;
        string? liveVideoContentType = null;
        if (request.IsLivePhoto)
        {
            if (string.IsNullOrWhiteSpace(request.LivePhotoVideoFileName)
                || string.IsNullOrWhiteSpace(request.LivePhotoVideoContentType))
            {
                return ServiceResult<StartUploadResponseDto>.Fail(
                    "Live Photo uploads require livePhotoVideoFileName and livePhotoVideoContentType",
                    400);
            }

            if (!PhotoUploadKeys.TryNormalizeLiveVideoFileName(request.LivePhotoVideoFileName, out liveVideoFileName))
            {
                return ServiceResult<StartUploadResponseDto>.Fail(
                    "livePhotoVideoFileName must be .mov or .mp4",
                    400);
            }

            liveVideoContentType = request.LivePhotoVideoContentType.Trim();
            if (!PhotoUploadKeys.IsAllowedLiveVideoContentType(liveVideoContentType))
            {
                return ServiceResult<StartUploadResponseDto>.Fail(
                    "livePhotoVideoContentType must be video/quicktime or video/mp4",
                    400);
            }
        }

        var keys = _s3.BuildPhotoUploadKeys(
            ctx.Value.FamilyId,
            ctx.Value.UserId,
            request.FileName,
            originalFileNameInput,
            liveVideoFileName);

        var expiryMinutes = _storageSettings.UploadPresignedUrlExpiryMinutes;
        var expiry = TimeSpan.FromMinutes(expiryMinutes);
        var expiresAt = DateTime.UtcNow.Add(expiry);
        const string compressedContentType = "image/jpeg";
        var usesFullAsCompressed = request.CompressedUsesFullOriginal;

        var fullUrl = await _s3.GetPresignedPutUrlAsync(
            keys.FullObjectKey,
            originalContentType.Trim(),
            expiry,
            cancellationToken);

        string? compressedUrl = null;
        if (!usesFullAsCompressed)
        {
            compressedUrl = await _s3.GetPresignedPutUrlAsync(
                keys.CompressedObjectKey,
                compressedContentType,
                expiry,
                cancellationToken);
        }

        var thumbnailUrl = await _s3.GetPresignedPutUrlAsync(
            keys.ThumbnailObjectKey,
            compressedContentType,
            expiry,
            cancellationToken);

        string? liveVideoPutUrl = null;
        if (request.IsLivePhoto && keys.LivePhotoVideoObjectKey is not null)
        {
            liveVideoPutUrl = await _s3.GetPresignedPutUrlAsync(
                keys.LivePhotoVideoObjectKey,
                liveVideoContentType!,
                expiry,
                cancellationToken);
        }

        var session = new UploadSession
        {
            UserId = ctx.Value.UserId,
            FamilyId = ctx.Value.FamilyId,
            MediaKind = MediaKind.Photo,
            FileName = usesFullAsCompressed ? keys.FullFileName : keys.CompressedFileName,
            ContentType = usesFullAsCompressed ? originalContentType.Trim() : compressedContentType,
            OriginalFileName = keys.FullFileName,
            OriginalContentType = originalContentType.Trim(),
            IsLivePhoto = request.IsLivePhoto,
            LivePhotoVideoFileName = keys.LivePhotoVideoFileName,
            LivePhotoVideoContentType = liveVideoContentType,
            OriginalStillFileSizeBytes = request.FileSizeBytes,
            LivePhotoVideoFileSizeBytes = liveVideoSizeBytes,
            FileSizeBytes = quotaBytes,
            S3Key = usesFullAsCompressed ? keys.FullObjectKey : keys.CompressedObjectKey,
            S3KeyFull = keys.FullObjectKey,
            S3KeyThumbnail = keys.ThumbnailObjectKey,
            S3KeyLivePhotoVideo = keys.LivePhotoVideoObjectKey,
            CompressedUsesFullOriginal = usesFullAsCompressed,
            ActivityAlbumId = activityAlbumId,
            TakenAt = request.TakenAt,
            PrivacyScope = request.PrivacyScope,
            SharedAlbumId = request.SharedAlbumId,
            ExpiresAt = expiresAt,
            Status = UploadSessionStatus.Pending
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        UploadPartTargetDto? liveTarget = null;
        if (request.IsLivePhoto && liveVideoPutUrl is not null && keys.LivePhotoVideoFileName is not null)
        {
            liveTarget = new UploadPartTargetDto
            {
                PresignedUrl = liveVideoPutUrl,
                ObjectKey = keys.LivePhotoVideoFileName
            };
        }

        return ServiceResult<StartUploadResponseDto>.Ok(new StartUploadResponseDto
        {
            SessionId = session.Id,
            PresignedUrlExpiryMinutes = expiryMinutes,
            ExpiresAt = expiresAt,
            CompressedUsesFullOriginal = usesFullAsCompressed,
            Uploads = new StartUploadTargetsDto
            {
                Full = new UploadPartTargetDto { PresignedUrl = fullUrl, ObjectKey = keys.FullFileName },
                Compressed = usesFullAsCompressed
                    ? null
                    : new UploadPartTargetDto { PresignedUrl = compressedUrl!, ObjectKey = keys.CompressedFileName },
                Thumbnail = new UploadPartTargetDto { PresignedUrl = thumbnailUrl, ObjectKey = keys.ThumbnailFileName },
                LivePhotoVideo = liveTarget
            }
        });
    }

    public async Task<ServiceResult<PhotoDto>> CompleteUploadAsync(
        Guid sessionId,
        CompleteUploadRequestDto? request = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoDto>.Fail("Unauthorized", 401);
        }

        var session = await _db.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == ctx.Value.UserId, cancellationToken);

        if (session is null)
        {
            return ServiceResult<PhotoDto>.NotFound("Upload session not found");
        }

        if (session.Status == UploadSessionStatus.Completed && session.ResultPhotoId.HasValue)
        {
            var existing = await _db.Photos
                .Include(p => p.PhotoMembers)
                .Include(p => p.AiTags)
                .Include(p => p.UploadedBy)
                .FirstAsync(p => p.Id == session.ResultPhotoId.Value, cancellationToken);

            return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(existing, cancellationToken: cancellationToken));
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = UploadSessionStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult<PhotoDto>.Fail("Upload session expired", 410);
        }

        if (!PhotoUploadKeys.UsesVariantLayout(session.S3Key, session.S3KeyFull))
        {
            return ServiceResult<PhotoDto>.Fail("Upload session is missing variant keys", 409);
        }

        var fullExists = await _s3.ObjectExistsAsync(session.S3KeyFull!, cancellationToken);
        var compressedExists = session.CompressedUsesFullOriginal
            ? fullExists
            : await _s3.ObjectExistsAsync(session.S3Key, cancellationToken);
        var thumbnailExists = await _s3.ObjectExistsAsync(session.S3KeyThumbnail!, cancellationToken);
        var liveVideoExists = !session.IsLivePhoto
                              || (!string.IsNullOrWhiteSpace(session.S3KeyLivePhotoVideo)
                                  && await _s3.ObjectExistsAsync(session.S3KeyLivePhotoVideo, cancellationToken));
        if (!fullExists || !compressedExists || !thumbnailExists || !liveVideoExists)
        {
            return ServiceResult<PhotoDto>.Fail(
                session.IsLivePhoto
                    ? session.CompressedUsesFullOriginal
                        ? "Upload incomplete: full original, thumbnail, and Live Photo video must be uploaded before complete"
                        : "Upload incomplete: full, compressed, thumbnail, and Live Photo video must be uploaded before complete"
                    : session.CompressedUsesFullOriginal
                        ? "Upload incomplete: full original and thumbnail must be uploaded before complete"
                        : "Upload incomplete: all photo variants must be uploaded before complete",
                409);
        }

        var displayObjectKey = session.CompressedUsesFullOriginal ? session.S3KeyFull! : session.S3Key;
        var displayContentType = session.CompressedUsesFullOriginal
            ? session.OriginalContentType ?? session.ContentType
            : session.ContentType;

        var stillSize = await _s3.GetObjectSizeBytesAsync(session.S3KeyFull!, cancellationToken)
                        ?? session.OriginalStillFileSizeBytes;
        long liveVideoSize = 0;
        if (session.IsLivePhoto && !string.IsNullOrWhiteSpace(session.S3KeyLivePhotoVideo))
        {
            liveVideoSize = await _s3.GetObjectSizeBytesAsync(session.S3KeyLivePhotoVideo, cancellationToken)
                            ?? session.LivePhotoVideoFileSizeBytes;
        }

        var totalQuotaBytes = stillSize + liveVideoSize;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = await _db.UserAccounts
            .FirstAsync(u => u.Id == ctx.Value.UserId, cancellationToken);

        if (user.CloudStorageUsedBytes + totalQuotaBytes > _quotaSettings.QuotaBytesPerUser)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<PhotoDto>.Fail("Storage quota exceeded", 413);
        }

        user.CloudStorageUsedBytes += totalQuotaBytes;

        var photo = new Photo
        {
            FamilyId = session.FamilyId,
            UploadedByUserId = session.UserId,
            S3Key = displayObjectKey,
            S3KeyFull = session.S3KeyFull,
            OriginalFileName = session.OriginalFileName,
            OriginalContentType = session.OriginalContentType,
            IsLivePhoto = session.IsLivePhoto,
            LivePhotoVideoS3Key = session.S3KeyLivePhotoVideo,
            LivePhotoVideoFileName = session.LivePhotoVideoFileName,
            LivePhotoVideoContentType = session.LivePhotoVideoContentType,
            ThumbnailS3Key = session.S3KeyThumbnail,
            ContentType = displayContentType,
            OriginalStillFileSizeBytes = stillSize,
            LivePhotoVideoFileSizeBytes = liveVideoSize,
            FileSizeBytes = totalQuotaBytes,
            PrivacyScope = session.PrivacyScope,
            SharedAlbumId = session.SharedAlbumId,
            TakenAt = session.TakenAt ?? session.CreatedAt,
            Visibility = MemoryVisibility.Family,
            Description = UploadMetadata.NormalizeOptionalText(request?.Description),
            Location = UploadMetadata.NormalizeOptionalText(request?.Location)
        };

        _db.Photos.Add(photo);
        session.Status = UploadSessionStatus.Completed;
        session.ResultPhotoId = photo.Id;
        session.OriginalStillFileSizeBytes = stillSize;
        session.LivePhotoVideoFileSizeBytes = liveVideoSize;
        session.FileSizeBytes = totalQuotaBytes;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (session.ActivityAlbumId.HasValue)
        {
            await _activities.LinkPhotoAfterUploadAsync(
                session.ActivityAlbumId.Value,
                photo.Id,
                session.FamilyId,
                cancellationToken);
        }

        photo = await _db.Photos
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags)
            .Include(p => p.UploadedBy)
            .FirstAsync(p => p.Id == photo.Id, cancellationToken);

        return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(photo, cancellationToken: cancellationToken));
    }

    public async Task<ServiceResult<StorageUsageDto>> GetStorageUsageAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult<StorageUsageDto>.Fail("Unauthorized", 401);
        }

        var used = await _db.UserAccounts.AsNoTracking()
            .Where(u => u.Id == _currentUser.UserId.Value)
            .Select(u => u.CloudStorageUsedBytes)
            .FirstOrDefaultAsync(cancellationToken);

        return ServiceResult<StorageUsageDto>.Ok(new StorageUsageDto
        {
            UsedBytes = used,
            LimitBytes = _quotaSettings.QuotaBytesPerUser
        });
    }

    public async Task<ServiceResult<IReadOnlyList<IncompleteUploadSessionDto>>> GetIncompleteUploadsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.UserId is null)
        {
            return ServiceResult<IReadOnlyList<IncompleteUploadSessionDto>>.Fail("Unauthorized", 401);
        }

        var now = DateTime.UtcNow;
        var configuredMinutes = _storageSettings.UploadPresignedUrlExpiryMinutes;
        var sessions = await _db.UploadSessions.AsNoTracking()
            .Where(s => s.UserId == _currentUser.UserId.Value
                        && s.Status == UploadSessionStatus.Pending
                        && s.ExpiresAt >= now)
            .OrderBy(s => s.ExpiresAt)
            .Select(s => new IncompleteUploadSessionDto
            {
                SessionId = s.Id,
                FileName = s.FileName,
                PresignedUrlExpiryMinutes = configuredMinutes,
                ExpiresAt = s.ExpiresAt,
                Status = s.Status,
                OriginalStillFileSizeBytes = s.OriginalStillFileSizeBytes,
                LivePhotoVideoFileSizeBytes = s.LivePhotoVideoFileSizeBytes,
                QuotaReservedBytes = s.FileSizeBytes,
                FullOriginalFileSizeBytes = s.OriginalStillFileSizeBytes,
                CompressedUsesFullOriginal = s.CompressedUsesFullOriginal
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<IncompleteUploadSessionDto>>.Ok(sessions);
    }

    private async Task<(bool Allowed, string? Error, int StatusCode)> CheckQuotaAsync(
        Guid userId,
        long additionalBytes,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var user = await _db.UserAccounts.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.CloudStorageUsedBytes })
            .FirstAsync(cancellationToken);

        var pendingBytes = await _db.UploadSessions.AsNoTracking()
            .Where(s => s.UserId == userId
                        && s.Status == UploadSessionStatus.Pending
                        && s.ExpiresAt >= now)
            .SumAsync(s => s.FileSizeBytes, cancellationToken);

        var projected = user.CloudStorageUsedBytes + pendingBytes + additionalBytes;
        if (projected > _quotaSettings.QuotaBytesPerUser)
        {
            return (false, "Storage quota exceeded", 413);
        }

        return (true, null, 200);
    }
}
