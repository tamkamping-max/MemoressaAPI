using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class FrameService : IFrameService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FrameService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>> GetPlaybackPackagesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>.Fail("Unauthorized", 401);
        }

        var packages = await _db.FramePlaybackPackages.AsNoTracking()
            .Where(p => p.DisplayDeviceId == deviceId && p.FamilyId == ctx.Value.FamilyId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FramePlaybackPackageDto>>.Ok(packages.Select(p => p.ToDto()).ToList());
    }

    public async Task<ServiceResult<FramePlaybackPackageDto>> CreatePlaybackPackageAsync(
        Guid deviceId,
        CreatePlaybackPackageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FramePlaybackPackageDto>.Fail("Unauthorized", 401);
        }

        var device = await _db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult<FramePlaybackPackageDto>.NotFound("Device not found");
        }

        var package = new FramePlaybackPackage
        {
            DisplayDeviceId = deviceId,
            FamilyId = ctx.Value.FamilyId,
            ExternalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim(),
            Title = request.Title,
            PackageJson = request.PackageJson,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        _db.FramePlaybackPackages.Add(package);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FramePlaybackPackageDto>.Ok(package.ToDto());
    }

    public async Task<ServiceResult<FramePlaybackPackageDto>> EnsurePlaybackPackageAsync(
        Guid deviceId,
        EnsurePlaybackPackageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FramePlaybackPackageDto>.Fail("Unauthorized", 401);
        }

        var externalId = request.ExternalId.Trim();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return ServiceResult<FramePlaybackPackageDto>.Fail("externalId is required");
        }

        var device = await _db.DisplayDevices.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (device is null)
        {
            return ServiceResult<FramePlaybackPackageDto>.NotFound("Device not found");
        }

        var package = await _db.FramePlaybackPackages
            .FirstOrDefaultAsync(
                p => p.DisplayDeviceId == deviceId
                    && p.FamilyId == ctx.Value.FamilyId
                    && p.ExternalId == externalId,
                cancellationToken);

        if (package is null)
        {
            package = new FramePlaybackPackage
            {
                DisplayDeviceId = deviceId,
                FamilyId = ctx.Value.FamilyId,
                ExternalId = externalId,
                Title = request.Title,
                PackageJson = request.PackageJson,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder
            };
            _db.FramePlaybackPackages.Add(package);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                package.Title = request.Title;
            }

            if (!string.IsNullOrWhiteSpace(request.PackageJson))
            {
                package.PackageJson = request.PackageJson;
            }

            package.IsActive = request.IsActive;
            if (request.SortOrder != 0)
            {
                package.SortOrder = request.SortOrder;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FramePlaybackPackageDto>.Ok(package.ToDto());
    }

    public async Task<ServiceResult<IReadOnlyList<FrameCommentDto>>> GetCommentsAsync(
        Guid packageId,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FrameCommentDto>>.Fail("Unauthorized", 401);
        }

        var comments = await _db.FrameComments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.PackageId == packageId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FrameCommentDto>>.Ok(comments.Select(c => c.ToDto()).ToList());
    }

    public async Task<ServiceResult<FrameCommentDto>> AddCommentAsync(
        Guid packageId,
        AddFrameCommentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FrameCommentDto>.Fail("Unauthorized", 401);
        }

        var packageExists = await _db.FramePlaybackPackages.AsNoTracking()
            .AnyAsync(p => p.Id == packageId && p.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (!packageExists)
        {
            return ServiceResult<FrameCommentDto>.NotFound("Package not found");
        }

        var comment = new FrameComment
        {
            PackageId = packageId,
            UserId = ctx.Value.UserId,
            Message = request.Message
        };

        _db.FrameComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.FrameComments.AsNoTracking()
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id, cancellationToken);

        return ServiceResult<FrameCommentDto>.Ok(saved.ToDto());
    }
}
