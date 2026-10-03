using System.Text;
using System.Text.Json;

namespace Memoressa.Application.Common;

/// <summary>Cursor for paginating viewer-visible activity album photos (link sort order).</summary>
public sealed record ActivityPhotoLinkCursor(int SortOrder, Guid PhotoId)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static ActivityPhotoLinkCursor? TryDecode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(PadBase64(cursor)));
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            if (payload is null || payload.PhotoId == Guid.Empty)
            {
                return null;
            }

            return new ActivityPhotoLinkCursor(payload.SortOrder, payload.PhotoId);
        }
        catch
        {
            return null;
        }
    }

    public string Encode()
    {
        var json = JsonSerializer.Serialize(new Payload { SortOrder = SortOrder, PhotoId = PhotoId }, JsonOptions);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
    }

    public bool IsBeforeInDescendingOrder(int sortOrder, Guid photoId) =>
        sortOrder > SortOrder
        || (sortOrder == SortOrder && photoId.CompareTo(PhotoId) > 0);

    private static string PadBase64(string cursor)
    {
        var padded = cursor.Replace('-', '+').Replace('_', '/');
        return padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
    }

    private sealed class Payload
    {
        public int SortOrder { get; set; }
        public Guid PhotoId { get; set; }
    }
}
