using System.Collections.Concurrent;
using Memoressa.Application.Abstractions;

namespace Memoressa.Api.WebSockets;

public sealed class FrameDeviceWebSocketHub : IFrameDeviceWebSocketHub
{
    private readonly ConcurrentDictionary<Guid, Func<string, CancellationToken, Task>> _sessions = new();

    public void AttachSession(Guid deviceId, Func<string, CancellationToken, Task> sendAsync) =>
        _sessions[deviceId] = sendAsync;

    public void DetachSession(Guid deviceId) =>
        _sessions.TryRemove(deviceId, out _);

    public async Task SendJsonAsync(Guid deviceId, string json, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(deviceId, out var sendAsync))
        {
            await sendAsync(json, cancellationToken);
        }
    }
}
