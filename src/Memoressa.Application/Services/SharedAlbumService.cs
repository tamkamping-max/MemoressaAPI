using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class SharedAlbumService : ISharedAlbumService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SharedAlbumService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<SharedAlbumDto>>> GetAlbumsAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<SharedAlbumDto>>.Fail("Unauthorized", 401);
        }

        var albums = await _db.SharedAlbums.AsNoTracking()
            .Where(a => a.FamilyId == ctx.Value.FamilyId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<SharedAlbumDto>>.Ok(albums.Select(a => a.ToDto()).ToList());
    }

    public async Task<ServiceResult<SharedAlbumDto>> GetAlbumByExternalIdAsync(
        string externalId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<SharedAlbumDto>.Fail("Unauthorized", 401);
        }

        var album = await _db.SharedAlbums.AsNoTracking()
            .FirstOrDefaultAsync(a => a.FamilyId == ctx.Value.FamilyId && a.ExternalId == externalId, cancellationToken);

        return album is null
            ? ServiceResult<SharedAlbumDto>.NotFound("Album not found")
            : ServiceResult<SharedAlbumDto>.Ok(album.ToDto());
    }

    public async Task<ServiceResult<SharedAlbumDto>> CreateAlbumAsync(
        CreateSharedAlbumRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<SharedAlbumDto>.Fail("Unauthorized", 401);
        }

        if (await _db.SharedAlbums.AnyAsync(
                a => a.FamilyId == ctx.Value.FamilyId && a.ExternalId == request.ExternalId,
                cancellationToken))
        {
            return ServiceResult<SharedAlbumDto>.Fail("Album external id already exists", 409);
        }

        var album = new SharedAlbum
        {
            FamilyId = ctx.Value.FamilyId,
            ExternalId = request.ExternalId,
            Name = request.Name,
            Subtitle = request.Subtitle,
            AlbumType = request.AlbumType,
            IsOwn = false
        };

        _db.SharedAlbums.Add(album);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<SharedAlbumDto>.Ok(album.ToDto());
    }
}
