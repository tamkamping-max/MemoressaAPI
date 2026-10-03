using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class FamilyService
{
    public async Task<ServiceResult<IReadOnlyList<FamilyMemberInviteDto>>> GetFamilyMemberInvitesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberInviteDto>>.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<IReadOnlyList<FamilyMemberInviteDto>>.Fail("Unauthorized", 401);
        }

        var invites = await _db.FamilyMemberInvites.AsNoTracking()
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => i.Status == FriendInviteStatus.Pending
                        && i.InviteeUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = new List<FamilyMemberInviteDto>(invites.Count);
        foreach (var invite in invites)
        {
            var dto = MapFamilyMemberInviteDto(invite, incomingForViewer: true, invite.Inviter, invite.Invitee);
            dto = dto with
            {
                AvatarUrl = await _avatarUrls.ResolveForResponseAsync(dto.AvatarUrl, cancellationToken),
                Inviter = dto.Inviter is null
                    ? null
                    : dto.Inviter with
                    {
                        AvatarUrl = await _avatarUrls.ResolveForResponseAsync(dto.Inviter.AvatarUrl, cancellationToken)
                    },
                Invitee = dto.Invitee is null
                    ? null
                    : dto.Invitee with
                    {
                        AvatarUrl = await _avatarUrls.ResolveForResponseAsync(dto.Invitee.AvatarUrl, cancellationToken)
                    }
            };
            dtos.Add(dto);
        }

        return ServiceResult<IReadOnlyList<FamilyMemberInviteDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<FamilyMemberDto>> CreateFamilyMemberInviteAsync(
        CreateFamilyMemberInviteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var ctx = await ServiceHelpers.ResolveFamilyAsync(_currentUser, _db, cancellationToken);
        if (ctx is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        if (!PasswordResetCodeRules.TryNormalizeEmail(request.Email, out var inviteeEmail, out var formatError))
        {
            return ServiceResult<FamilyMemberDto>.Fail(formatError!, 400);
        }

        var inviterId = ctx.Value.UserId;
        var inviter = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == inviterId, cancellationToken);
        if (inviter is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        if (string.Equals(inviter.Email, inviteeEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<FamilyMemberDto>.Fail("Cannot invite yourself", 400);
        }

        var inviteeUser = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == inviteeEmail && u.IsActive, cancellationToken);
        if (inviteeUser is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Invitee must be an existing app member", 404);
        }

        if (await AlreadyLinkedInFamilyAsync(ctx.Value.FamilyId, inviteeUser.Id, cancellationToken))
        {
            return ServiceResult<FamilyMemberDto>.Fail("Already in family tree", 409);
        }

        var existingPending = await _db.FamilyMemberInvites
            .FirstOrDefaultAsync(
                i => i.Status == FriendInviteStatus.Pending
                     && i.FamilyId == ctx.Value.FamilyId
                     && i.InviterUserId == inviterId
                     && i.InviteeEmail == inviteeEmail,
                cancellationToken);

        if (existingPending is not null)
        {
            return ServiceResult<FamilyMemberDto>.Ok(
                await MapPendingOutgoingInviteAsync(existingPending, inviteeUser, cancellationToken));
        }

        var invite = new FamilyMemberInvite
        {
            FamilyId = ctx.Value.FamilyId,
            InviterUserId = inviterId,
            InviteeUserId = inviteeUser.Id,
            InviteeEmail = inviteeEmail,
            Status = FriendInviteStatus.Pending
        };
        _db.FamilyMemberInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<FamilyMemberDto>.Created(
            await MapPendingOutgoingInviteAsync(invite, inviteeUser, cancellationToken));
    }

    public async Task<ServiceResult<FamilyMemberDto>> AcceptFamilyMemberInviteAsync(
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Unauthorized", 401);
        }

        var invite = await _db.FamilyMemberInvites
            .Include(i => i.Inviter)
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);

        if (invite is null || invite.Status != FriendInviteStatus.Pending)
        {
            return ServiceResult<FamilyMemberDto>.NotFound("Invite not found");
        }

        if (invite.InviterUserId == userId)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Cannot accept your own invite", 400);
        }

        if (invite.InviteeUserId.HasValue && invite.InviteeUserId != userId)
        {
            return ServiceResult<FamilyMemberDto>.Fail("Forbidden", 403);
        }

        if (!invite.InviteeUserId.HasValue
            && !string.Equals(invite.InviteeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<FamilyMemberDto>.Fail("Forbidden", 403);
        }

        invite.InviteeUserId ??= userId;

        if (await AlreadyLinkedInFamilyAsync(invite.FamilyId, userId, cancellationToken))
        {
            invite.Status = FriendInviteStatus.Accepted;
            await _db.SaveChangesAsync(cancellationToken);
            var existing = await _db.FamilyMembers
                .Include(m => m.PhotoMembers)
                .FirstAsync(m => m.FamilyId == invite.FamilyId && m.LinkedUserId == userId, cancellationToken);
            return ServiceResult<FamilyMemberDto>.Ok(
                await MapAcceptedMemberAsync(existing, user, cancellationToken));
        }

        var displayName = user.Nickname ?? user.Email ?? "Family member";
        var member = new FamilyMember
        {
            FamilyId = invite.FamilyId,
            Name = displayName,
            Nickname = user.Nickname,
            LinkedUserId = userId,
            AssignedToTree = false,
            Generation = Generation.Self,
            AvatarUrl = user.AvatarUrl
        };
        _db.FamilyMembers.Add(member);
        invite.Status = FriendInviteStatus.Accepted;
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<FamilyMemberDto>.Ok(await MapAcceptedMemberAsync(member, user, cancellationToken));
    }

    public async Task<ServiceResult> RejectFamilyMemberInviteAsync(
        Guid inviteId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var invite = await _db.FamilyMemberInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
        if (invite is null || invite.Status != FriendInviteStatus.Pending)
        {
            return ServiceResult.NotFound("Invite not found");
        }

        if (invite.InviterUserId == userId)
        {
            invite.Status = FriendInviteStatus.Rejected;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        if (invite.InviteeUserId.HasValue && invite.InviteeUserId != userId)
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        if (!invite.InviteeUserId.HasValue
            && !string.Equals(invite.InviteeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult.Fail("Forbidden", 403);
        }

        invite.Status = FriendInviteStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.NoContent();
    }

    private async Task<bool> AlreadyLinkedInFamilyAsync(
        Guid familyId,
        Guid linkedUserId,
        CancellationToken cancellationToken) =>
        await _db.FamilyMembers.AsNoTracking()
            .AnyAsync(m => m.FamilyId == familyId && m.LinkedUserId == linkedUserId, cancellationToken);

    private async Task<IReadOnlyList<FamilyMemberDto>> BuildPendingOutgoingInviteDtosAsync(
        Guid familyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var invites = await _db.FamilyMemberInvites.AsNoTracking()
            .Include(i => i.Invitee)
            .Where(i => i.Status == FriendInviteStatus.Pending
                        && i.FamilyId == familyId
                        && i.InviterUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var list = new List<FamilyMemberDto>(invites.Count);
        foreach (var invite in invites)
        {
            list.Add(await MapPendingOutgoingInviteAsync(invite, invite.Invitee, cancellationToken));
        }

        return list;
    }
}
