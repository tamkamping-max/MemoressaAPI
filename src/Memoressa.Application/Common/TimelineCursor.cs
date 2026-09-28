using System.Text;
using System.Text.Json;

namespace Memoressa.Application.Common;

public sealed record TimelineCursor(DateTime SortAtUtc, Guid PhotoId)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static TimelineCursor? TryDecode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(cursor)));
            var payload = JsonSerializer.Deserialize<TimelineCursorPayload>(json, JsonOptions);
            if (payload is null || payload.PhotoId == Guid.Empty)
            {
                return null;
            }

            return new TimelineCursor(payload.SortAtUtc, payload.PhotoId);
        }
        catch
        {
            return null;
        }
    }

    public string Encode()
    {
        var json = JsonSerializer.Serialize(
            new TimelineCursorPayload { SortAtUtc = SortAtUtc, PhotoId = PhotoId },
            JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
    }

    private static string PadBase64(string cursor)
    {
        var padded = cursor.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    }

    private sealed class TimelineCursorPayload
    {
        public DateTime SortAtUtc { get; set; }
        public Guid PhotoId { get; set; }
    }
}

public static class PhotoTimelinePagination
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public static int NormalizeLimit(int? limit)
    {
        if (limit is null or < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit.Value, MaxLimit);
    }
}
