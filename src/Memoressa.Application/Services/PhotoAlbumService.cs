using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class PhotoAlbumService : IPhotoAlbumService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPhotoUrlResolver _photoUrls;

    public PhotoAlbumService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IPhotoUrlResolver photoUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _photoUrls = photoUrls;
    }

    public async Task<ServiceResult<PhotoAlbumDto>> CreateOrFindAsync(
        CreatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("Unauthorized", 401);
        }

        var photoIds = request.PhotoIds?.Distinct().ToList() ?? [];
        if (photoIds.Count == 0)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("photoIds must contain at least one photo");
        }

        var accessError = await ValidatePhotoAccessAsync(ctx.Value.FamilyId, ctx.Value.UserId, photoIds, cancellationToken);
        if (accessError is not null)
        {
            return accessError;
        }

        var fingerprint = PhotoAlbumFingerprint.Compute(photoIds);
        var existing = await LoadAlbumQuery(ctx.Value.FamilyId)
            .FirstOrDefaultAsync(a => a.PhotoSetFingerprint == fingerprint, cancellationToken);

        if (existing is not null)
        {
            var dto = await MapAlbumDtoAsync(existing, includePhotoSummaries: true, created: false, cancellationToken);
            return ServiceResult<PhotoAlbumDto>.Ok(dto);
        }

        var album = new PhotoAlbum
        {
            FamilyId = ctx.Value.FamilyId,
            CreatedByUserId = ctx.Value.UserId,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Visibility = request.Visibility ?? MemoryVisibility.Family,
            PhotoSetFingerprint = fingerprint,
            CoverPhotoId = photoIds[0]
        };

        _db.PhotoAlbums.Add(album);
        await AddAlbumPhotosAsync(album, photoIds, cancellationToken);

        if (request.UserTags is not null)
        {
            await ReplaceAlbumUserTagsAsync(album.Id, PhotoUserTagRules.NormalizeReplaceList(request.UserTags), cancellationToken);
        }

        if (request.MemberIds is not null)
        {
            var memberError = await ReplaceAlbumMembersAsync(album, ctx.Value.FamilyId, request.MemberIds, cancellationToken);
            if (memberError is not null)
            {
                return memberError;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadAlbumQuery(ctx.Value.FamilyId)
            .FirstAsync(a => a.Id == album.Id, cancellationToken);

        var createdDto = await MapAlbumDtoAsync(loaded, includePhotoSummaries: true, created: true, cancellationToken);
        return ServiceResult<PhotoAlbumDto>.Created(createdDto);
    }

    public async Task<ServiceResult<PhotoAlbumDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("Unauthorized", 401);
        }

        var album = await LoadAlbumQuery(ctx.Value.FamilyId)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (album is null)
        {
            return ServiceResult<PhotoAlbumDto>.NotFound("Photo album not found");
        }

        var dto = await MapAlbumDtoAsync(album, includePhotoSummaries: true, created: null, cancellationToken);
        return ServiceResult<PhotoAlbumDto>.Ok(dto);
    }

    public async Task<ServiceResult<PhotoAlbumListPageDto>> ListCardsAsync(
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumListPageDto>.Fail("Unauthorized", 401);
        }

        var pageSize = PhotoTimelinePagination.NormalizeLimit(limit);
        var takeCount = pageSize + 1;

        var query = LoadAlbumQuery(ctx.Value.FamilyId);

        if (!string.IsNullOrWhiteSpace(cursor) && Guid.TryParse(cursor, out var cursorId))
        {
            var cursorAlbum = await _db.PhotoAlbums.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == cursorId && a.FamilyId == ctx.Value.FamilyId, cancellationToken);
            if (cursorAlbum is not null)
            {
                query = query.Where(a =>
                    a.UpdatedAt < cursorAlbum.UpdatedAt
                    || (a.UpdatedAt == cursorAlbum.UpdatedAt && a.Id.CompareTo(cursorAlbum.Id) < 0));
            }
        }

        var page = await query
            .OrderByDescending(a => a.UpdatedAt)
            .ThenByDescending(a => a.Id)
            .Take(takeCount)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > pageSize;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var items = page.Select(MapCard).ToList();
        string? nextCursor = hasMore && page.Count > 0 ? page[^1].Id.ToString("D") : null;

        return ServiceResult<PhotoAlbumListPageDto>.Ok(new PhotoAlbumListPageDto
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = hasMore
        });
    }

    public async Task<ServiceResult<PhotoAlbumDto>> UpdateAsync(
        Guid id,
        UpdatePhotoAlbumRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("Unauthorized", 401);
        }

        var album = await _db.PhotoAlbums
            .Include(a => a.AlbumPhotos)
            .Include(a => a.UserTags)
            .Include(a => a.AlbumMembers)
            .FirstOrDefaultAsync(a => a.Id == id && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (album is null)
        {
            return ServiceResult<PhotoAlbumDto>.NotFound("Photo album not found");
        }

        if (request.Description is not null)
        {
            album.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        if (request.Visibility.HasValue)
        {
            album.Visibility = request.Visibility.Value;
        }

        if (request.CoverPhotoId.HasValue)
        {
            if (!album.AlbumPhotos.Any(p => p.PhotoId == request.CoverPhotoId.Value))
            {
                return ServiceResult<PhotoAlbumDto>.Fail("coverPhotoId must belong to the album");
            }

            album.CoverPhotoId = request.CoverPhotoId.Value;
        }

        if (request.UserTags is not null)
        {
            await ReplaceAlbumUserTagsAsync(album.Id, PhotoUserTagRules.NormalizeReplaceList(request.UserTags), cancellationToken);
        }

        if (request.MemberIds is not null)
        {
            var memberError = await ReplaceAlbumMembersAsync(album, ctx.Value.FamilyId, request.MemberIds, cancellationToken);
            if (memberError is not null)
            {
                return memberError;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadAlbumQuery(ctx.Value.FamilyId)
            .FirstAsync(a => a.Id == id, cancellationToken);

        var dto = await MapAlbumDtoAsync(loaded, includePhotoSummaries: true, created: null, cancellationToken);
        return ServiceResult<PhotoAlbumDto>.Ok(dto);
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var album = await _db.PhotoAlbums
            .FirstOrDefaultAsync(a => a.Id == id && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (album is null)
        {
            return ServiceResult.NotFound("Photo album not found");
        }

        _db.PhotoAlbums.Remove(album);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<PhotoAlbumDto>> PatchPhotosAsync(
        Guid id,
        PatchPhotoAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("Unauthorized", 401);
        }

        var album = await _db.PhotoAlbums
            .Include(a => a.AlbumPhotos)
            .FirstOrDefaultAsync(a => a.Id == id && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (album is null)
        {
            return ServiceResult<PhotoAlbumDto>.NotFound("Photo album not found");
        }

        var photoIds = album.AlbumPhotos.Select(p => p.PhotoId).ToHashSet();

        if (request.RemovePhotoIds is not null)
        {
            foreach (var removeId in request.RemovePhotoIds)
            {
                photoIds.Remove(removeId);
            }
        }

        if (request.AddPhotoIds is not null)
        {
            var addIds = request.AddPhotoIds.Distinct().ToList();
            var accessError = await ValidatePhotoAccessAsync(ctx.Value.FamilyId, ctx.Value.UserId, addIds, cancellationToken);
            if (accessError is not null)
            {
                return accessError;
            }

            foreach (var addId in addIds)
            {
                photoIds.Add(addId);
            }
        }

        if (photoIds.Count == 0)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("Album must contain at least one photo");
        }

        var newFingerprint = PhotoAlbumFingerprint.Compute(photoIds);
        if (!string.Equals(newFingerprint, album.PhotoSetFingerprint, StringComparison.Ordinal))
        {
            var collision = await _db.PhotoAlbums.AsNoTracking()
                .AnyAsync(
                    a => a.FamilyId == ctx.Value.FamilyId
                         && a.PhotoSetFingerprint == newFingerprint
                         && a.Id != album.Id,
                    cancellationToken);

            if (collision)
            {
                return ServiceResult<PhotoAlbumDto>.Fail("An album with this photo set already exists", 409);
            }

            album.PhotoSetFingerprint = newFingerprint;
        }

        var toRemove = album.AlbumPhotos.Where(p => !photoIds.Contains(p.PhotoId)).ToList();
        if (toRemove.Count > 0)
        {
            _db.PhotoAlbumPhotos.RemoveRange(toRemove);
        }

        var existingInAlbum = album.AlbumPhotos.Select(p => p.PhotoId).ToHashSet();
        var order = album.AlbumPhotos.Count > 0 ? album.AlbumPhotos.Max(p => p.SortOrder) + 1 : 0;
        foreach (var photoId in photoIds.Where(id => !existingInAlbum.Contains(id)))
        {
            _db.PhotoAlbumPhotos.Add(new PhotoAlbumPhoto
            {
                PhotoAlbumId = album.Id,
                PhotoId = photoId,
                SortOrder = order++
            });
        }

        if (album.CoverPhotoId is null || !photoIds.Contains(album.CoverPhotoId.Value))
        {
            album.CoverPhotoId = photoIds.First();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var loaded = await LoadAlbumQuery(ctx.Value.FamilyId)
            .FirstAsync(a => a.Id == id, cancellationToken);

        var dto = await MapAlbumDtoAsync(loaded, includePhotoSummaries: true, created: null, cancellationToken);
        return ServiceResult<PhotoAlbumDto>.Ok(dto);
    }

    public Task<ServiceResult<PhotoAlbumDto>> UnlinkPhotosAsync(
        Guid id,
        UnlinkPhotoAlbumPhotosRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.RemovePhotoIds is null || request.RemovePhotoIds.Count == 0)
        {
            return Task.FromResult(ServiceResult<PhotoAlbumDto>.Fail("removePhotoIds must contain at least one id"));
        }

        return PatchPhotosAsync(
            id,
            new PatchPhotoAlbumPhotosRequestDto { RemovePhotoIds = request.RemovePhotoIds },
            cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>> GetCommentsAsync(
        Guid albumId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>.Fail("Unauthorized", 401);
        }

        var exists = await _db.PhotoAlbums.AsNoTracking()
            .AnyAsync(a => a.Id == albumId && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (!exists)
        {
            return ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>.NotFound("Photo album not found");
        }

        var comments = await _db.PhotoAlbumComments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.PhotoAlbumId == albumId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<PhotoAlbumCommentDto>>.Ok(
            comments.Select(c => c.ToDto(ctx.Value.UserId)).ToList());
    }

    public async Task<ServiceResult<PhotoAlbumCommentDto>> AddCommentAsync(
        Guid albumId,
        AddPhotoAlbumCommentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<PhotoAlbumCommentDto>.Fail("Unauthorized", 401);
        }

        var message = request.Message.Trim();
        if (message.Length == 0)
        {
            return ServiceResult<PhotoAlbumCommentDto>.Fail("message is required");
        }

        var exists = await _db.PhotoAlbums.AsNoTracking()
            .AnyAsync(a => a.Id == albumId && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (!exists)
        {
            return ServiceResult<PhotoAlbumCommentDto>.NotFound("Photo album not found");
        }

        var comment = new PhotoAlbumComment
        {
            PhotoAlbumId = albumId,
            UserId = ctx.Value.UserId,
            Message = message
        };

        _db.PhotoAlbumComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.PhotoAlbumComments.AsNoTracking()
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id, cancellationToken);

        return ServiceResult<PhotoAlbumCommentDto>.Ok(saved.ToDto(ctx.Value.UserId));
    }

    public async Task<ServiceResult> DeleteCommentAsync(
        Guid albumId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var comment = await _db.PhotoAlbumComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.PhotoAlbumId == albumId, cancellationToken);

        if (comment is null)
        {
            return ServiceResult.NotFound("Comment not found");
        }

        var albumInFamily = await _db.PhotoAlbums.AsNoTracking()
            .AnyAsync(a => a.Id == albumId && a.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (!albumInFamily)
        {
            return ServiceResult.NotFound("Photo album not found");
        }

        if (comment.UserId != ctx.Value.UserId)
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        _db.PhotoAlbumComments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<(Guid AlbumId, IReadOnlyList<string> UserTags, string? Description)?> TryGetPrimaryAlbumForPhotoAsync(
        Guid photoId,
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var albumIds = await _db.PhotoAlbumPhotos.AsNoTracking()
            .Where(ap => ap.PhotoId == photoId)
            .Select(ap => ap.PhotoAlbumId)
            .ToListAsync(cancellationToken);

        if (albumIds.Count == 0)
        {
            return null;
        }

        var link = await _db.PhotoAlbums.AsNoTracking()
            .Where(a => a.FamilyId == familyId && albumIds.Contains(a.Id))
            .Include(a => a.UserTags)
            .OrderByDescending(a => a.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (link is null)
        {
            return null;
        }

        return (
            link.Id,
            link.UserTags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList(),
            link.Description);
    }

    private IQueryable<PhotoAlbum> LoadAlbumQuery(Guid familyId) =>
        _db.PhotoAlbums.AsNoTracking()
            .Where(a => a.FamilyId == familyId)
            .Include(a => a.AlbumPhotos)
            .Include(a => a.UserTags)
            .Include(a => a.AlbumMembers);

    private async Task<ServiceResult<PhotoAlbumDto>?> ValidatePhotoAccessAsync(
        Guid familyId,
        Guid viewerUserId,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        var accessible = await PhotoViewerAccess.ApplyViewerFilter(
                _db.Photos.AsNoTracking().Where(p => p.FamilyId == familyId),
                viewerUserId,
                _db)
            .Where(p => photoIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (accessible.Count != photoIds.Count)
        {
            return ServiceResult<PhotoAlbumDto>.Fail("One or more photos were not found or are not accessible", 404);
        }

        return null;
    }

    private static async Task AddAlbumPhotosAsync(
        PhotoAlbum album,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        var order = 0;
        foreach (var photoId in photoIds)
        {
            album.AlbumPhotos.Add(new PhotoAlbumPhoto
            {
                PhotoAlbumId = album.Id,
                PhotoId = photoId,
                SortOrder = order++
            });
        }

        await Task.CompletedTask;
    }

    private async Task ReplaceAlbumUserTagsAsync(
        Guid albumId,
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        var existing = await _db.PhotoAlbumUserTags
            .Where(t => t.PhotoAlbumId == albumId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.PhotoAlbumUserTags.RemoveRange(existing);
        }

        foreach (var tag in tags)
        {
            _db.PhotoAlbumUserTags.Add(new PhotoAlbumUserTag
            {
                PhotoAlbumId = albumId,
                Tag = tag
            });
        }
    }

    private async Task<ServiceResult<PhotoAlbumDto>?> ReplaceAlbumMembersAsync(
        PhotoAlbum album,
        Guid familyId,
        IReadOnlyList<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var distinct = memberIds.Distinct().ToList();
        if (distinct.Count > 0)
        {
            var validCount = await _db.FamilyMembers.AsNoTracking()
                .CountAsync(m => m.FamilyId == familyId && distinct.Contains(m.Id), cancellationToken);

            if (validCount != distinct.Count)
            {
                return ServiceResult<PhotoAlbumDto>.Fail("Invalid memberIds for this family");
            }
        }

        var existing = await _db.PhotoAlbumMembers
            .Where(m => m.PhotoAlbumId == album.Id)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.PhotoAlbumMembers.RemoveRange(existing);
        }

        foreach (var memberId in distinct)
        {
            _db.PhotoAlbumMembers.Add(new PhotoAlbumMember
            {
                PhotoAlbumId = album.Id,
                FamilyMemberId = memberId
            });
        }

        return null;
    }

    private async Task<PhotoAlbumDto> MapAlbumDtoAsync(
        PhotoAlbum album,
        bool includePhotoSummaries,
        bool? created,
        CancellationToken cancellationToken)
    {
        var photoIds = album.AlbumPhotos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.PhotoId)
            .Select(p => p.PhotoId)
            .ToList();

        IReadOnlyList<PhotoAlbumPhotoSummaryDto>? summaries = null;
        if (includePhotoSummaries && photoIds.Count > 0)
        {
            var photos = await PhotoViewerAccess.ApplyViewerFilter(
                    _db.Photos.AsNoTracking().Where(p => photoIds.Contains(p.Id)),
                    _currentUser.UserId ?? Guid.Empty,
                    _db)
                .ToListAsync(cancellationToken);

            var summaryList = new List<PhotoAlbumPhotoSummaryDto>();
            foreach (var id in photoIds)
            {
                var photo = photos.FirstOrDefault(p => p.Id == id);
                if (photo is null)
                {
                    continue;
                }

                var dto = await _photoUrls.ToDtoAsync(photo, cancellationToken: cancellationToken);
                summaryList.Add(new PhotoAlbumPhotoSummaryDto
                {
                    Id = photo.Id,
                    TakenAt = photo.TakenAt,
                    ThumbnailUrl = dto.ThumbnailUrl
                });
            }

            summaries = summaryList;
        }

        return new PhotoAlbumDto
        {
            Id = album.Id,
            PhotoIds = photoIds,
            UserTags = album.UserTags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList(),
            Description = album.Description,
            Visibility = album.Visibility,
            MemberIds = album.AlbumMembers.Select(m => m.FamilyMemberId).ToList(),
            CoverPhotoId = album.CoverPhotoId ?? photoIds.FirstOrDefault(),
            CreatedAt = album.CreatedAt,
            UpdatedAt = album.UpdatedAt,
            CreatedBy = album.CreatedByUserId,
            Photos = summaries,
            Created = created
        };
    }

    private static PhotoAlbumCardDto MapCard(PhotoAlbum album)
    {
        var photoIds = album.AlbumPhotos
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.PhotoId)
            .Select(p => p.PhotoId)
            .ToList();

        return new PhotoAlbumCardDto
        {
            AlbumId = album.Id,
            PhotoIds = photoIds,
            UserTags = album.UserTags.OrderBy(t => t.Tag).Select(t => t.Tag).ToList(),
            Description = album.Description,
            CoverPhotoId = album.CoverPhotoId ?? photoIds.FirstOrDefault()
        };
    }
}
