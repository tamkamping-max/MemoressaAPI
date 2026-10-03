using Memoressa.Application.Abstractions;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class FamilyInviteConnectionRules
{
    public static Task<bool> IsLinkedInFamilyAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid linkedUserId,
        CancellationToken cancellationToken) =>
        db.FamilyMembers.AsNoTracking()
            .AnyAsync(m => m.FamilyId == familyId && m.LinkedUserId == linkedUserId, cancellationToken);

    public static Task<bool> HasPendingFamilyInviteAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid inviteeUserId,
        string inviteeEmail,
        CancellationToken cancellationToken) =>
        db.FamilyMemberInvites.AsNoTracking()
            .AnyAsync(
                i => i.Status == FriendInviteStatus.Pending
                     && i.FamilyId == familyId
                     && (i.InviteeUserId == inviteeUserId
                         || string.Equals(i.InviteeEmail, inviteeEmail, StringComparison.OrdinalIgnoreCase)),
                cancellationToken);

    public static async Task<bool> IsAlreadyConnectedAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid inviteeUserId,
        string inviteeEmail,
        CancellationToken cancellationToken)
    {
        if (await IsLinkedInFamilyAsync(db, familyId, inviteeUserId, cancellationToken))
        {
            return true;
        }

        return await HasPendingFamilyInviteAsync(db, familyId, inviteeUserId, inviteeEmail, cancellationToken);
    }
}
