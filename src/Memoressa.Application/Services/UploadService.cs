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
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IS3StorageService _s3;
    private readonly IPhotoUrlResolver _photoUrls;
    private readonly IThumbnailGenerationService _thumbnails;
    private readonly MediaStorageSettings _storageSettings;

    public UploadService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IS3StorageService s3,
        IPhotoUrlResolver photoUrls,
        IThumbnailGenerationService thumbnails,
        IOptions<MediaStorageSettings> storageSettings)
    {
        _db = db;
        _currentUser = currentUser;
        _s3 = s3;
        _photoUrls = photoUrls;
        _thumbnails = thumbnails;
        _storageSettings = storageSettings.Value;
    }

    public async Task<ServiceResult<UploadSessionDto>> StartUploadAsync(
        StartUploadRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<UploadSessionDto>.Fail("Unauthorized", 401);
        }

        var s3Key = _s3.BuildObjectKey(ctx.Value.FamilyId, ctx.Value.UserId, request.FileName);
        var expiry = TimeSpan.FromMinutes(_storageSettings.PresignedUrlExpiryMinutes);
        var presignedUrl = await _s3.GetPresignedPutUrlAsync(s3Key, request.ContentType, expiry, cancellationToken);

        var session = new UploadSession
        {
            UserId = ctx.Value.UserId,
            FamilyId = ctx.Value.FamilyId,
            MediaKind = request.MediaKind,
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            S3Key = s3Key,
            PrivacyScope = request.PrivacyScope,
            SharedAlbumId = request.SharedAlbumId,
            ExpiresAt = DateTime.UtcNow.Add(expiry),
            Status = UploadSessionStatus.Pending
        };

        _db.UploadSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<UploadSessionDto>.Ok(new UploadSessionDto
        {
            SessionId = session.Id,
            PresignedUrl = presignedUrl,
            S3Key = s3Key,
            ExpiresAt = session.ExpiresAt
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

            if (string.IsNullOrWhiteSpace(existing.ThumbnailS3Key))
            {
                await _thumbnails.GenerateAndStoreAsync(existing.Id, cancellationToken);
                existing = await _db.Photos
                    .Include(p => p.PhotoMembers)
                    .Include(p => p.AiTags)
                    .FirstAsync(p => p.Id == existing.Id, cancellationToken);
            }

            return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(existing, cancellationToken: cancellationToken));
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            session.Status = UploadSessionStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult<PhotoDto>.Fail("Upload session expired", 410);
        }

        var photo = new Photo
        {
            FamilyId = session.FamilyId,
            UploadedByUserId = session.UserId,
            S3Key = session.S3Key,
            ContentType = session.ContentType,
            FileSizeBytes = session.FileSizeBytes,
            PrivacyScope = session.PrivacyScope,
            SharedAlbumId = session.SharedAlbumId,
            TakenAt = session.CreatedAt,
            Visibility = MemoryVisibility.Family
        };

        _db.Photos.Add(photo);
        session.Status = UploadSessionStatus.Completed;
        session.ResultPhotoId = photo.Id;
        await _db.SaveChangesAsync(cancellationToken);

        await _thumbnails.GenerateAndStoreAsync(photo.Id, cancellationToken);

        photo = await _db.Photos
            .Include(p => p.PhotoMembers)
            .Include(p => p.AiTags)
            .FirstAsync(p => p.Id == photo.Id, cancellationToken);

        return ServiceResult<PhotoDto>.Ok(await _photoUrls.ToDtoAsync(photo, cancellationToken: cancellationToken));
    }
}
