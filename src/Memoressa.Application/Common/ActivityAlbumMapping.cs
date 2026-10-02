using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public readonly record struct ActivityAlbumCreatorSummary(string DisplayName, string? AvatarUrl);

public readonly record struct ActivityAlbumMapContext(
    Guid? ViewerUserId,
    ActivityAlbumCreatorSummary? Creator = null);

public static class ActivityAlbumMapping
{
    public static string TypeToApiString(ActivityAlbumType type) => type switch
    {
        ActivityAlbumType.Travel => "travel",
        ActivityAlbumType.Wedding => "wedding",
        ActivityAlbumType.Conference => "conference",
        ActivityAlbumType.Concert => "concert",
        ActivityAlbumType.Gathering => "gathering",
        ActivityAlbumType.Other => "other",
        _ => "other"
    };

    public static ActivityAlbumDto ToDto(ActivityAlbum activity, ActivityAlbumMapContext? context = null) =>
        ToDto(activity, context?.ViewerUserId, context?.Creator);

    public static ActivityAlbumDto ToDto(
        ActivityAlbum activity,
        Guid? viewerUserId,
        ActivityAlbumCreatorSummary? creator = null)
    {
        var participantUserIds = BuildParticipantUserIds(activity);
        var viewerIsCreator = viewerUserId.HasValue && activity.CreatorUserId == viewerUserId.Value;
        var viewerIsParticipant = viewerUserId.HasValue
                                  && (viewerIsCreator || participantUserIds.Contains(viewerUserId.Value));

        return new ActivityAlbumDto
        {
            Id = activity.ExternalId,
            Title = activity.Title,
            Type = activity.Type,
            Status = activity.Status,
            StartDate = activity.StartDate,
            EndDate = activity.EndDate,
            Location = activity.Location,
            CreatorUserId = activity.CreatorUserId,
            ParticipantUserIds = participantUserIds,
            ViewerIsCreator = viewerIsCreator,
            ViewerIsParticipant = viewerIsParticipant,
            CreatorDisplayName = creator?.DisplayName,
            CreatorAvatarUrl = creator?.AvatarUrl,
            CoverPhotoId = activity.CoverPhotoId,
            PrivacyScope = activity.PrivacyScope,
            FamilyMemberIds = activity.FamilyMembers.Select(m => m.FamilyMemberId).ToList(),
            FriendIds = activity.Friends.Select(f => f.FriendReference).ToList(),
            CreatedAt = activity.CreatedAt,
            Agenda = activity.AgendaItems
                .OrderBy(a => a.SortOrder)
                .Select(a => new ActivityAgendaItemDto
                {
                    Id = string.IsNullOrWhiteSpace(a.ExternalId) ? a.Id.ToString() : a.ExternalId!,
                    Title = a.Title,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    Location = a.Location
                })
                .ToList()
        };
    }

    public static IReadOnlyList<Guid> BuildParticipantUserIds(ActivityAlbum activity)
    {
        var ids = new HashSet<Guid> { activity.CreatorUserId };

        foreach (var fm in activity.FamilyMembers)
        {
            var linked = fm.FamilyMember?.LinkedUserId;
            if (linked.HasValue && linked.Value != Guid.Empty)
            {
                ids.Add(linked.Value);
            }
        }

        foreach (var af in activity.Friends)
        {
            var friendUserId = af.Friend?.FriendUserId;
            if (friendUserId.HasValue && friendUserId.Value != Guid.Empty)
            {
                ids.Add(friendUserId.Value);
            }
        }

        return ids.OrderBy(id => id == activity.CreatorUserId ? 0 : 1).ThenBy(id => id).ToList();
    }

    public static string BuildSubtitle(ActivityAlbum activity, DateOnly date)
    {
        if (activity.Type != ActivityAlbumType.Travel)
        {
            return activity.Title;
        }

        var dayNumber = date.DayNumber - activity.StartDate.DayNumber + 1;
        if (dayNumber < 1)
        {
            dayNumber = 1;
        }

        var place = activity.Location;
        if (string.IsNullOrWhiteSpace(place))
        {
            place = activity.AgendaItems.OrderBy(a => a.SortOrder).FirstOrDefault()?.Location;
        }

        return string.IsNullOrWhiteSpace(place)
            ? $"第 {dayNumber} 天"
            : $"{place} · 第 {dayNumber} 天";
    }
}
