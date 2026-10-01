using Memoressa.Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Memoressa.Infrastructure.Services;

public class DisplayDevicePairingSessionStore : IDisplayDevicePairingSessionStore
{
    private const string KeyPrefix = "display-device-pairing:";
    private readonly IMemoryCache _cache;

    public DisplayDevicePairingSessionStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Register(string qrCode, TimeSpan ttl)
    {
        _cache.Set(KeyPrefix + qrCode, true, ttl);
    }

    public bool IsActive(string qrCode) =>
        _cache.TryGetValue(KeyPrefix + qrCode, out _);

    public void Remove(string qrCode)
    {
        _cache.Remove(KeyPrefix + qrCode);
    }
}
