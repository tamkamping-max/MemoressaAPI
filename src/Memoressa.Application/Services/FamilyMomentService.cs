using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class FamilyMomentService : IFamilyMomentService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FamilyMomentService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<FamilyMomentDto>>> GetMomentsAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMomentDto>>.Fail("Unauthorized", 401);
        }

        var moments = await QueryMoments(ctx.Value.FamilyId).OrderByDescending(m => m.Date).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<FamilyMomentDto>>.Ok(moments.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<FamilyMomentDto>> CreateMomentAsync(
        CreateFamilyMomentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMomentDto>.Fail("Unauthorized", 401);
        }

        var moment = new FamilyMoment
        {
            FamilyId = ctx.Value.FamilyId,
            Name = request.Name,
            EventType = request.EventType,
            Date = request.Date,
            Description = request.Description
        };
        _db.FamilyMoments.Add(moment);
        await ApplyRelationsAsync(moment, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var loaded = await QueryMoments(ctx.Value.FamilyId).FirstAsync(m => m.Id == moment.Id, cancellationToken);
        return ServiceResult<FamilyMomentDto>.Ok(loaded.ToDto());
    }

    public async Task<ServiceResult<FamilyMomentDto>> UpdateMomentAsync(
        Guid id,
        UpdateFamilyMomentRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMomentDto>.Fail("Unauthorized", 401);
        }

        var moment = await _db.FamilyMoments
            .Include(m => m.MomentPhotos)
            .Include(m => m.MomentMembers)
            .FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (moment is null)
        {
            return ServiceResult<FamilyMomentDto>.NotFound("Moment not found");
        }

        moment.Name = request.Name;
        moment.EventType = request.EventType;
        moment.Date = request.Date;
        moment.Description = request.Description;
        _db.FamilyMomentPhotos.RemoveRange(moment.MomentPhotos);
        _db.FamilyMomentMembers.RemoveRange(moment.MomentMembers);
        await ApplyRelationsAsync(moment, request, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        var loaded = await QueryMoments(ctx.Value.FamilyId).FirstAsync(m => m.Id == moment.Id, cancellationToken);
        return ServiceResult<FamilyMomentDto>.Ok(loaded.ToDto());
    }

    private async Task ApplyRelationsAsync(FamilyMoment moment, CreateFamilyMomentRequestDto request, CancellationToken cancellationToken)
    {
        foreach (var photoId in request.PhotoIds)
        {
            _db.FamilyMomentPhotos.Add(new FamilyMomentPhoto { FamilyMomentId = moment.Id, PhotoId = photoId });
        }

        foreach (var memberId in request.MemberIds)
        {
            _db.FamilyMomentMembers.Add(new FamilyMomentMember { FamilyMomentId = moment.Id, FamilyMemberId = memberId });
        }

        await Task.CompletedTask;
    }

    private IQueryable<FamilyMoment> QueryMoments(Guid familyId) =>
        _db.FamilyMoments.AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .Include(m => m.MomentPhotos)
            .Include(m => m.MomentMembers);
}
