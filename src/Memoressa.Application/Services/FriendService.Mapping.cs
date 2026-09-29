using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Application.Interfaces;

namespace Memoressa.Application.Services;

public partial class FriendService
{
    private async Task<FriendDto> MapAcceptedFriendDtoAsync(
        Friend friend,
        UserAccount? linkedUser,
        int activityCount,
        CancellationToken cancellationToken)
    {
        var storedAvatar = friend.AvatarUrl ?? linkedUser?.AvatarUrl;
        var avatarUrl = await _avatarUrls.ResolveForResponseAsync(storedAvatar, cancellationToken);
        FriendUserSummaryDto? friendUser = null;
        if (linkedUser is not null)
        {
            friendUser = new FriendUserSummaryDto
            {
                Id = linkedUser.Id,
                Nickname = linkedUser.Nickname,
                Email = linkedUser.Email,
                AvatarUrl = await _avatarUrls.ResolveForResponseAsync(linkedUser.AvatarUrl, cancellationToken)
            };
        }

        var displayName = linkedUser?.Nickname ?? linkedUser?.Email ?? friend.Name;
        return new FriendDto
        {
            Id = friend.Id,
            Name = friend.Name,
            Nickname = displayName,
            Email = linkedUser?.Email,
            AvatarUrl = avatarUrl,
            FriendUserId = friend.FriendUserId,
            FriendUser = friendUser,
            FrameLinked = friend.FrameLinked,
            Status = "accepted",
            SharedActivityCount = activityCount > 0 ? activityCount : friend.SharedMemoryCount,
            SharedMemoryCount = friend.SharedMemoryCount
        };
    }

    private async Task<FriendDto> MapPendingInviteFriendDtoAsync(
        FriendInvite invite,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var outgoing = invite.InviterUserId == currentUserId;
        var counterparty = outgoing ? invite.Invitee : invite.Inviter;
        var displayName = counterparty?.Nickname ?? counterparty?.Email ?? invite.InviteeEmail;
        var storedAvatar = counterparty?.AvatarUrl;
        FriendUserSummaryDto? friendUser = null;
        if (counterparty is not null)
        {
            friendUser = new FriendUserSummaryDto
            {
                Id = counterparty.Id,
                Nickname = counterparty.Nickname,
                Email = counterparty.Email,
                AvatarUrl = await _avatarUrls.ResolveForResponseAsync(counterparty.AvatarUrl, cancellationToken)
            };
        }

        return new FriendDto
        {
            Id = invite.Id,
            Name = displayName ?? invite.InviteeEmail,
            Nickname = displayName,
            Email = outgoing ? invite.InviteeEmail : invite.Inviter.Email,
            AvatarUrl = await _avatarUrls.ResolveForResponseAsync(storedAvatar, cancellationToken),
            FriendUserId = counterparty?.Id ?? invite.InviteeUserId,
            FriendUser = friendUser,
            Status = outgoing ? "pending_outgoing" : "pending_incoming",
            SharedActivityCount = 0,
            SharedMemoryCount = 0
        };
    }
}
