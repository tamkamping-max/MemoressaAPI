namespace Memoressa.Application.Abstractions;

public interface IDisplayDevicePairingSessionStore
{
    void Register(string qrCode, TimeSpan ttl);

    bool IsActive(string qrCode);

    void Remove(string qrCode);
}
