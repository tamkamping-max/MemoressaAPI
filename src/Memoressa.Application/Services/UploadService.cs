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

    public UploadService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IS3StorageService s3,
        IPhotoUrlResolver photoUrls,
        IOptions<MediaStorageSettings> storageSettings,
        IOptions<StorageQuotaSettings> quotaSettings)
    {
        _db = db;
        _currentUser = currentUser;
        _s3 = s3;
        _photoUrls = photoUrls;
        _storageSettings = storageSettings.Value;
        _quotaSettings = quotaSettings.Value;
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
            return ServiceResult<StartUploadResponseDto>.Fail("fileSizeBytes must be the original image size in bytes", 400);
        }

        var quotaCheck = await CheckQuotaAsync(ctx.Value.UserId, request.FileSizeBytes, cancellationToken);
        if (!quotaCheck.Allowed)
        {
            return ServiceResult<StartUploadResponseDto>.Fail(quotaCheck.Error!, quotaCheck.StatusCode);
        }

        var keys = _s3.BuildPhotoUploadKeys(ctx.Value.FamilyId, ctx.Value.UserId, request.FileName);
        var expiryMinutes = _storageSettings.UploadPresignedUrlExpiryMinutes;
        var expiry = TimeSpan.FromMinutes(expiryMinutes);
        var expiresAt = DateTime.UtcNow.Add(expiry);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "image/jpeg" : request.ContentType;

        var fullUrl = await _s3.GetPresignedPutUrlAsync(keys.FullObjectKey, contentType, expiry, cancellationToken);
        var compressedUrl = await _s3.GetPresignedPutUrlAsync(keys.CompressedObjectKey, contentType, expiry, cancellationToken);
        var thumbnailUrl = await _s3.GetPresignedPutUrlAsync(keys.ThumbnailObjectKey, contentType, expiry, cancellationToken);

        var session = new UploadSession
        {
            UserId = ctx.Value.UserId,
            FamilyId = ctx.Value.FamilyId,
            MediaKind = MediaKind.Photo,
            FileName = keys.CompressedFileName,
            ContentType = contentType,
            FileSizeBytes = request.FileSizeBytes,
            S3Key = keys.CompressedObjectKey,
            S3KeyFull = keys.FullObjectKey,
            S3KeyThumbnail = keys.ThumbnailObjectKey,
            TakenAt = request.TakenAt,
            PrivacyScope = request.PrivacyScope,
            SharedAlbumId = request.SharedAlbumId,
            ExpiresAt = expiresAt,
            Status = UploadSessionStatus.Pending
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<StartUploadResponseDto>.Ok(new StartUploadResponseDto
        {
            SessionId = session.Id,
            PresignedUrlExpiryMinutes = expiryMinutes,
            ExpiresAt = expiresAt,
            Uploads = new StartUploadTargetsDto
            {
                Full = new UploadPartTargetDto { PresignedUrl = fullUrl, ObjectKey = keys.FullFileName },
                Compressed = new UploadPartTargetDto { PresignedUrl = compressedUrl, ObjectKey = keys.CompressedFileName },
                Thumbnail = new UploadPartTargetDto { PresignedUrl = thumbnailUrl, ObjectKey = keys.ThumbnailFileName }
            }
        });
    }

    public async Task<ServiceResult<PhotoDto>> CompleteUploadAsync(
        Guid sessionId,
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
        var compressedExists = await _s3.ObjectExistsAsync(session.S3Key, cancellationToken);
        var thumbnailExists = await _s3.ObjectExistsAsync(session.S3KeyThumbnail!, cancellationToken);
        if (!fullExists || !compressedExists || !thumbnailExists)
        {
            return ServiceResult<PhotoDto>.Fail("Upload incomplete: all photo variants must be uploaded before complete", 409);
        }

        var fullSize = await _s3.GetObjectSizeBytesAsync(session.S3KeyFull!, cancellationToken) ?? session.FileSizeBytes;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var user = await _db.UserAccounts
            .FirstAsync(u => u.Id == ctx.Value.UserId, cancellationToken);

        if (user.CloudStorageUsedBytes + fullSize > _quotaSettings.QuotaBytesPerUser)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<PhotoDto>.Fail("Storage quota exceeded", 413);
        }

        user.CloudStorageUsedBytes += fullSize;

        var photo = new Photo
        {
            FamilyId = session.FamilyId,
            UploadedByUserId = session.UserId,
            S3Key = session.S3Key,
            ThumbnailS3Key = session.S3KeyThumbnail,
            ContentType = session.ContentType,
            FileSizeBytes = fullSize,
            PrivacyScope = session.PrivacyScope,
            SharedAlbumId = session.SharedAlbumId,
            TakenAt = session.TakenAt ?? session.CreatedAt,
            Visibility = MemoryVisibility.Family
        };

        _db.Photos.Add(photo);
        session.Status = UploadSessionStatus.Completed;
        session.ResultPhotoId = photo.Id;
        session.FileSizeBytes = fullSize;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        photo = await _db.Photos
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags)
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
                FullOriginalFileSizeBytes = s.FileSizeBytes
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
