using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoressa.Application.DTOs;
using Memoressa.Application.Interfaces;
using Memoressa.Domain.Enums;

namespace Memoressa.Api.WebSockets;

public static class DeviceWebSocketEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static void MapDeviceWebSocket(this WebApplication app)
    {
        app.Map("/ws/devices/{deviceId:guid}", HandleDeviceWebSocketAsync);
    }

    private static async Task HandleDeviceWebSocketAsync(
        HttpContext context,
        Guid deviceId,
        IInternalRealtimeService realtime,
        ILoggerFactory loggerFactory)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Expected a WebSocket request.");
            return;
        }

        var onlineResult = await realtime.UpdateDeviceStatusAsync(
            new UpdateDeviceStatusRequestDto
            {
                DeviceId = deviceId,
                Status = DisplayDeviceStatus.Online
            },
            context.RequestAborted);

        if (!onlineResult.Success)
        {
            context.Response.StatusCode = onlineResult.StatusCode;
            await context.Response.WriteAsync(onlineResult.Error ?? "Device not found.");
            return;
        }

        var logger = loggerFactory.CreateLogger("DeviceWebSocket");
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

        try
        {
            await RunSessionAsync(webSocket, deviceId, realtime, logger, context.RequestAborted);
        }
        finally
        {
            await realtime.UpdateDeviceStatusAsync(
                new UpdateDeviceStatusRequestDto
                {
                    DeviceId = deviceId,
                    Status = DisplayDeviceStatus.Offline
                },
                CancellationToken.None);
        }
    }

    private static async Task RunSessionAsync(
        WebSocket webSocket,
        Guid deviceId,
        IInternalRealtimeService realtime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var receiveTask = ReceiveLoopAsync(webSocket, deviceId, realtime, logger, linkedCts.Token);
        var pushTask = PushCommandsLoopAsync(webSocket, deviceId, realtime, linkedCts.Token);

        var completed = await Task.WhenAny(receiveTask, pushTask);
        linkedCts.Cancel();

        try
        {
            await completed;
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Session ended",
                    CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                logger.LogDebug(ex, "WebSocket close failed for device {DeviceId}", deviceId);
            }
        }
    }

    private static async Task PushCommandsLoopAsync(
        WebSocket webSocket,
        Guid deviceId,
        IInternalRealtimeService realtime,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (!cancellationToken.IsCancellationRequested
               && webSocket.State == WebSocketState.Open
               && await timer.WaitForNextTickAsync(cancellationToken))
        {
            var result = await realtime.GetPendingCommandsAsync(deviceId, cancellationToken);
            if (!result.Success || result.Data is null || result.Data.Count == 0)
            {
                continue;
            }

            var payload = JsonSerializer.Serialize(
                new { type = "commands", commands = result.Data },
                JsonOptions);

            await SendTextAsync(webSocket, payload, cancellationToken);
        }
    }

    private static async Task ReceiveLoopAsync(
        WebSocket webSocket,
        Guid deviceId,
        IInternalRealtimeService realtime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && webSocket.State == WebSocketState.Open)
        {
            var segment = new ArraySegment<byte>(buffer);
            var receiveResult = await webSocket.ReceiveAsync(segment, cancellationToken);

            if (receiveResult.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            await HandleClientMessageAsync(webSocket, json, realtime, logger, cancellationToken);
        }
    }

    private static async Task HandleClientMessageAsync(
        WebSocket webSocket,
        string json,
        IInternalRealtimeService realtime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            switch (type)
            {
                case "ping":
                    await SendTextAsync(webSocket, """{"type":"pong"}""", cancellationToken);
                    break;
                case "ack":
                    if (document.RootElement.TryGetProperty("commandId", out var idElement)
                        && Guid.TryParse(idElement.GetString(), out var commandId))
                    {
                        var success = !document.RootElement.TryGetProperty("success", out var successElement)
                                      || successElement.GetBoolean();
                        await realtime.AcknowledgeCommandAsync(
                            commandId,
                            new AckFrameCommandRequestDto { Success = success },
                            cancellationToken);
                    }

                    break;
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Invalid WebSocket JSON from device session");
        }
    }

    private static async Task SendTextAsync(WebSocket webSocket, string text, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken);
    }
}
