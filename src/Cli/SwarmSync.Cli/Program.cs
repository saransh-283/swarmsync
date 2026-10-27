using Spectre.Console;
using SwarmSync.Core;

var oneOff = args.Contains("--list");

if (args.Contains("--help"))
{
    AnsiConsole.Write(new FigletText("SwarmSync").Color(new Color(0, 255, 255)));
    AnsiConsole.WriteLine();
    AnsiConsole.WriteLine("Usage: SwarmSync.Cli [--list] [--help]");
    AnsiConsole.WriteLine("  --list   One-off device list");
    AnsiConsole.WriteLine("  --help   Show this help");
    return;
}

var ip = DeviceDetector.GetLocalIpAddress();
var daemonUrl = "http://localhost:5136";
var daemonAlive = false;
string? devicesJson = null;

try
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    devicesJson = await client.GetStringAsync($"{daemonUrl}/devices");
    daemonAlive = true;
}
catch { }

var devices = daemonAlive && devicesJson != null
    ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(devicesJson) ?? new List<string>()
    : new List<string>();

if (oneOff)
{
    AnsiConsole.Write(new FigletText("SwarmSync").Color(new Color(0, 255, 255)));
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Table()
        .AddColumn("Local IP")
        .AddColumn("Daemon")
        .AddColumn("Devices")
        .AddRow(ip, daemonAlive ? "Live" : "Down", string.Join(", ", devices)));
    return;
}

while (true)
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new FigletText("SwarmSync").Color(new Color(0, 255, 255)));
    AnsiConsole.WriteLine();
    var panel = new Panel(
        new Table()
            .AddColumn("Property")
            .AddColumn("Value")
            .AddRow("Local IP", ip)
            .AddRow("Daemon", daemonAlive ? "[green]Live[/]" : "[red]Down[/]")
            .AddRow("Devices", string.Join(", ", devices) ?? "(none)")
            .AddRow("Daemon URL", daemonUrl)
    )
    .Header("SwarmSync Terminal UI")
    .Border(BoxBorder.Rounded);

    AnsiConsole.Write(panel);
    AnsiConsole.WriteLine();

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Select an option:")
            .AddChoices(new[] { "Refresh", "List devices", "Exit" })
    );

    if (choice == "Exit") break;

    if (choice == "Refresh" || choice == "List devices")
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            devicesJson = await client.GetStringAsync($"{daemonUrl}/devices");
            daemonAlive = true;
            devices = System.Text.Json.JsonSerializer.Deserialize<List<string>>(devicesJson) ?? new List<string>();
        }
        catch
        {
            daemonAlive = false;
            devices = new List<string>();
        }
    }
}
