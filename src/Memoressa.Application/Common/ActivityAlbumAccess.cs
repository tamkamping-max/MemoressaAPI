using Memoressa.Application.Abstractions;
using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class ActivityAlbumAccess
{
    public static async Task<ActivityAlbum?> ResolveAsync(
        IMemoressaDbContext db,
        Guid familyId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var trimmed = activityId.Trim();
        return await db.ActivityAlbums
            .Include(a => a.FamilyMembers)
            .Include(a => a.Friends)
            .Include(a => a.AgendaItems)
            .FirstOrDefaultAsync(
                a => a.FamilyId == familyId
                     && (a.ExternalId == trimmed || a.Id.ToString() == trimmed),
                cancellationToken);
    }

    public static async Task<bool> CanAccessAsync(
        IMemoressaDbContext db,
        ActivityAlbum activity,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (activity.CreatorUserId == userId)
        {
            return true;
        }

        var isFamilyUser = await db.FamilyMemberships.AsNoTracking()
            .AnyAsync(m => m.FamilyId == activity.FamilyId && m.UserId == userId, cancellationToken);
        if (isFamilyUser && activity.PrivacyScope == UploadPrivacyScope.Family)
        {
            return true;
        }

        var linkedMemberIds = await db.FamilyMembers.AsNoTracking()
            .Where(m => m.FamilyId == activity.FamilyId && m.LinkedUserId == userId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (activity.FamilyMembers.Any(fm => linkedMemberIds.Contains(fm.FamilyMemberId)))
        {
            return true;
        }

        var friendIds = activity.Friends.Where(f => f.FriendId.HasValue).Select(f => f.FriendId!.Value).ToList();
        if (friendIds.Count == 0)
        {
            return false;
        }

        return await db.Friends.AsNoTracking()
            .AnyAsync(
                f => friendIds.Contains(f.Id) && f.FriendUserId == userId,
                cancellationToken);
    }

    public static async Task<bool> CanUploadToAsync(
        IMemoressaDbContext db,
        ActivityAlbum activity,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (activity.Status != ActivityAlbumStatus.InProgress)
        {
            return false;
        }

        return await CanAccessAsync(db, activity, userId, cancellationToken);
    }

    public static string NewExternalId() => $"act_{Guid.NewGuid():N}"[..20];
}
