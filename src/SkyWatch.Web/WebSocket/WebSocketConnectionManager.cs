using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace SkyWatch.Web.WebSocket;

/// <summary>
/// Manages active WebSocket connections and provides broadcast capabilities.
/// </summary>
public sealed class WebSocketConnectionManager
{
    private readonly ConcurrentDictionary<string, System.Net.WebSockets.WebSocket> connections = new();

    /// <summary>
    /// Gets the number of currently active connections.
    /// </summary>
    public int ConnectionCount => connections.Count;

    /// <summary>
    /// Adds a WebSocket connection and returns a unique connection identifier.
    /// </summary>
    /// <param name="socket">The WebSocket to track.</param>
    /// <returns>The unique connection identifier.</returns>
    public string AddConnection(System.Net.WebSockets.WebSocket socket)
    {
        var id = Guid.NewGuid().ToString("N");
        connections.TryAdd(id, socket);
        return id;
    }

    /// <summary>
    /// Removes a tracked WebSocket connection by its identifier.
    /// </summary>
    /// <param name="id">The connection identifier to remove.</param>
    public void RemoveConnection(string id)
    {
        connections.TryRemove(id, out _);
    }

    /// <summary>
    /// Broadcasts a text message to all connected WebSocket clients, removing any dead connections.
    /// </summary>
    /// <param name="message">The text message to broadcast.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task BroadcastTextAsync(string message, CancellationToken cancellationToken)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        foreach (var (id, socket) in connections)
        {
            if (socket.State != WebSocketState.Open)
            {
                connections.TryRemove(id, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(segment, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            }
            catch (WebSocketException)
            {
                connections.TryRemove(id, out _);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Broadcasts binary data to all connected WebSocket clients, removing any dead connections.
    /// </summary>
    /// <param name="data">The binary data to broadcast.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task BroadcastBinaryAsync(byte[] data, CancellationToken cancellationToken)
    {
        var segment = new ArraySegment<byte>(data);

        foreach (var (id, socket) in connections)
        {
            if (socket.State != WebSocketState.Open)
            {
                connections.TryRemove(id, out _);
                continue;
            }

            try
            {
                await socket.SendAsync(segment, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
            }
            catch (WebSocketException)
            {
                connections.TryRemove(id, out _);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
