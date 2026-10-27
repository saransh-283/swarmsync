using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using SwarmSync.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://+:5136");
var app = builder.Build();

// ---- mDNS publisher (raw UDP, RFC 6762) ----
var mdnsPublisher = new MdnsPublisher(5353);
await mdnsPublisher.PublishAsync("_swarmsync._tcp.local", "SwarmSync Daemon", 5136);

// ---- In-memory device list (seeded from local network interfaces) ----
var devices = SwarmSync.Core.DeviceDetector.FindDevices();

// ---- WebSocket endpoint for real-time sync ----
var webSockets = new HashSet<WebSocket>();

app.MapGet("/devices", () => Results.Json(devices));

app.MapGet("/", () => "Hello World!");

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
    lock (webSockets) webSockets.Add(webSocket);

    try
    {
        var buffer = new byte[4096];
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }
    catch { }
    finally
    {
        lock (webSockets) webSockets.Remove(webSocket);
    }
});

// ---- Graceful shutdown ----
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
});

// ---- Run daemon ----
await app.RunAsync(cts.Token);

// ---- mDNS goodbye on shutdown ----
await mdnsPublisher.StopAsync();

// ---- mDNS Publisher (raw UDP multicast, RFC 6762) ----
public class MdnsPublisher : IDisposable
{
    private readonly UdpClient _socket;
    private readonly IPEndPoint _multicastEndpoint;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _announceTask;
    private bool _disposed;

    public MdnsPublisher(int port)
    {
        _socket = new UdpClient
        {
            EnableBroadcast = true,
            MulticastLoopback = false
        };
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(new IPEndPoint(IPAddress.Any, port));

        var multicastIp = IPAddress.Parse("224.0.0.251");
        _socket.JoinMulticastGroup(multicastIp);
        _multicastEndpoint = new IPEndPoint(multicastIp, 5353);

        _announceTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await AnnounceAsync();
                    await Task.Delay(5000, _cts.Token); // announce every 5 seconds
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    public async Task PublishAsync(string serviceType, string instanceName, int port)
    {
        await AnnounceAsync();
    }

    private async Task AnnounceAsync()
    {
        var packet = BuildMdnsResponse("_swarmsync._tcp.local", "SwarmSync Daemon", 5136);
        await _socket.SendAsync(packet, packet.Length, _multicastEndpoint);
    }

    private static byte[] BuildMdnsResponse(string serviceType, string instanceName, int port)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);

        // Header
        writer.Write((ushort)0); // Transaction ID
        writer.Write((ushort)0x8400); // Flags: response, authoritative
        writer.Write((ushort)1); // Questions: 0
        writer.Write((ushort)1); // Answer RRs: 1
        writer.Write((ushort)0); // Authority RRs: 0
        writer.Write((ushort)0); // Additional RRs: 0

        // Answer: PTR record _swarmsync._tcp.local -> instance.serviceType
        WriteDnsName(writer, serviceType);
        writer.Write((ushort)12); // PTR
        writer.Write((ushort)1); // IN
        writer.Write((uint)3600); // TTL
        var rdStart = (ushort)ms.Length;
        writer.Write((ushort)0); // RDLENGTH placeholder
        WriteDnsName(writer, $"{instanceName}.{serviceType}");
        var rdEnd = (ushort)ms.Length;
        ms.Position = rdStart;
        writer.Write((ushort)(rdEnd - rdStart - 2));
        ms.Position = rdEnd;

        // Answer: SRV record instance.serviceType -> host:port
        WriteDnsName(writer, $"{instanceName}.{serviceType}");
        writer.Write((ushort)33); // SRV
        writer.Write((ushort)1); // IN
        writer.Write((uint)3600); // TTL
        rdStart = (ushort)ms.Length;
        writer.Write((ushort)0); // RDLENGTH placeholder
        writer.Write((ushort)0); // priority
        writer.Write((ushort)0); // weight
        writer.Write((ushort)port); // port
        WriteDnsName(writer, $"{Dns.GetHostName()}.local");
        rdEnd = (ushort)ms.Length;
        ms.Position = rdStart;
        writer.Write((ushort)(rdEnd - rdStart - 2));
        ms.Position = rdEnd;

        // Answer: A record host.local -> IP
        var localIp = GetLocalIpAddress();
        WriteDnsName(writer, $"{Dns.GetHostName()}.local");
        writer.Write((ushort)1); // A
        writer.Write((ushort)1); // IN
        writer.Write((uint)3600); // TTL
        writer.Write((ushort)4); // RDLENGTH
        foreach (var part in localIp.Split('.'))
            writer.Write(byte.Parse(part));

        return ms.ToArray();
    }

    private static void WriteDnsName(BinaryWriter writer, string name)
    {
        foreach (var label in name.Split('.'))
        {
            writer.Write((byte)label.Length);
            writer.Write(Encoding.ASCII.GetBytes(label));
        }
        writer.Write((byte)0); // root label
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

    public async Task StopAsync()
    {
        _cts.Cancel();
        try { await _announceTask; } catch { }
        _socket.Close();
        _socket.Dispose();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cts.Cancel();
            _socket.Close();
            _socket.Dispose();
            _cts.Dispose();
        }
    }
}
