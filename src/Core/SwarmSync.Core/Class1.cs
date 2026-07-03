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
//                if (iface.OperationalStatus == OperationalStatus.Up)
  //              {
                    var props = iface.GetIPProperties();
                    var gateways = props.GatewayAddresses;
                    if (gateways.Count > 0)
                        devices.Add(iface.Name);
    //            }
            }
        }
        catch { }
        if (devices.Count == 0)
            devices.Add("localhost");
        return devices;
    }
}
