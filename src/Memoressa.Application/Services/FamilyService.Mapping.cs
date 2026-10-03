using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Services;

public partial class FamilyService
{
    private async Task<FamilyMemberDto> MapAcceptedMemberAsync(
        FamilyMember member,
        UserAccount? linkedUser,
        CancellationToken cancellationToken)
    {
        var dto = await _avatarUrls.ToFamilyMemberDtoAsync(member, cancellationToken);
        FamilyMemberUserSummaryDto? linked = null;
        if (linkedUser is not null)
        {
            linked = new FamilyMemberUserSummaryDto
            {
                Id = linkedUser.Id,
                Nickname = linkedUser.Nickname,
                Email = linkedUser.Email,
                AvatarUrl = await _avatarUrls.ResolveForResponseAsync(linkedUser.AvatarUrl, cancellationToken)
            };
        }

        var displayName = linkedUser?.Nickname ?? linkedUser?.Email;
        return dto with
        {
            Nickname = dto.Nickname ?? displayName,
            Email = linkedUser?.Email,
            LinkedUserId = member.LinkedUserId,
            LinkedUser = linked,
            ConnectionStatus = "accepted",
            AssignedToTree = member.AssignedToTree
        };
    }

    private async Task<FamilyMemberDto> MapPendingOutgoingInviteAsync(
        FamilyMemberInvite invite,
        UserAccount? inviteeUser,
        CancellationToken cancellationToken)
    {
        var displayName = inviteeUser?.Nickname ?? inviteeUser?.Email ?? invite.InviteeEmail;
        FamilyMemberUserSummaryDto? linked = null;
        if (inviteeUser is not null)
        {
            linked = new FamilyMemberUserSummaryDto
            {
                Id = inviteeUser.Id,
                Nickname = inviteeUser.Nickname,
                Email = inviteeUser.Email,
                AvatarUrl = await _avatarUrls.ResolveForResponseAsync(inviteeUser.AvatarUrl, cancellationToken)
            };
        }

        return new FamilyMemberDto
        {
            Id = invite.Id,
            InviteId = invite.Id,
            Name = displayName ?? invite.InviteeEmail,
            Nickname = displayName,
            Email = invite.InviteeEmail,
            AvatarUrl = await _avatarUrls.ResolveForResponseAsync(inviteeUser?.AvatarUrl, cancellationToken),
            LinkedUserId = inviteeUser?.Id,
            LinkedUser = linked,
            ConnectionStatus = "pending_outgoing",
            AssignedToTree = false,
            FaceRecognitionEnabled = true,
            Generation = Generation.Self
        };
    }

    private static FamilyMemberInviteDto MapFamilyMemberInviteDto(
        FamilyMemberInvite invite,
        bool incomingForViewer,
        UserAccount inviter,
        UserAccount? invitee)
    {
        var counterparty = incomingForViewer ? inviter : invitee;
        var displayName = counterparty?.Nickname ?? counterparty?.Email
                          ?? (incomingForViewer ? inviter.Email : invite.InviteeEmail);

        FamilyMemberUserSummaryDto MapUser(UserAccount user) => new()
        {
            Id = user.Id,
            Nickname = user.Nickname,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl
        };

        return new FamilyMemberInviteDto
        {
            Id = invite.Id,
            Email = incomingForViewer ? inviter.Email : invite.InviteeEmail,
            Name = displayName,
            Nickname = displayName,
            AvatarUrl = counterparty?.AvatarUrl,
            Status = incomingForViewer ? "pending_incoming" : "pending_outgoing",
            CreatedAt = invite.CreatedAt,
            Inviter = MapUser(inviter),
            Invitee = invitee is null ? null : MapUser(invitee)
        };
    }
}
