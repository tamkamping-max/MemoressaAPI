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
            return ServiceResult<FamilyMemberDto>.Fail(
                formatError!,
                400,
                InviteErrorCodes.InvalidEmail);
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
            return ServiceResult<FamilyMemberDto>.Fail(
                InviteErrorCodes.CannotInviteSelfMessage,
                400,
                InviteErrorCodes.CannotInviteSelf);
        }

        var inviteeUser = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == inviteeEmail && u.IsActive, cancellationToken);
        if (inviteeUser is null)
        {
            return ServiceResult<FamilyMemberDto>.Fail(
                InviteErrorCodes.EmailNotRegisteredMessage,
                404,
                InviteErrorCodes.EmailNotRegistered);
        }

        if (await FamilyInviteConnectionRules.IsAlreadyConnectedAsync(
                _db,
                ctx.Value.FamilyId,
                inviteeUser.Id,
                inviteeEmail,
                cancellationToken))
        {
            return ServiceResult<FamilyMemberDto>.Fail(
                InviteErrorCodes.FamilyAlreadyConnectedMessage,
                409,
                InviteErrorCodes.FamilyAlreadyConnected);
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

        var homeFamilyResult = await ResolveInviteeHomeFamilyIdAsync(userId, cancellationToken);
        if (!homeFamilyResult.Success)
        {
            return ServiceResult<FamilyMemberDto>.Fail(
                homeFamilyResult.Error!,
                homeFamilyResult.StatusCode,
                homeFamilyResult.ErrorCode);
        }

        if (!await AlreadyLinkedInFamilyAsync(invite.FamilyId, userId, cancellationToken))
        {
            var displayName = user.Nickname ?? user.Email ?? "Family member";
            _db.FamilyMembers.Add(new FamilyMember
            {
                FamilyId = invite.FamilyId,
                Name = displayName,
                Nickname = user.Nickname,
                LinkedUserId = userId,
                AssignedToTree = false,
                Generation = Generation.Self,
                AvatarUrl = user.AvatarUrl
            });
        }

        var mirrorMember = await EnsureInviterMirrorMemberOnHomeFamilyAsync(
            homeFamilyResult.Data!,
            invite,
            cancellationToken);

        invite.Status = FriendInviteStatus.Accepted;
        await _db.SaveChangesAsync(cancellationToken);

        var inviterAccount = invite.Inviter
                             ?? await _db.UserAccounts.AsNoTracking()
                                 .FirstAsync(u => u.Id == invite.InviterUserId, cancellationToken);

        return ServiceResult<FamilyMemberDto>.Ok(
            await MapAcceptedMemberAsync(mirrorMember, inviterAccount, cancellationToken));
    }

    /// <summary>
    /// Home family for invitee roster + accept response (JWT <c>family_id</c> when valid, else first membership, else bootstrap).
    /// </summary>
    private async Task<ServiceResult<Guid>> ResolveInviteeHomeFamilyIdAsync(
        Guid inviteeUserId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.FamilyId.HasValue)
        {
            var jwtFamilyId = _currentUser.FamilyId.Value;
            var jwtMember = await _db.FamilyMemberships.AsNoTracking()
                .AnyAsync(
                    m => m.UserId == inviteeUserId && m.FamilyId == jwtFamilyId,
                    cancellationToken);
            if (jwtMember)
            {
                return ServiceResult<Guid>.Ok(jwtFamilyId);
            }
        }

        var membership = await _db.FamilyMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == inviteeUserId, cancellationToken);
        if (membership is not null)
        {
            return ServiceResult<Guid>.Ok(membership.FamilyId);
        }

        return await BootstrapInviteeHomeFamilyAsync(inviteeUserId, cancellationToken);
    }

    private async Task<ServiceResult<Guid>> BootstrapInviteeHomeFamilyAsync(
        Guid inviteeUserId,
        CancellationToken cancellationToken)
    {
        var userExists = await _db.UserAccounts.AsNoTracking()
            .AnyAsync(u => u.Id == inviteeUserId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult<Guid>.Fail("Unauthorized", 401);
        }

        var family = new Family
        {
            Name = "My Family",
            OwnerUserId = inviteeUserId
        };
        _db.Families.Add(family);
        _db.FamilyMemberships.Add(new FamilyMembership
        {
            FamilyId = family.Id,
            UserId = inviteeUserId,
            Role = "owner"
        });

        return ServiceResult<Guid>.Ok(family.Id);
    }

    /// <summary>
    /// Row on invitee home family representing the inviter (GET /family-members + accept response body).
    /// </summary>
    private async Task<FamilyMember> EnsureInviterMirrorMemberOnHomeFamilyAsync(
        Guid homeFamilyId,
        FamilyMemberInvite invite,
        CancellationToken cancellationToken)
    {
        var existing = await _db.FamilyMembers
            .Include(m => m.PhotoMembers)
            .FirstOrDefaultAsync(
                m => m.FamilyId == homeFamilyId && m.LinkedUserId == invite.InviterUserId,
                cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var inviter = invite.Inviter
                      ?? await _db.UserAccounts.AsNoTracking()
                          .FirstOrDefaultAsync(u => u.Id == invite.InviterUserId, cancellationToken)
                      ?? throw new InvalidOperationException("Inviter account missing");

        var inviterDisplay = inviter.Nickname ?? inviter.Email ?? "Family member";
        var mirror = new FamilyMember
        {
            FamilyId = homeFamilyId,
            Name = inviterDisplay,
            Nickname = inviter.Nickname,
            LinkedUserId = invite.InviterUserId,
            AssignedToTree = false,
            Generation = Generation.Self,
            AvatarUrl = inviter.AvatarUrl
        };
        _db.FamilyMembers.Add(mirror);
        return mirror;
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

    private Task<bool> AlreadyLinkedInFamilyAsync(
        Guid familyId,
        Guid linkedUserId,
        CancellationToken cancellationToken) =>
        FamilyInviteConnectionRules.IsLinkedInFamilyAsync(_db, familyId, linkedUserId, cancellationToken);

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
