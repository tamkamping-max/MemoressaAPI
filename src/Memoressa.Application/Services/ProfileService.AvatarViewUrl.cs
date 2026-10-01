using Memoressa.Application.Abstractions;
using Memoressa.Application.Common;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Services;

public partial class ProfileService
{
    public async Task<ServiceResult<AvatarViewUrlResponseDto>> GetAvatarViewUrlAsync(
        Guid? friendId,
        Guid? friendUserId,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return ServiceResult<AvatarViewUrlResponseDto>.Fail("Unauthorized", 401);
        }

        if (!friendId.HasValue && !friendUserId.HasValue)
        {
            return ServiceResult<AvatarViewUrlResponseDto>.Fail("friendId or friendUserId is required", 400);
        }

        var userId = _currentUser.UserId.Value;
        var user = await _db.UserAccounts.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return ServiceResult<AvatarViewUrlResponseDto>.Fail("Unauthorized", 401);
        }

        var storedKey = await ResolveFriendAvatarKeyAsync(
            userId,
            user.Email,
            friendId,
            friendUserId,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(storedKey))
        {
            return ServiceResult<AvatarViewUrlResponseDto>.NotFound("Avatar not found");
        }

        var viewUrl = await _avatarUrls.ResolveForResponseAsync(storedKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(viewUrl))
        {
            return ServiceResult<AvatarViewUrlResponseDto>.NotFound("Avatar not found");
        }

        return ServiceResult<AvatarViewUrlResponseDto>.Ok(new AvatarViewUrlResponseDto
        {
            ViewUrl = viewUrl,
            AvatarUrl = StoredObjectUrl.IsPresignableObjectKey(storedKey) ? storedKey : null
        });
    }

    private async Task<string?> ResolveFriendAvatarKeyAsync(
        Guid ownerUserId,
        string ownerEmail,
        Guid? friendId,
        Guid? friendUserId,
        CancellationToken cancellationToken)
    {
        if (friendId.HasValue)
        {
            var fromFriend = await TryFriendRowAvatarKeyAsync(ownerUserId, friendId.Value, friendUserId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(fromFriend))
            {
                return fromFriend;
            }

            var fromInvite = await TryInviteAvatarKeyAsync(
                ownerUserId,
                ownerEmail,
                inviteId: friendId.Value,
                counterpartyUserId: friendUserId,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(fromInvite))
            {
                return fromInvite;
            }
        }

        if (friendUserId.HasValue)
        {
            var fromFriendUser = await TryFriendRowAvatarKeyAsync(
                ownerUserId,
                friendId: null,
                friendUserId: friendUserId.Value,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(fromFriendUser))
            {
                return fromFriendUser;
            }

            return await TryInviteAvatarKeyAsync(
                ownerUserId,
                ownerEmail,
                inviteId: null,
                counterpartyUserId: friendUserId.Value,
                cancellationToken);
        }

        return null;
    }

    private async Task<string?> TryFriendRowAvatarKeyAsync(
        Guid ownerUserId,
        Guid? friendId,
        Guid? friendUserId,
        CancellationToken cancellationToken)
    {
        var query = _db.Friends.AsNoTracking().Where(f => f.OwnerUserId == ownerUserId);
        if (friendId.HasValue)
        {
            query = query.Where(f => f.Id == friendId.Value);
        }

        if (friendUserId.HasValue)
        {
            query = query.Where(f => f.FriendUserId == friendUserId.Value);
        }

        var friend = await query.FirstOrDefaultAsync(cancellationToken);
        if (friend is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(friend.AvatarUrl))
        {
            return friend.AvatarUrl;
        }

        if (!friend.FriendUserId.HasValue)
        {
            return null;
        }

        return await _db.UserAccounts.AsNoTracking()
            .Where(u => u.Id == friend.FriendUserId.Value)
            .Select(u => u.AvatarUrl)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<string?> TryInviteAvatarKeyAsync(
        Guid ownerUserId,
        string ownerEmail,
        Guid? inviteId,
        Guid? counterpartyUserId,
        CancellationToken cancellationToken)
    {
        var query = _db.FriendInvites.AsNoTracking()
            .Include(i => i.Inviter)
            .Include(i => i.Invitee)
            .Where(i => i.Status == FriendInviteStatus.Pending
                        && (i.InviterUserId == ownerUserId
                            || i.InviteeUserId == ownerUserId
                            || i.InviteeEmail == ownerEmail));

        if (inviteId.HasValue)
        {
            query = query.Where(i => i.Id == inviteId.Value);
        }

        if (counterpartyUserId.HasValue)
        {
            query = query.Where(i =>
                i.InviterUserId == counterpartyUserId.Value
                || i.InviteeUserId == counterpartyUserId.Value);
        }

        var invite = await query.OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (invite is null)
        {
            return null;
        }

        UserAccount? counterparty = null;
        if (invite.InviterUserId == ownerUserId)
        {
            counterparty = invite.Invitee;
        }
        else if (invite.InviteeUserId == ownerUserId
                 || string.Equals(invite.InviteeEmail, ownerEmail, StringComparison.OrdinalIgnoreCase))
        {
            counterparty = invite.Inviter;
        }

        if (counterparty is null && counterpartyUserId.HasValue)
        {
            counterparty = await _db.UserAccounts.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == counterpartyUserId.Value, cancellationToken);
        }

        return counterparty?.AvatarUrl;
    }
}
