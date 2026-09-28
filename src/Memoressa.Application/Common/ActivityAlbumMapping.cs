using Memoressa.Application.DTOs;
using Memoressa.Domain.Entities;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class ActivityAlbumMapping
{
    public static ActivityAlbumDto ToDto(ActivityAlbum activity) => new()
    {
        Id = activity.ExternalId,
        Title = activity.Title,
        Type = activity.Type,
        Status = activity.Status,
        StartDate = activity.StartDate,
        EndDate = activity.EndDate,
        Location = activity.Location,
        CreatorUserId = activity.CreatorUserId,
        CoverPhotoId = activity.CoverPhotoId,
        FamilyMemberIds = activity.FamilyMembers.Select(m => m.FamilyMemberId).ToList(),
        FriendIds = activity.Friends.Select(f => f.FriendReference).ToList(),
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
