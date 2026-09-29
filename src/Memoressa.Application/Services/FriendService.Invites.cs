using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class FriendService
{
    private async Task<IReadOnlyList<FriendDto>> BuildPendingFriendDtosAsync(
        Guid userId,
        string userEmail,
        CancellationToken cancellationToken)
    {
        var invites = await _db.FriendInvites.AsNoTracking()
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => i.Status == FriendInviteStatus.Pending
                        && (i.InviterUserId == userId
                            || i.InviteeUserId == userId
                            || i.InviteeEmail == userEmail))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var list = new List<FriendDto>(invites.Count);
        foreach (var invite in invites)
        {
            list.Add(await MapPendingInviteFriendDtoAsync(invite, userId, cancellationToken));
        }

        return list;
    }

    public async Task<ServiceResult<IReadOnlyList<FriendInviteDto>>> GetFriendInvitesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<IReadOnlyList<FriendInviteDto>>.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<IReadOnlyList<FriendInviteDto>>.Fail("Unauthorized", 401);
        }

        var email = user.Email;
        var invites = await _db.FriendInvites.AsNoTracking()
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => i.Status == FriendInviteStatus.Pending
                        && (i.InviterUserId == userId
                            || i.InviteeUserId == userId
                            || i.InviteeEmail == email))
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = invites.Select(i =>
        {
            var outgoing = i.InviterUserId == userId;
            var counterparty = outgoing ? i.Invitee : i.Inviter;
            var displayName = counterparty?.Nickname ?? counterparty?.Email;
            return new FriendInviteDto
            {
                Id = i.Id,
                Email = outgoing ? i.InviteeEmail : i.Inviter.Email,
                Name = displayName,
                Nickname = displayName,
                AvatarUrl = counterparty?.AvatarUrl,
                Status = outgoing ? "pending_outgoing" : "pending_incoming",
                CreatedAt = i.CreatedAt
            };
        }).ToList();

        return ServiceResult<IReadOnlyList<FriendInviteDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<FriendInviteDto>> CreateFriendInviteAsync(
        CreateFriendInviteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<FriendInviteDto>.Fail("Unauthorized", 401);
        }

        if (!PasswordResetCodeRules.TryNormalizeEmail(request.Email, out var inviteeEmail, out var formatError))
        {
            return ServiceResult<FriendInviteDto>.Fail(formatError!, 400);
        }

        var inviterId = _currentUser.UserId.Value;
        var inviter = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == inviterId, cancellationToken);
        if (inviter is null)
        {
            return ServiceResult<FriendInviteDto>.Fail("Unauthorized", 401);
        }

        if (string.Equals(inviter.Email, inviteeEmail, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<FriendInviteDto>.Fail("Cannot invite yourself", 400);
        }

        var inviteeUser = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == inviteeEmail && u.IsActive, cancellationToken);

        if (inviteeUser is not null
            && await AlreadyFriendsAsync(inviterId, inviteeUser.Id, cancellationToken))
        {
            return ServiceResult<FriendInviteDto>.Fail("Already friends", 409);
        }

        var existingPending = await _db.FriendInvites
            .FirstOrDefaultAsync(
                i => i.Status == FriendInviteStatus.Pending
                     && i.InviterUserId == inviterId
                     && i.InviteeEmail == inviteeEmail,
                cancellationToken);

        if (existingPending is not null)
        {
            return ServiceResult<FriendInviteDto>.Ok(MapOutgoingInvite(existingPending, inviteeUser));
        }

        var invite = new FriendInvite
        {
            InviterUserId = inviterId,
            InviteeUserId = inviteeUser?.Id,
            InviteeEmail = inviteeEmail,
            Status = FriendInviteStatus.Pending
        };
        _db.FriendInvites.Add(invite);
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<FriendInviteDto>.Created(MapOutgoingInvite(invite, inviteeUser));
    }

    public async Task<ServiceResult> AcceptFriendInviteAsync(Guid inviteId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult.Fail("Unauthorized", 401);
        }

        var invite = await _db.FriendInvites
            .Include(i => i.Inviter)
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);

        if (invite is null || invite.Status != FriendInviteStatus.Pending)
        {
            return ServiceResult.NotFound("Invite not found");
        }

        if (invite.InviterUserId == userId)
        {
            return ServiceResult.Fail("Cannot accept your own invite", 400);
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

        invite.InviteeUserId ??= userId;
        if (await AlreadyFriendsAsync(invite.InviterUserId, userId, cancellationToken))
        {
            invite.Status = FriendInviteStatus.Accepted;
            await _db.SaveChangesAsync(cancellationToken);
            return ServiceResult.NoContent();
        }

        var inviter = invite.Inviter;
        _db.Friends.Add(CreateFriendRow(invite.InviterUserId, user));
        _db.Friends.Add(CreateFriendRow(userId, inviter));
        invite.Status = FriendInviteStatus.Accepted;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult.NoContent();
    }

    public async Task<ServiceResult> RejectFriendInviteAsync(Guid inviteId, CancellationToken cancellationToken = default)
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

        var invite = await _db.FriendInvites.FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken);
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

    private static FriendInviteDto MapOutgoingInvite(FriendInvite invite, UserAccount? inviteeUser)
    {
        var displayName = inviteeUser?.Nickname ?? inviteeUser?.Email;
        return new FriendInviteDto
        {
            Id = invite.Id,
            Email = invite.InviteeEmail,
            Name = displayName,
            Nickname = displayName,
            AvatarUrl = inviteeUser?.AvatarUrl,
            Status = "pending_outgoing",
            CreatedAt = invite.CreatedAt
        };
    }

    private static Friend CreateFriendRow(Guid ownerUserId, UserAccount counterparty)
    {
        var name = counterparty.Nickname ?? counterparty.Email;
        return new Friend
        {
            OwnerUserId = ownerUserId,
            FriendUserId = counterparty.Id,
            Name = name,
            AvatarUrl = counterparty.AvatarUrl
        };
    }

    private async Task<bool> AlreadyFriendsAsync(Guid userA, Guid userB, CancellationToken cancellationToken)
    {
        return await _db.Friends.AsNoTracking().AnyAsync(
            f => f.OwnerUserId == userA && f.FriendUserId == userB,
            cancellationToken);
    }
}
