using System.Net.WebSockets;

namespace Memoressa.Api.WebSockets;

internal static class WebSocketReverseProxy
{
    public static async Task ProxyAsync(
        HttpContext context,
        Uri backendUri,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var backendSocket = new ClientWebSocket();
        CopyRequestHeaders(context, backendSocket);

        try
        {
            await backendSocket.ConnectAsync(backendUri, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WebSocket backend unavailable at {BackendUri}", backendUri);
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsync("WebSocket backend unavailable.");
            return;
        }

        using var clientSocket = await context.WebSockets.AcceptWebSocketAsync();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var clientToBackend = PumpAsync(clientSocket, backendSocket, linkedCts.Token);
        var backendToClient = PumpAsync(backendSocket, clientSocket, linkedCts.Token);

        var completed = await Task.WhenAny(clientToBackend, backendToClient);
        linkedCts.Cancel();

        try
        {
            await completed;
        }
        catch (OperationCanceledException)
        {
            // expected when the peer closes
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "WebSocket proxy session ended for {Path}", context.Request.Path);
        }
    }

    public static Uri BuildBackendUri(string backendBaseUrl, PathString path, QueryString query)
    {
        if (!Uri.TryCreate(backendBaseUrl.TrimEnd('/'), UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException($"Invalid WebSocketProxy:BackendBaseUrl '{backendBaseUrl}'.");
        }

        var scheme = baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        var builder = new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Path = path.Value ?? string.Empty,
            Query = query.HasValue ? query.Value.TrimStart('?') : string.Empty
        };

        return builder.Uri;
    }

    private static void CopyRequestHeaders(HttpContext context, ClientWebSocket backendSocket)
    {
        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Connection", StringComparison.OrdinalIgnoreCase)
                || header.Key.StartsWith("Sec-WebSocket-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                || header.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase))
            {
                backendSocket.Options.SetRequestHeader(header.Key, header.Value.ToString());
            }
        }
    }

    private static async Task PumpAsync(
        WebSocket source,
        WebSocket destination,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested
               && source.State == WebSocketState.Open
               && destination.State == WebSocketState.Open)
        {
            var receiveResult = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (receiveResult.MessageType == WebSocketMessageType.Close)
            {
                if (destination.State == WebSocketState.Open)
                {
                    await destination.CloseAsync(
                        receiveResult.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        receiveResult.CloseStatusDescription,
                        cancellationToken);
                }

                break;
            }

            await destination.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                cancellationToken);
        }
    }
}
