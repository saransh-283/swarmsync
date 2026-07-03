var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/devices", () => SwarmSync.Core.DeviceDetector.FindDevices());

app.MapGet("/", () => "Hello World!");

app.Run();
