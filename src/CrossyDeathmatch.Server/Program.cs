using System.Net.NetworkInformation;
using System.Net.Sockets;
using CrossyDeathmatch.Server;

// Options: --port 5000 --min-players 2 --round-seconds 180 --deep-copy true --prototype-demo true
var builder = WebApplication.CreateBuilder(args);
var port = builder.Configuration.GetValue("port", 5000);

if (builder.Configuration.GetValue("prototype-demo", false))
{
    PrototypeDemo.Run();
    return;
}

ObstacleFactory.Registry.UseDeepCopy = builder.Configuration.GetValue("deep-copy", true);
ServerLog.Instance.Info($"Obstacle copies: {(ObstacleFactory.Registry.UseDeepCopy ? "deep" : "shallow")}");

builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddSignalR();
builder.Services.AddSingleton<Game>();

var app = builder.Build();
app.MapGet("/", () => "Crossy Deathmatch server is running. Point the client at this address.");
app.MapHub<GameHub>("/game");

// Create the game now so its timer is running before anyone connects.
app.Services.GetRequiredService<Game>();

ServerLog.Instance.Info($"Crossy Deathmatch server on port {port}");
ServerLog.Instance.Info($"  This machine:  http://localhost:{port}");
foreach (var address in NetworkInterface.GetAllNetworkInterfaces()
             .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
             .SelectMany(n => n.GetIPProperties().UnicastAddresses)
             .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork))
{
    ServerLog.Instance.Info($"  Same network:  http://{address.Address}:{port}");
}

app.Run();
