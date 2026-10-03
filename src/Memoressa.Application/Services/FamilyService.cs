using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class FamilyService : IFamilyService
{
    private readonly IMemoressaDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAvatarUrlResolver _avatarUrls;

    public FamilyService(
        IMemoressaDbContext db,
        ICurrentUserService currentUser,
        IAvatarUrlResolver avatarUrls)
    {
        _db = db;
        _currentUser = currentUser;
        _avatarUrls = avatarUrls;
    }

    public async Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersAsync(
        string? connectionStatus = null,
        CancellationToken cancellationToken = default)
    {
        if (!ConnectionListFilter.TryParseFamilyConnectionStatus(
                connectionStatus,
                out var filterMode,
                out var filterError))
        {
            return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Fail(filterError!, 400);
        }

        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Fail("Unauthorized", 401);
        }

        // Full roster: all accepted members (assigned + unassigned pool), not only tree tiers.
        var members = await QueryMembers(ctx.Value.FamilyId).OrderBy(m => m.Name).ToListAsync(cancellationToken);
        var linkedUserIds = members.Where(m => m.LinkedUserId.HasValue).Select(m => m.LinkedUserId!.Value).Distinct().ToList();
        var linkedUsers = linkedUserIds.Count == 0
            ? new Dictionary<Guid, UserAccount>()
            : await _db.UserAccounts.AsNoTracking()
                .Where(u => linkedUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

        var dtos = new List<FamilyMemberDto>(members.Count);
        foreach (var member in members)
        {
            linkedUsers.TryGetValue(member.LinkedUserId ?? Guid.Empty, out var linked);
            dtos.Add(await MapAcceptedMemberAsync(member, linked, cancellationToken));
        }

        if (filterMode != ConnectionListFilterMode.AcceptedOnly)
        {
            dtos.AddRange(await BuildPendingOutgoingInviteDtosAsync(
                ctx.Value.FamilyId,
                ctx.Value.UserId,
                cancellationToken));
        }

        return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<FamilyMemberDto>> GetMemberByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var member = await QueryMembers(ctx.Value.FamilyId).FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (member is not null)
        {
            UserAccount? linked = null;
            if (member.LinkedUserId.HasValue)
            {
                linked = await _db.UserAccounts.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == member.LinkedUserId.Value, cancellationToken);
            }

            return ServiceResult<FamilyMemberDto>.Ok(await MapAcceptedMemberAsync(member, linked, cancellationToken));
        }

        var pending = await _db.FamilyMemberInvites.AsNoTracking()
            .Include(i => i.Invitee)
            .FirstOrDefaultAsync(
                i => i.Id == id
                     && i.FamilyId == ctx.Value.FamilyId
                     && i.Status == FriendInviteStatus.Pending
                     && i.InviterUserId == ctx.Value.UserId,
                cancellationToken);

        if (pending is null)
        {
            return ServiceResult<FamilyMemberDto>.NotFound("Member not found");
        }

        return ServiceResult<FamilyMemberDto>.Ok(
            await MapPendingOutgoingInviteAsync(pending, pending.Invitee, cancellationToken));
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
            FaceRecognitionEnabled = request.FaceRecognitionEnabled,
            AssignedToTree = true
        };

        _db.FamilyMembers.Add(member);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<FamilyMemberDto>.Ok(await MapAcceptedMemberAsync(member, linkedUser: null, cancellationToken));
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
        member.Relationship = request.Relationship;
        member.AvatarUrl = request.AvatarUrl;
        member.CityId = request.CityId;
        member.FaceRecognitionEnabled = request.FaceRecognitionEnabled;
        if (request.AssignedToTree.HasValue)
        {
            member.AssignedToTree = request.AssignedToTree.Value;
        }

        if (member.AssignedToTree)
        {
            member.Generation = request.Generation;
        }

        await _db.SaveChangesAsync(cancellationToken);

        UserAccount? linked = null;
        if (member.LinkedUserId.HasValue)
        {
            linked = await _db.UserAccounts.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == member.LinkedUserId.Value, cancellationToken);
        }

        return ServiceResult<FamilyMemberDto>.Ok(await MapAcceptedMemberAsync(member, linked, cancellationToken));
    }

    public async Task<ServiceResult> DeleteMemberAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var member = await _db.FamilyMembers.FirstOrDefaultAsync(
            m => m.Id == id && m.FamilyId == ctx.Value.FamilyId,
            cancellationToken);
        if (member is not null)
        {
            _db.FamilyMembers.Remove(member);
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        var invite = await _db.FamilyMemberInvites.FirstOrDefaultAsync(
            i => i.Id == id && i.Status == FriendInviteStatus.Pending,
            cancellationToken);
        if (invite is null)
        {
            return ServiceResult.NotFound("Member not found");
        }

        if (invite.FamilyId != ctx.Value.FamilyId)
        {
            return ServiceResult.NotFound("Member not found");
        }

        if (invite.InviterUserId == ctx.Value.UserId)
        {
            invite.Status = FriendInviteStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        return ServiceResult.Forbidden("You cannot delete this connection");
    }

    public async Task<ServiceResult<IReadOnlyList<FamilyMemberDto>>> GetMembersByGenerationAsync(
        Generation generation,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Fail("Unauthorized", 401);
        }

        var members = await QueryMembers(ctx.Value.FamilyId)
            .Where(m => m.AssignedToTree && m.Generation == generation)
            .ToListAsync(cancellationToken);

        var linkedUserIds = members.Where(m => m.LinkedUserId.HasValue).Select(m => m.LinkedUserId!.Value).Distinct().ToList();
        var linkedUsers = linkedUserIds.Count == 0
            ? new Dictionary<Guid, UserAccount>()
            : await _db.UserAccounts.AsNoTracking()
                .Where(u => linkedUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, cancellationToken);

        var dtos = new List<FamilyMemberDto>(members.Count);
        foreach (var member in members)
        {
            linkedUsers.TryGetValue(member.LinkedUserId ?? Guid.Empty, out var linked);
            dtos.Add(await MapAcceptedMemberAsync(member, linked, cancellationToken));
        }

        return ServiceResult<IReadOnlyList<FamilyMemberDto>>.Ok(dtos);
    }

    private IQueryable<FamilyMember> QueryMembers(Guid familyId) =>
        _db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == familyId)
            .Include(m => m.PhotoMembers);
}
