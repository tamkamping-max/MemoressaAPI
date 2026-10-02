using Memoressa.Domain.Enums;

namespace Memoressa.Application.Common;

public static class ActivityAlbumListFilters
{
    public static HashSet<ActivityAlbumStatus> ParseStatusInclude(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return [];
        }

        var set = new HashSet<ActivityAlbumStatus>();
        foreach (var part in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseStatus(part, out var parsed))
            {
                set.Add(parsed);
            }
        }

        return set;
    }

    public static HashSet<ActivityAlbumStatus> ParseStatusExclude(string? excludeStatus)
    {
        if (string.IsNullOrWhiteSpace(excludeStatus))
        {
            return [];
        }

        var set = new HashSet<ActivityAlbumStatus>();
        foreach (var part in excludeStatus.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryParseStatus(part, out var parsed))
            {
                set.Add(parsed);
            }
        }

        return set;
    }

    public static bool Matches(
        ActivityAlbumStatus status,
        HashSet<ActivityAlbumStatus> include,
        HashSet<ActivityAlbumStatus> exclude)
    {
        if (include.Count > 0 && !include.Contains(status))
        {
            return false;
        }

        if (exclude.Count > 0 && exclude.Contains(status))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseStatus(string raw, out ActivityAlbumStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim();
        if (Enum.TryParse<ActivityAlbumStatus>(normalized, ignoreCase: true, out status))
        {
            return true;
        }

        return normalized.ToLowerInvariant() switch
        {
            "inprogress" => Assign(ActivityAlbumStatus.InProgress, out status),
            "completed" => Assign(ActivityAlbumStatus.Completed, out status),
            "cancelled" or "canceled" => Assign(ActivityAlbumStatus.Cancelled, out status),
            _ => false
        };
    }

    private static bool Assign(ActivityAlbumStatus value, out ActivityAlbumStatus status)
    {
        status = value;
        return true;
    }
}

public static class ActivityListPagination
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static int NormalizeLimit(int? limit)
    {
        if (limit is null or < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }
}
