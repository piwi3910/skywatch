using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SkyWatch.Web.WebSocket;

/// <summary>
/// Handles WebSocket upgrade requests on <c>/ws/live</c> and manages connection lifecycle.
/// </summary>
public sealed partial class SkyWatchWebSocketHandler
{
    private readonly WebSocketConnectionManager connectionManager;
    private readonly ILogger<SkyWatchWebSocketHandler> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SkyWatchWebSocketHandler"/> class.
    /// </summary>
    /// <param name="connectionManager">The connection manager for tracking clients.</param>
    /// <param name="logger">The logger instance.</param>
    public SkyWatchWebSocketHandler(
        WebSocketConnectionManager connectionManager,
        ILogger<SkyWatchWebSocketHandler> logger)
    {
        this.connectionManager = connectionManager;
        this.logger = logger;
    }

    /// <summary>
    /// Handles an incoming HTTP context by upgrading to WebSocket and maintaining the connection.
    /// </summary>
    /// <param name="context">The HTTP context containing the WebSocket upgrade request.</param>
    /// <returns>A task that completes when the WebSocket connection is closed.</returns>
    public async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = connectionManager.AddConnection(socket);
        LogClientConnected(connectionId, connectionManager.ConnectionCount);

        try
        {
            await ReceiveUntilCloseAsync(socket, context.RequestAborted);
        }
        finally
        {
            connectionManager.RemoveConnection(connectionId);
            LogClientDisconnected(connectionId, connectionManager.ConnectionCount);
        }
    }

    private static async Task ReceiveUntilCloseAsync(System.Net.WebSockets.WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];
        while (socket.State == WebSocketState.Open)
        {
            try
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                    break;
                }
            }
            catch (WebSocketException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "WebSocket client connected: {ConnectionId} (total: {ConnectionCount})")]
    private partial void LogClientConnected(string connectionId, int connectionCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "WebSocket client disconnected: {ConnectionId} (total: {ConnectionCount})")]
    private partial void LogClientDisconnected(string connectionId, int connectionCount);
}
