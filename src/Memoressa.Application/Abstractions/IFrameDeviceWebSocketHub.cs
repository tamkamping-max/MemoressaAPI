namespace Memoressa.Application.Abstractions;

public interface IFrameDeviceWebSocketHub
{
    void AttachSession(Guid deviceId, Func<string, CancellationToken, Task> sendAsync);

    void DetachSession(Guid deviceId);

    Task SendJsonAsync(Guid deviceId, string json, CancellationToken cancellationToken = default);
}
