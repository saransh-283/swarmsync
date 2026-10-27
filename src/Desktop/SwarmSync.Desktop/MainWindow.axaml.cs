using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SwarmSync.Core;

namespace SwarmSync.Desktop;

public partial class MainWindow : Window
{
    private readonly DaemonDiscovery _discovery = new();
    private readonly DaemonConnection _connection = new();
    private readonly List<DeviceItemViewModel> _devices = new();

    public MainWindow()
    {
        InitializeComponent();

        _discovery.DeviceDiscovered += OnDeviceDiscovered;
        _discovery.DeviceLost += OnDeviceLost;
        _connection.DeviceListChanged += OnDeviceListChanged;

        RefreshButton.Click += (_, _) => _ = RefreshAsync();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        StatusText.Text = "Discovering daemons...";
        await _discovery.StartBrowsingAsync();

        // Connect to local daemon WebSocket (if daemon is running)
        try
        {
            await _connection.ConnectAsync(new Uri("ws://localhost:5136/ws"));
            await _connection.SubscribeToDeviceListAsync();
            StatusText.Text = "Connected";
        }
        catch
        {
            StatusText.Text = "Daemon not running";
        }

        var ip = SwarmSync.Core.DeviceDetector.GetLocalIpAddress();
        LocalIpText.Text = $"Local IP: {ip}";
    }

    private async Task RefreshAsync()
    {
        await _discovery.StartBrowsingAsync();
    }

    private void OnDeviceDiscovered(object? sender, DiscoveredDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_devices)
            {
                if (!_devices.Any(d => d.Id == device.Id))
                {
                    _devices.Add(new DeviceItemViewModel(device));
                }
                else
                {
                    var existing = _devices.First(d => d.Id == device.Id);
                    existing.Update(device);
                }
            }
            DeviceList.ItemsSource = null;
            DeviceList.ItemsSource = _devices;
        });
    }

    private void OnDeviceLost(object? sender, DiscoveredDevice device)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_devices)
            {
                _devices.RemoveAll(d => d.Id == device.Id);
            }
            DeviceList.ItemsSource = null;
            DeviceList.ItemsSource = _devices;
        });
    }

    private void OnDeviceListChanged(object? sender, List<DiscoveredDevice> devices)
    {
        Dispatcher.UIThread.Post(() =>
        {
            lock (_devices)
            {
                // Update local daemon entries from WebSocket broadcast
                foreach (var d in devices.Where(d => d.IsLocalDaemon))
                {
                    var existing = _devices.FirstOrDefault(x => x.Id == d.Id);
                    if (existing != null)
                        existing.Update(d);
                    else
                        _devices.Add(new DeviceItemViewModel(d));
                }
            }
            DeviceList.ItemsSource = null;
            DeviceList.ItemsSource = _devices;
        });
    }

}