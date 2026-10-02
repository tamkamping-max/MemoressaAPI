using Memoressa.Application.Abstractions;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Memoressa.Application.Common;

public static class ActivityAlbumAccess
{
    public static Task<ActivityAlbumResolveResult> ResolveAccessibleAsync(
        IMemoressaDbContext db,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken,
        Guid? creatorUserId = null,
        ActivityAlbumAmbiguityPolicy ambiguityPolicy = ActivityAlbumAmbiguityPolicy.PreferViewerAsCreator) =>
        ResolveForViewerAsync(
            db,
            userId,
            new ActivityAlbumResolveRequest(
                activityId,
                creatorUserId,
                HomeFamilyId: null,
                ForUpdate: false,
                ambiguityPolicy),
            cancellationToken);

    public static Task<ActivityAlbumResolveResult> ResolveForUpdateAccessibleAsync(
        IMemoressaDbContext db,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken,
        Guid? creatorUserId = null,
        ActivityAlbumAmbiguityPolicy ambiguityPolicy = ActivityAlbumAmbiguityPolicy.PreferViewerAsCreator) =>
        ResolveForViewerAsync(
            db,
            userId,
            new ActivityAlbumResolveRequest(
                activityId,
                creatorUserId,
                HomeFamilyId: null,
                ForUpdate: true,
                ambiguityPolicy),
            cancellationToken);

    public static Task<ActivityAlbumResolveResult> ResolveAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken,
        Guid? creatorUserId = null,
        ActivityAlbumAmbiguityPolicy ambiguityPolicy = ActivityAlbumAmbiguityPolicy.PreferViewerAsCreator) =>
        ResolveForViewerAsync(
            db,
            userId,
            new ActivityAlbumResolveRequest(
                activityId,
                creatorUserId,
                familyId,
                ForUpdate: false,
                ambiguityPolicy),
            cancellationToken);

    public static Task<ActivityAlbumResolveResult> ResolveForUpdateAsync(
        IMemoressaDbContext db,
        Guid familyId,
        Guid userId,
        string activityId,
        CancellationToken cancellationToken,
        Guid? creatorUserId = null,
        ActivityAlbumAmbiguityPolicy ambiguityPolicy = ActivityAlbumAmbiguityPolicy.PreferViewerAsCreator) =>
        ResolveForViewerAsync(
            db,
            userId,
            new ActivityAlbumResolveRequest(
                activityId,
                creatorUserId,
                familyId,
                ForUpdate: true,
                ambiguityPolicy),
            cancellationToken);

    public static async Task<ActivityAlbumResolveResult> ResolveForViewerAsync(
        IMemoressaDbContext db,
        Guid userId,
        ActivityAlbumResolveRequest request,
        CancellationToken cancellationToken)
    {
        var trimmed = request.ActivityId.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return ActivityAlbumResolveResult.NotFound();
        }

        var candidates = await LoadMatchingActivitiesAsync(
            db,
            trimmed,
            request.ForUpdate,
            familyId: null,
            cancellationToken);

        return await PickAccessibleActivityAsync(
            db,
            userId,
            candidates,
            request.CreatorUserId,
            request.AmbiguityPolicy,
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

    private static async Task<ActivityAlbumResolveResult> PickAccessibleActivityAsync(
        IMemoressaDbContext db,
        Guid userId,
        IReadOnlyList<ActivityAlbum> candidates,
        Guid? creatorUserId,
        ActivityAlbumAmbiguityPolicy ambiguityPolicy,
        CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return ActivityAlbumResolveResult.NotFound();
        }

        var accessible = new List<ActivityAlbum>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (await CanAccessAsync(db, candidate, userId, cancellationToken))
            {
                accessible.Add(candidate);
            }
        }

        if (accessible.Count == 0)
        {
            return ActivityAlbumResolveResult.NotFound();
        }

        if (creatorUserId.HasValue)
        {
            accessible = accessible.Where(a => a.CreatorUserId == creatorUserId.Value).ToList();
            if (accessible.Count == 0)
            {
                return ActivityAlbumResolveResult.NotFound();
            }

            if (accessible.Count == 1)
            {
                return ActivityAlbumResolveResult.Found(accessible[0]);
            }

            return ActivityAlbumResolveResult.Ambiguous();
        }

        if (accessible.Count == 1)
        {
            return ActivityAlbumResolveResult.Found(accessible[0]);
        }

        var viewerOwned = accessible.Where(a => a.CreatorUserId == userId).ToList();
        if (viewerOwned.Count == 1)
        {
            return ActivityAlbumResolveResult.Found(viewerOwned[0]);
        }

        if (viewerOwned.Count > 1)
        {
            var picked = viewerOwned
                .OrderByDescending(a => a.CreatedAt)
                .ThenByDescending(a => a.Id)
                .First();
            if (ambiguityPolicy == ActivityAlbumAmbiguityPolicy.FailIfAmbiguous)
            {
                var second = viewerOwned.Count(a =>
                    a.Id != picked.Id
                    && a.CreatedAt == picked.CreatedAt);
                if (second > 0)
                {
                    return ActivityAlbumResolveResult.Ambiguous();
                }
            }

            return ActivityAlbumResolveResult.Found(picked);
        }

        if (ambiguityPolicy == ActivityAlbumAmbiguityPolicy.FailIfAmbiguous)
        {
            return ActivityAlbumResolveResult.Ambiguous();
        }

        var fallback = accessible
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .First();
        return ActivityAlbumResolveResult.Found(fallback);
    }

    private static async Task<List<ActivityAlbum>> LoadMatchingActivitiesAsync(
        IMemoressaDbContext db,
        string trimmedActivityId,
        bool forUpdate,
        Guid? familyId,
        CancellationToken cancellationToken)
    {
        var baseQuery = forUpdate ? db.ActivityAlbums : db.ActivityAlbums.AsNoTracking();
        var query = ApplyActivityGraphIncludes(baseQuery, forUpdate);

        if (familyId.HasValue)
        {
            query = query.Where(a => a.FamilyId == familyId.Value);
        }

        if (Guid.TryParse(trimmedActivityId, out var internalId))
        {
            return await query.Where(a => a.Id == internalId).ToListAsync(cancellationToken);
        }

        return await query.Where(a => a.ExternalId == trimmedActivityId).ToListAsync(cancellationToken);
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
