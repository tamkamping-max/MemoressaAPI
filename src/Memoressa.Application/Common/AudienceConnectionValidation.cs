using Memoressa.Application.Abstractions;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class AudienceConnectionValidation
{
    public const string NotConnectedCode = "audience_not_connected";

    public const string NotConnectedMessage =
        "Audience includes a family member or friend that is not an accepted connection";

    public static async Task<ServiceResult?> ValidateFamilyMemberIdsAsync(
        IMemoressaDbContext db,
        Guid familyId,
        IReadOnlyList<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var ids = memberIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        var acceptedCount = await db.FamilyMembers.AsNoTracking()
            .CountAsync(
                m => m.FamilyId == familyId && ids.Contains(m.Id),
                cancellationToken);

        if (acceptedCount == ids.Count)
        {
            return null;
        }

        if (await HasPendingFamilyInviteIdsAsync(db, familyId, ids, cancellationToken))
        {
            return FailNotConnected();
        }

        return ServiceResult.Fail(
            "One or more family members are invalid for this family",
            400);
    }

    public static async Task<ServiceResult?> ValidateFriendIdsAsync(
        IMemoressaDbContext db,
        Guid ownerUserId,
        IReadOnlyList<Guid> friendIds,
        CancellationToken cancellationToken)
    {
        var ids = friendIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return null;
        }

        var acceptedCount = await db.Friends.AsNoTracking()
            .CountAsync(
                f => f.OwnerUserId == ownerUserId && ids.Contains(f.Id),
                cancellationToken);

        if (acceptedCount == ids.Count)
        {
            return null;
        }

        if (await HasPendingFriendInviteIdsForUserAsync(db, ownerUserId, ids, cancellationToken))
        {
            return FailNotConnected();
        }

        return ServiceResult.Fail(
            "One or more friends are invalid for this user",
            400);
    }

    public static async Task<bool> HasPendingFamilyInviteIdsAsync(
        IMemoressaDbContext db,
        Guid familyId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken) =>
        await db.FamilyMemberInvites.AsNoTracking()
            .AnyAsync(
                i => i.Status == FriendInviteStatus.Pending
                     && i.FamilyId == familyId
                     && ids.Contains(i.Id),
                cancellationToken);

    public static async Task<bool> HasPendingFriendInviteIdsForUserAsync(
        IMemoressaDbContext db,
        Guid ownerUserId,
        IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken)
    {
        var userEmail = await db.UserAccounts.AsNoTracking()
            .Where(u => u.Id == ownerUserId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return await db.FriendInvites.AsNoTracking()
            .AnyAsync(
                i => i.Status == FriendInviteStatus.Pending
                     && ids.Contains(i.Id)
                     && (i.InviterUserId == ownerUserId
                         || i.InviteeUserId == ownerUserId
                         || (userEmail != null
                             && string.Equals(i.InviteeEmail, userEmail, StringComparison.OrdinalIgnoreCase))),
                cancellationToken);
    }

    private static ServiceResult FailNotConnected() =>
        ServiceResult.Fail(NotConnectedMessage, 400, NotConnectedCode);
}
