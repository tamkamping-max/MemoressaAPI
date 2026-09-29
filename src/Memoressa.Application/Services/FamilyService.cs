using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public class FamilyService : IFamilyService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FamilyService(IMemoressaDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersAsync(CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Fail("Unauthorized", 401);
        }

        var members = await QueryMembers(ctx.Value.FamilyId).OrderBy(m => m.Name).ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Ok(members.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<FamilyMemberDto>> GetMemberByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var member = await QueryMembers(ctx.Value.FamilyId).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        return member is null
            ? ServiceResult<FamilyMemberDto>.NotFound("Member not found")
            : ServiceResult<FamilyMemberDto>.Ok(member.ToDto());
    }

    public async Task<ServiceResult<FamilyMemberDto>> AddMemberAsync(
        CreateFamilyMemberRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var member = new FamilyMember
        {
            FamilyId = ctx.Value.FamilyId,
            Name = request.Name,
            Nickname = request.Nickname,
            BirthDate = request.BirthDate,
            Generation = request.Generation,
            Relationship = request.Relationship,
            AvatarUrl = request.AvatarUrl,
            CityId = request.CityId,
            FaceRecognitionEnabled = request.FaceRecognitionEnabled
        };

        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FamilyMemberDto>.Ok(member.ToDto());
    }

    public async Task<ServiceResult<FamilyMemberDto>> UpdateMemberAsync(
        Guid id,
        UpdateFamilyMemberRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var member = await _db.FamilyMembers
            .Include(m => m.PhotoMembers)
            .FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == ctx.Value.FamilyId, cancellationToken);

        if (member is null)
        {
            return ServiceResult<FamilyMemberDto>.NotFound("Member not found");
        }

        member.Name = request.Name;
        member.Nickname = request.Nickname;
        member.BirthDate = request.BirthDate;
        member.Generation = request.Generation;
        member.Relationship = request.Relationship;
        member.AvatarUrl = request.AvatarUrl;
        member.CityId = request.CityId;
        member.FaceRecognitionEnabled = request.FaceRecognitionEnabled;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FamilyMemberDto>.Ok(member.ToDto());
    }

    public async Task<ServiceResult> DeleteMemberAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var member = await _db.FamilyMembers.FirstOrDefaultAsync(m => m.Id == id && m.FamilyId == ctx.Value.FamilyId, cancellationToken);
        if (member is null)
        {
            return ServiceResult.NotFound("Member not found");
        }

        _db.FamilyMembers.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersByGenerationAsync(
        Domain.Enums.Generation generation,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Fail("Unauthorized", 401);
        }

        var members = await QueryMembers(ctx.Value.FamilyId)
            .Where(m => m.Generation == generation)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Ok(members.Select(m => m.ToDto()).ToList());
    }

    private IQueryable<FamilyMember> QueryMembers(Guid familyId) =>
        _db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .Include(m => m.PhotoMembers);
}
