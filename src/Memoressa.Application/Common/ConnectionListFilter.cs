namespace Memoressa.Application.Common;

public enum ConnectionListFilterMode
{
    All,
    AcceptedOnly
}

public static class ConnectionListFilter
{
    public static bool TryParseFriendsStatus(
        string? status,
        out ConnectionListFilterMode mode,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            mode = ConnectionListFilterMode.All;
            error = null;
            return true;
        }

        if (string.Equals(status.Trim(), "accepted", StringComparison.OrdinalIgnoreCase))
        {
            mode = ConnectionListFilterMode.AcceptedOnly;
            error = null;
            return true;
        }

        mode = ConnectionListFilterMode.All;
        error = "Unsupported status; use accepted";
        return false;
    }

    public static bool TryParseFamilyConnectionStatus(
        string? connectionStatus,
        out ConnectionListFilterMode mode,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(connectionStatus))
        {
            mode = ConnectionListFilterMode.All;
            error = null;
            return true;
        }

        if (string.Equals(connectionStatus.Trim(), "accepted", StringComparison.OrdinalIgnoreCase))
        {
            mode = ConnectionListFilterMode.AcceptedOnly;
            error = null;
            return true;
        }

        mode = ConnectionListFilterMode.All;
        error = "Unsupported connectionStatus; use accepted";
        return false;
    }
}
