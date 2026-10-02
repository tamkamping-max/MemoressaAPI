using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class ActivityAlbumAccess
{
    public static async Task<ActivityAlbum?> ResolveAccessibleAsync(
        IMemoressaDbContext db,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var activity = await FindActivityGraphAsync(db, activityId, forUpdate: false, cancellationToken);
        if (activity is null)
        {
            return null;
        }

        if (!await CanAccessAsync(db, activity, userId, cancellationToken))
        {
            return null;
        }

        return activity;
    }

    public static async Task<ActivityAlbum?> ResolveForUpdateAccessibleAsync(
        IMemoressaDbContext db,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var activity = await FindActivityGraphAsync(db, activityId, forUpdate: true, cancellationToken);
        if (activity is null)
        {
            return null;
        }

        if (!await CanAccessAsync(db, activity, userId, cancellationToken))
        {
            return null;
        }

        return activity;
    }

    public static async Task<ActivityAlbum?> ResolveAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var trimmed = activityId.Trim();
        var local = await ApplyActivityGraphIncludes(
                db.ActivityAlbums.AsNoTracking(),
                forUpdate: false)
            .FirstOrDefaultAsync(
                a => a.FamilyId == familyId
                     && (a.ExternalId == trimmed || a.Id.ToString() == trimmed),
                cancellationToken);

        if (local is not null)
        {
            return local;
        }

        return await ResolveAccessibleAsync(db, userId, activityId, cancellationToken);
    }

    public static async Task<ActivityAlbum?> ResolveForUpdateAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken)
    {
        var trimmed = activityId.Trim();
        var local = await ApplyActivityGraphIncludes(db.ActivityAlbums, forUpdate: true)
            .FirstOrDefaultAsync(
                a => a.FamilyId == familyId
                     && (a.ExternalId == trimmed || a.Id.ToString() == trimmed),
                cancellationToken);

        if (local is not null)
        {
            return local;
        }

        return await ResolveForUpdateAccessibleAsync(db, userId, activityId, cancellationToken);
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

    /// <summary>
    /// Link existing photos: activity creator, or caller who uploaded every photo in the batch.
    /// </summary>
    public static async Task<bool> CanLinkPhotosAsync(
        IMemoressaDbContext db,
        ActivityAlbum activity,
        Guid userId,
        IReadOnlyList<Guid> photoIds,
        CancellationToken cancellationToken)
    {
        if (activity.Status != ActivityAlbumStatus.InProgress)
        {
            return false;
        }

        if (!await CanAccessAsync(db, activity, userId, cancellationToken))
        {
            return false;
        }

        if (activity.CreatorUserId == userId)
        {
            return true;
        }

        if (photoIds.Count == 0)
        {
            return false;
        }

        var distinct = photoIds.Distinct().ToList();
        var ownedCount = await db.Photos.AsNoTracking()
            .CountAsync(
                p => distinct.Contains(p.Id)
                     && !p.IsHidden
                     && p.UploadedByUserId == userId,
                cancellationToken);

        return ownedCount == distinct.Count;
    }

    public static string NewExternalId() => $"act_{Guid.NewGuid():N}"[..20];

    private static async Task<ActivityAlbum?> FindActivityGraphAsync(
        IMemoressaDbContext db,
        string activityId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var trimmed = activityId.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return await ApplyActivityGraphIncludes(
                forUpdate ? db.ActivityAlbums : db.ActivityAlbums.AsNoTracking(),
                forUpdate)
            .FirstOrDefaultAsync(
                a => a.ExternalId == trimmed || a.Id.ToString() == trimmed,
                cancellationToken);
    }

    private static IQueryable<ActivityAlbum> ApplyActivityGraphIncludes(
        IQueryable<ActivityAlbum> query,
        bool forUpdate)
    {
        query = query
            .Include(a => a.AgendaItems)
            .Include(a => a.FamilyMembers)
            .ThenInclude(fm => fm.FamilyMember)
            .Include(a => a.Friends)
            .ThenInclude(f => f.Friend);

        return query;
    }
}
