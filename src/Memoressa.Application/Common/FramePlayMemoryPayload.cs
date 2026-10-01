using System.Text.Json;

namespace Memoressa.Application.Common;

public static class FramePlayMemoryPayload
{
    public static bool TryParse(
        string payloadJson,
        out Guid? memoryId,
        out bool playNow,
        out string? title)
    {
        memoryId = null;
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

            if (!root.TryGetProperty("memoryId", out var memoryIdElement))
            {
                return false;
            }

            if (memoryIdElement.ValueKind == JsonValueKind.String)
            {
                if (!Guid.TryParse(memoryIdElement.GetString(), out var parsedMemoryId))
                {
                    return false;
                }

                memoryId = parsedMemoryId;
            }
            else if (!memoryIdElement.TryGetGuid(out var guidMemoryId))
            {
                return false;
            }
            else
            {
                memoryId = guidMemoryId;
            }

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

    public static bool PackageJsonContainsMemoryId(string packageJson, Guid memoryId)
    {
        if (string.IsNullOrWhiteSpace(packageJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(packageJson);
            if (!doc.RootElement.TryGetProperty("memoryId", out var el))
            {
                return false;
            }

            if (el.ValueKind == JsonValueKind.String && Guid.TryParse(el.GetString(), out var id))
            {
                return id == memoryId;
            }

            return el.TryGetGuid(out var guid) && guid == memoryId;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
