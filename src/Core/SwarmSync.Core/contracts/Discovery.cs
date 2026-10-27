namespace SwarmSync.Core;

/// <summary>
/// Represents a daemon discovered on the network via mDNS.
/// </summary>
public class DiscoveredDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsLocalDaemon { get; set; }
}

/// <summary>
/// Discovers daemons on the local network via mDNS/DNS-SD.
/// </summary>
public interface IDaemonDiscovery
{
    event EventHandler<DiscoveredDevice>? DeviceDiscovered;
    event EventHandler<DiscoveredDevice>? DeviceLost;
    Task StartBrowsingAsync(CancellationToken cancellationToken = default);
    Task StopBrowsingAsync();
}

/// <summary>
/// Maintains a WebSocket connection to a daemon for real-time device-list sync.
/// </summary>
public interface IDaemonConnection
{
    event EventHandler<List<DiscoveredDevice>>? DeviceListChanged;
    event EventHandler? Disconnected;
    Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default);
    Task SubscribeToDeviceListAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
