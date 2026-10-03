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
        var profile = await BuildAcceptedMemberProfileAsync(member, linkedUser, cancellationToken);
        var displayName = linkedUser?.Nickname ?? linkedUser?.Email;

        return dto with
        {
            FamilyMemberId = member.Id,
            Nickname = dto.Nickname ?? displayName ?? dto.Name,
            Email = linkedUser?.Email,
            LinkedUserId = member.LinkedUserId,
            LinkedUser = profile,
            Counterparty = profile,
            ConnectionStatus = "accepted",
            AssignedToTree = member.AssignedToTree
        };
    }

    private async Task<FamilyMemberUserSummaryDto> BuildAcceptedMemberProfileAsync(
        FamilyMember member,
        UserAccount? linkedUser,
        CancellationToken cancellationToken)
    {
        var avatarStored = linkedUser?.AvatarUrl ?? member.AvatarUrl;
        var name = linkedUser?.Nickname ?? linkedUser?.Email ?? member.Name;
        return new FamilyMemberUserSummaryDto
        {
            FamilyMemberId = member.Id,
            Id = linkedUser?.Id,
            Name = name,
            Nickname = member.Nickname ?? linkedUser?.Nickname,
            Email = linkedUser?.Email,
            AvatarUrl = await _avatarUrls.ResolveForResponseAsync(avatarStored, cancellationToken)
        };
    }

    private async Task<FamilyMemberDto> MapPendingOutgoingInviteAsync(
        FamilyMemberInvite invite,
        UserAccount? inviteeUser,
        CancellationToken cancellationToken)
    {
        var displayName = inviteeUser?.Nickname ?? inviteeUser?.Email ?? invite.InviteeEmail;
        var profile = new FamilyMemberUserSummaryDto
        {
            Id = inviteeUser?.Id,
            Name = displayName,
            Nickname = displayName,
            Email = invite.InviteeEmail,
            AvatarUrl = await _avatarUrls.ResolveForResponseAsync(inviteeUser?.AvatarUrl, cancellationToken)
        };

        return new FamilyMemberDto
        {
            Id = invite.Id,
            FamilyMemberId = null,
            InviteId = invite.Id,
            Name = displayName ?? invite.InviteeEmail,
            Nickname = displayName,
            Email = invite.InviteeEmail,
            AvatarUrl = profile.AvatarUrl,
            LinkedUserId = inviteeUser?.Id,
            LinkedUser = profile,
            Counterparty = profile,
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
            Name = user.Nickname ?? user.Email,
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
