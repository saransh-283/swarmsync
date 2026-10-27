using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace SwarmSync.Core;

/// <summary>
/// Maintains a WebSocket connection to a daemon for real-time device-list sync.
/// </summary>
public class DaemonConnection : IDaemonConnection
{
    private ClientWebSocket? _webSocket;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private bool _disposed;

    public event EventHandler<List<DiscoveredDevice>>? DeviceListChanged;
    public event EventHandler? Disconnected;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        await DisconnectAsync();

        _webSocket = new ClientWebSocket();
        await _webSocket.ConnectAsync(endpoint, cancellationToken);

        _receiveTask = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var devices = JsonSerializer.Deserialize<List<DiscoveredDevice>>(json);
                    if (devices != null)
                        DeviceListChanged?.Invoke(this, devices);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }, cancellationToken);
    }

    public async Task SubscribeToDeviceListAsync(CancellationToken cancellationToken = default)
    {
        // The daemon broadcasts device list automatically on connection.
        // This method is a placeholder for future subscription messages.
        await Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        _cts.Cancel();

        if (_receiveTask != null)
        {
            try { await _receiveTask; } catch { }
        }

        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            }
            catch { }
            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _webSocket?.Dispose();
            _cts.Dispose();
        }
    }
}
