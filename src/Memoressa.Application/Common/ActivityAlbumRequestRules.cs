using Memoressa.Application.DTOs;
using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class ActivityAlbumRequestRules
{
    public static (bool Ok, ActivityAlbumType Type, string? Error) ResolveType(UpsertActivityAlbumRequestDto request)
    {
        if (request.Type.HasValue && request.ActivityType.HasValue
            && request.Type.Value != request.ActivityType.Value)
        {
            return (false, default, "type and activityType must match when both are provided");
        }

        var resolved = request.Type ?? request.ActivityType;
        if (!resolved.HasValue)
        {
            return (false, default, "type is required");
        }

        if (!Enum.IsDefined(typeof(ActivityAlbumType), resolved.Value))
        {
            return (false, default, "Invalid activity type");
        }

        return (true, resolved.Value, null);
    }

    public static string? ValidateUpsert(UpsertActivityAlbumRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return "title is required";
        }

        var (_, _, typeError) = ResolveType(request);
        if (typeError is not null)
        {
            return typeError;
        }

        if (!Enum.IsDefined(typeof(ActivityAlbumStatus), request.Status))
        {
            return "Invalid activity status";
        }

        foreach (var item in request.Agenda ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.Title))
            {
                return "Each agenda item requires a non-empty title";
            }
        }

        return null;
    }
}
