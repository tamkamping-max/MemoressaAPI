using Memoressa.Application.Abstractions;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class FriendInviteConnectionRules
{
    public static async Task<bool> AreAlreadyFriendsAsync(
        IMemoressaDbContext db,
        Guid userAId,
        Guid userBId,
        CancellationToken cancellationToken) =>
        await db.Friends.AsNoTracking().AnyAsync(
            f => f.OwnerUserId == userAId && f.FriendUserId == userBId,
            cancellationToken);

    public static async Task<bool> HasPendingInviteBetweenAsync(
        IMemoressaDbContext db,
        Guid userAId,
        string userAEmail,
        Guid userBId,
        string userBEmail,
        CancellationToken cancellationToken) =>
        await db.FriendInvites.AsNoTracking().AnyAsync(
            i => i.Status == FriendInviteStatus.Pending
                 && (
                     (i.InviterUserId == userAId
                      && (i.InviteeUserId == userBId
                          || string.Equals(i.InviteeEmail, userBEmail, StringComparison.OrdinalIgnoreCase)))
                     || (i.InviterUserId == userBId
                         && (i.InviteeUserId == userAId
                             || string.Equals(i.InviteeEmail, userAEmail, StringComparison.OrdinalIgnoreCase)))),
            cancellationToken);

    public static async Task<bool> IsAlreadyConnectedAsync(
        IMemoressaDbContext db,
        Guid inviterId,
        string inviterEmail,
        Guid inviteeUserId,
        string inviteeEmail,
        CancellationToken cancellationToken)
    {
        if (await AreAlreadyFriendsAsync(db, inviterId, inviteeUserId, cancellationToken))
        {
            return true;
        }

        return await HasPendingInviteBetweenAsync(
            db,
            inviterId,
            inviterEmail,
            inviteeUserId,
            inviteeEmail,
            cancellationToken);
    }
}
