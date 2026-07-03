using System.Net;

var oneOff = args.Contains("--list");
if (args.Contains("--help"))
{
    Console.WriteLine("Usage: SwarmSync.Cli [--list] [--help]\n--list  One-off device list\n--help  Show this help");
    return;
}

while (true)
{
    if (oneOff)
    {
        var client = new HttpClient();
        var res = await client.GetStringAsync("http://localhost:5136/devices");
        Console.WriteLine("Devices: " + res);
        break;
    }
    else
    {
        Console.WriteLine("1. List devices\nq. Exit");
        var line = Console.ReadLine();
        if (line == "q") break;
        if (line == "1")
        {
            var client = new HttpClient();
            var res = await client.GetStringAsync("http://localhost:5136/devices");
            Console.WriteLine("Devices: " + res);
        }
    }
}
