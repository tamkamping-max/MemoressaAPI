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
        var memberFailure = await AudienceConnectionValidation.ValidateFamilyMemberIdsAsync(
            db,
            photo.FamilyId,
            distinctMemberIds,
            cancellationToken);
        if (memberFailure is not null)
        {
            return memberFailure;
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
        var friendFailure = await AudienceConnectionValidation.ValidateFriendIdsAsync(
            db,
            ownerUserId,
            distinctFriendIds,
            cancellationToken);
        if (friendFailure is not null)
        {
            return friendFailure;
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
