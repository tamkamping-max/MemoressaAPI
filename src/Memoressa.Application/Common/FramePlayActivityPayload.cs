using System.Text.Json;

namespace Memoressa.Application.Common;

public static class FramePlayActivityPayload
{
    public static bool TryParse(
        string payloadJson,
        out string? activityId,
        out bool playNow,
        out string? title)
    {
        activityId = null;
        playNow = false;
        title = null;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty("activityId", out var activityIdElement)
                || activityIdElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var parsedId = activityIdElement.GetString()?.Trim();
            if (string.IsNullOrEmpty(parsedId))
            {
                return false;
            }

            activityId = parsedId;

            if (root.TryGetProperty("playNow", out var playNowElement)
                && (playNowElement.ValueKind == JsonValueKind.True || playNowElement.ValueKind == JsonValueKind.False))
            {
                playNow = playNowElement.GetBoolean();
            }

            if (root.TryGetProperty("packageTitle", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                title = titleElement.GetString();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
