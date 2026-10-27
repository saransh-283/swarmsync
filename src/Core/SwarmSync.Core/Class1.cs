using System.Net;
using System.Net.NetworkInformation;

namespace SwarmSync.Core;

public class DeviceDetector
{
    public static List<string> FindDevices()
    {
        var devices = new List<string>();
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                var props = iface.GetIPProperties();
                var gateways = props.GatewayAddresses;
                if (gateways.Count > 0)
                    devices.Add(iface.Name);
            }
        }
        catch { }
        if (devices.Count == 0)
            devices.Add("localhost");
        return devices;
    }

    public static string GetLocalIpAddress()
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
}
