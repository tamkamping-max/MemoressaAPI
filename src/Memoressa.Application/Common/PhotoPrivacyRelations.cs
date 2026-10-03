using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class PhotoPrivacyRelations
{
    public static async Task<ServiceResult?> ReplaceMembersAsync(
        IMemoressaDbContext db,
        Photo photo,
        IReadOnlyList<Guid> memberIds,
        CancellationToken cancellationToken)
    {
        var distinctMemberIds = memberIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctMemberIds.Count > 0)
        {
            var validCount = await db.FamilyMembers.AsNoTracking()
                .CountAsync(
                    m => m.FamilyId == photo.FamilyId && distinctMemberIds.Contains(m.Id),
                    cancellationToken);

            if (validCount != distinctMemberIds.Count)
            {
                return ServiceResult.Fail(
                    "One or more family members are invalid for this photo",
                    403);
            }
        }

        if (db.Database.IsRelational())
        {
            await db.PhotoMembers
                .Where(pm => pm.PhotoId == photo.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var existing = await db.PhotoMembers
                .Where(pm => pm.PhotoId == photo.Id)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                db.PhotoMembers.RemoveRange(existing);
            }
        }

        photo.PhotoMembers.Clear();
        foreach (var memberId in distinctMemberIds)
        {
            db.PhotoMembers.Add(new PhotoMember
            {
                PhotoId = photo.Id,
                FamilyMemberId = memberId
            });
        }

        return null;
    }

    public static async Task<ServiceResult?> ReplaceFriendsAsync(
        IMemoressaDbContext db,
        Photo photo,
        Guid ownerUserId,
        IReadOnlyList<Guid> friendIds,
        CancellationToken cancellationToken)
    {
        var distinctFriendIds = friendIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctFriendIds.Count > 0)
        {
            var validCount = await db.Friends.AsNoTracking()
                .CountAsync(
                    f => f.OwnerUserId == ownerUserId && distinctFriendIds.Contains(f.Id),
                    cancellationToken);

            if (validCount != distinctFriendIds.Count)
            {
                return ServiceResult.Fail(
                    "One or more friends are invalid for this photo",
                    403);
            }
        }

        if (db.Database.IsRelational())
        {
            await db.PhotoFriends
                .Where(pf => pf.PhotoId == photo.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var existing = await db.PhotoFriends
                .Where(pf => pf.PhotoId == photo.Id)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                db.PhotoFriends.RemoveRange(existing);
            }
        }

        photo.PhotoFriends.Clear();
        foreach (var friendId in distinctFriendIds)
        {
            db.PhotoFriends.Add(new PhotoFriend
            {
                PhotoId = photo.Id,
                FriendId = friendId
            });
        }

        return null;
    }
}
