using Memoressa.Application.Abstractions;

namespace Memoressa.Api.Tests;

internal sealed class NoOpFrameDeviceWebSocketHub : IFrameDeviceWebSocketHub
{
    public List<string> SentMessages { get; } = [];

    public void AttachSession(Guid deviceId, Func<string, CancellationToken, Task> sendAsync)
    {
    }

    public void DetachSession(Guid deviceId)
    {
    }

    public Task SendJsonAsync(Guid deviceId, string json, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(json);
        return Task.CompletedTask;
    }
}
