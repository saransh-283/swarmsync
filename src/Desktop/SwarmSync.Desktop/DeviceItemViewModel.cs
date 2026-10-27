using Avalonia.Media;
using SwarmSync.Core;

namespace SwarmSync.Desktop;

public class DeviceItemViewModel
{
    public string Id { get; }
    public string Name { get; private set; }
    public string IpAddress { get; private set; }
    public int Port { get; private set; }
    public bool IsLocalDaemon { get; }
    public string StatusText => IsLocalDaemon ? "Local" : "Network";
    public IBrush StatusColor => IsLocalDaemon ? Brushes.Green : Brushes.Blue;

    public DeviceItemViewModel(DiscoveredDevice device)
    {
        Id = device.Id;
        Name = device.Name;
        IpAddress = device.IpAddress;
        Port = device.Port;
        IsLocalDaemon = device.IsLocalDaemon;
    }

    public void Update(DiscoveredDevice device)
    {
        Name = device.Name;
        IpAddress = device.IpAddress;
        Port = device.Port;
    }
}
