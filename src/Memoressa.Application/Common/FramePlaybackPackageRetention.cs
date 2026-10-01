using System.Text.Json;

namespace Memoressa.Application.Common;

public static class FramePlaybackPackageRetention
{
    public static bool ShouldRemoveOnDeviceUnbind(string packageJson)
    {
        if (string.IsNullOrWhiteSpace(packageJson))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(packageJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return true;
            }

            if (root.TryGetProperty("isFriendShare", out var friendShareFlag)
                && friendShareFlag.ValueKind == JsonValueKind.True)
            {
                return false;
            }

            if (root.TryGetProperty("source", out var source)
                && source.ValueKind == JsonValueKind.String
                && string.Equals(source.GetString(), "friend", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return true;
        }
    }
}
