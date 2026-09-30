using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.DTOs;

namespace Memoressa.Application.Common;

public static class FrameDeviceWebSocketMessages
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string CommandsPayload(IReadOnlyList<FrameCommandDto> commands) =>
        JsonSerializer.Serialize(new { type = "commands", commands }, JsonOptions);

    public static string RemoveFromQueuePayload(Guid commandId, Guid memoryId) =>
        JsonSerializer.Serialize(
            new { type = "removeFromQueue", commandId, memoryId },
            JsonOptions);
}
