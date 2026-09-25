namespace Memoressa.Infrastructure.Options;

public class WebSocketProxyOptions
{
    public const string SectionName = "WebSocketProxy";

    /// <summary>
    /// When true, <c>/ws/devices/{deviceId}</c> on Kestrel is tunneled to the backend (plain ws/http base URL).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Backend origin without path, e.g. <c>http://192.168.1.131:8080</c> (upgraded to ws/wss when connecting).
    /// </summary>
    public string BackendBaseUrl { get; set; } = "http://127.0.0.1:8080";
}
