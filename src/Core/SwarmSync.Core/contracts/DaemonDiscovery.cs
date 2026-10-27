using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SwarmSync.Core;

/// <summary>
/// Discovers daemons on the local network via mDNS/DNS-SD (raw UDP, RFC 6762).
/// </summary>
public class DaemonDiscovery : IDaemonDiscovery
{
    private readonly UdpClient _socket;
    private readonly IPEndPoint _multicastEndpoint;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenTask;
    private readonly Dictionary<string, DiscoveredDevice> _cache = new();
    private readonly Timer _expiryTimer;
    private bool _disposed;

    public event EventHandler<DiscoveredDevice>? DeviceDiscovered;
    public event EventHandler<DiscoveredDevice>? DeviceLost;

    public DaemonDiscovery()
    {
        _socket = new UdpClient
        {
            EnableBroadcast = true,
            MulticastLoopback = false
        };
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));

        var multicastIp = IPAddress.Parse("224.0.0.251");
        _socket.JoinMulticastGroup(multicastIp);
        _multicastEndpoint = new IPEndPoint(multicastIp, 5353);

        _listenTask = Task.Run(ListenLoopAsync);

        // Expire devices that haven't been seen in 15 seconds
        _expiryTimer = new Timer(ExpireStaleDevices, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async Task StartBrowsingAsync(CancellationToken cancellationToken = default)
    {
        // Send a query for _swarmsync._tcp.local to prompt any daemons to respond
        var query = BuildMdnsQuery("_swarmsync._tcp.local");
        await _socket.SendAsync(query, query.Length, _multicastEndpoint);
    }

    public async Task StopBrowsingAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        try { await _listenTask; } catch { }
        _expiryTimer.Dispose();
        _socket.Close();
        _socket.Dispose();
    }

    private async Task ListenLoopAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(_cts.Token);
                var packet = result.Buffer;
                ParseMdnsPacket(packet, result.RemoteEndPoint.Address);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    private void ParseMdnsPacket(byte[] packet, IPAddress sourceIp)
    {
        if (packet.Length < 12) return;

        using var ms = new MemoryStream(packet);
        using var reader = new BinaryReader(ms);

        // Skip header (12 bytes)
        ms.Position = 12;

        // Parse questions (skip them)
        var qdcount = ReadBigEndianUInt16(packet, 4);
        for (int i = 0; i < qdcount; i++)
        {
            SkipDnsName(ms);
            ms.Position += 4; // type + class
        }

        // Parse answers
        var ancount = ReadBigEndianUInt16(packet, 6);
        for (int i = 0; i < ancount; i++)
        {
            var nameStart = ms.Position;
            SkipDnsName(ms);
            var type = ReadBigEndianUInt16(packet, (int)ms.Position);
            ms.Position += 2; // type
            ms.Position += 2; // class
            var ttl = ReadBigEndianUInt32(packet, (int)ms.Position);
            ms.Position += 4; // ttl
            var rdlength = ReadBigEndianUInt16(packet, (int)ms.Position);
            ms.Position += 2; // rdlength

            if (type == 33) // SRV record
            {
                var priority = ReadBigEndianUInt16(packet, (int)ms.Position);
                ms.Position += 2;
                var weight = ReadBigEndianUInt16(packet, (int)ms.Position);
                ms.Position += 2;
                var port = ReadBigEndianUInt16(packet, (int)ms.Position);
                ms.Position += 2;
                var target = ReadDnsName(ms, packet);

                if (target.Contains("_swarmsync._tcp.local"))
                {
                    var instanceName = target.Replace("._swarmsync._tcp.local", "");
                    var ip = sourceIp.ToString();

                    var device = new DiscoveredDevice
                    {
                        Id = ComputeDeviceId(instanceName, ip),
                        Name = instanceName,
                        HostName = target,
                        IpAddress = ip,
                        Port = port,
                        LastSeen = DateTime.UtcNow,
                        IsLocalDaemon = ip == GetLocalIpAddress()
                    };

                    lock (_cache)
                    {
                        _cache[device.Id] = device;
                    }

                    DeviceDiscovered?.Invoke(this, device);
                }
            }
            else
            {
                ms.Position += rdlength;
            }
        }
    }

    private void ExpireStaleDevices(object? state)
    {
        List<DiscoveredDevice> toRemove;
        lock (_cache)
        {
            toRemove = _cache
                .Where(kvp => (DateTime.UtcNow - kvp.Value.LastSeen).TotalSeconds > 15)
                .Select(kvp => kvp.Value)
                .ToList();

            foreach (var device in toRemove)
                _cache.Remove(device.Id);
        }

        foreach (var device in toRemove)
            DeviceLost?.Invoke(this, device);
    }

    private static ushort ReadBigEndianUInt16(byte[] packet, int offset)
        => (ushort)((packet[offset] << 8) | packet[offset + 1]);

    private static uint ReadBigEndianUInt32(byte[] packet, int offset)
        => ((uint)packet[offset] << 24) | ((uint)packet[offset + 1] << 16) |
           ((uint)packet[offset + 2] << 8) | packet[offset + 3];

    private static void SkipDnsName(MemoryStream ms)
    {
        while (true)
        {
            var length = ms.ReadByte();
            if (length == -1) return;
            if ((length & 0xC0) == 0xC0) // pointer
            {
                ms.Position += 1;
                return;
            }
            ms.Position += length;
        }
    }

    private static string ReadDnsName(MemoryStream ms, byte[] packet)
    {
        var labels = new List<string>();
        var visited = new HashSet<int>();
        int originalPosition = (int)ms.Position;

        while (true)
        {
            if (visited.Contains((int)ms.Position)) break;
            visited.Add((int)ms.Position);

            var length = ms.ReadByte();
            if (length == -1) break;
            if (length == 0) break;

            if ((length & 0xC0) == 0xC0) // pointer
            {
                var pointer = ((length & 0x3F) << 8) | ms.ReadByte();
                var save = ms.Position;
                ms.Position = pointer;
                var sub = ReadDnsName(ms, packet);
                ms.Position = save;
                labels.Add(sub);
                break;
            }

            var labelBytes = new byte[length];
            ms.Read(labelBytes, 0, length);
            labels.Add(Encoding.ASCII.GetString(labelBytes));
        }

        return string.Join(".", labels);
    }

    private static string ComputeDeviceId(string instanceName, string ip)
    {
        var input = $"{instanceName}:{ip}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    private static byte[] BuildMdnsQuery(string serviceType)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        writer.Write((ushort)0); // Transaction ID
        writer.Write((ushort)0x0100); // Flags: standard query
        writer.Write((ushort)1); // Questions: 1
        writer.Write((ushort)0); // Answer RRs: 0
        writer.Write((ushort)0); // Authority RRs: 0
        writer.Write((ushort)0); // Additional RRs: 0

        WriteDnsName(writer, serviceType);
        writer.Write((ushort)12); // PTR
        writer.Write((ushort)1); // IN

        return ms.ToArray();
    }

    private static void WriteDnsName(BinaryWriter writer, string name)
    {
        foreach (var label in name.Split('.'))
        {
            writer.Write((byte)label.Length);
            writer.Write(Encoding.ASCII.GetBytes(label));
        }
        writer.Write((byte)0);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _expiryTimer.Dispose();
            _socket.Close();
            _socket.Dispose();
            _cts.Dispose();
        }
    }
}
