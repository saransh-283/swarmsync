using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SwarmSync.Core;

namespace SwarmSync.Android;

[Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
public class MainActivity : Activity
{
    private TextView? _statusText;
    private readonly List<DiscoveredDevice> _discoveredDevices = new();
    private UdpClient? _mdnsSocket;
    private CancellationTokenSource? _mdnsCts;
    private Task? _mdnsListenTask;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        _statusText = FindViewById<TextView>(Resource.Id.statusText)!;

        _statusText.Text = "SwarmSync Android — Ready";

        // Initialize mDNS discovery via raw UDP multicast
        _ = StartMdnsDiscoveryAsync();
    }

    private async Task StartMdnsDiscoveryAsync()
    {
        try
        {
            _mdnsSocket = new UdpClient
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };
            _mdnsSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _mdnsSocket.Client.Bind(new IPEndPoint(IPAddress.Any, 5353));

            var multicastIp = IPAddress.Parse("224.0.0.251");
            _mdnsSocket.JoinMulticastGroup(multicastIp);
            var multicastEndpoint = new IPEndPoint(multicastIp, 5353);

            _mdnsCts = new CancellationTokenSource();

            // Send mDNS query for _swarmsync._tcp.local
            var query = BuildMdnsQuery("_swarmsync._tcp.local");
            await _mdnsSocket.SendAsync(query, query.Length, multicastEndpoint);

            _mdnsListenTask = Task.Run(async () =>
            {
                try
                {
                    while (!_mdnsCts.Token.IsCancellationRequested)
                    {
                        var result = await _mdnsSocket!.ReceiveAsync(_mdnsCts.Token);
                        ParseMdnsPacket(result.Buffer, result.RemoteEndPoint.Address);
                    }
                }
                catch (System.OperationCanceledException) { }
                catch (ObjectDisposedException) { }
            });
        }
        catch (Exception ex)
        {
            RunOnUiThread(() => _statusText!.Text = $"mDNS error: {ex.Message}");
        }
    }

    private void ParseMdnsPacket(byte[] packet, IPAddress sourceIp)
    {
        if (packet.Length < 12) return;

        using var ms = new MemoryStream(packet);
        using var reader = new BinaryReader(ms);

        ms.Position = 12; // Skip header

        var qdcount = ReadBigEndianUInt16(packet, 4);
        for (int i = 0; i < qdcount; i++)
        {
            SkipDnsName(ms);
            ms.Position += 4;
        }

        var ancount = ReadBigEndianUInt16(packet, 6);
        for (int i = 0; i < ancount; i++)
        {
            SkipDnsName(ms);
            var type = ReadBigEndianUInt16(packet, (int)ms.Position);
            ms.Position += 2;
            ms.Position += 2;
            ms.Position += 4;
            var rdlength = ReadBigEndianUInt16(packet, (int)ms.Position);
            ms.Position += 2;

            if (type == 33) // SRV record
            {
                ms.Position += 4; // priority + weight
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

                    lock (_discoveredDevices)
                    {
                        if (!_discoveredDevices.Any(d => d.Id == device.Id))
                            _discoveredDevices.Add(device);
                    }

                    RunOnUiThread(() =>
                    {
                        _statusText!.Text = $"Found: {device.Name} at {device.IpAddress}:{device.Port}";
                    });
                }
            }
            else
            {
                ms.Position += rdlength;
            }
        }
    }

    private static ushort ReadBigEndianUInt16(byte[] packet, int offset)
        => (ushort)((packet[offset] << 8) | packet[offset + 1]);

    private static void SkipDnsName(MemoryStream ms)
    {
        while (true)
        {
            var length = ms.ReadByte();
            if (length == -1) return;
            if ((length & 0xC0) == 0xC0)
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
        while (true)
        {
            var length = ms.ReadByte();
            if (length == -1 || length == 0) break;
            if ((length & 0xC0) == 0xC0)
            {
                var pointer = ((length & 0x3F) << 8) | ms.ReadByte();
                var save = ms.Position;
                ms.Position = pointer;
                labels.Add(ReadDnsName(ms, packet));
                ms.Position = save;
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

        writer.Write((ushort)0);
        writer.Write((ushort)0x0100);
        writer.Write((ushort)1);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);

        foreach (var label in serviceType.Split('.'))
        {
            writer.Write((byte)label.Length);
            writer.Write(Encoding.ASCII.GetBytes(label));
        }
        writer.Write((byte)0);
        writer.Write((ushort)12); // PTR
        writer.Write((ushort)1); // IN

        return ms.ToArray();
    }

    protected override void OnDestroy()
    {
        _mdnsCts?.Cancel();
        _mdnsSocket?.Close();
        _mdnsSocket?.Dispose();
        base.OnDestroy();
    }
}
