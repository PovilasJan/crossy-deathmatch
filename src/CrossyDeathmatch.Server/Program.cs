using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using CrossyDeathmatch.Server;
using CrossyDeathmatch.Server.Networking;
using CrossyDeathmatch.Server.Persistence;
using CrossyDeathmatch.Shared;

var builder = WebApplication.CreateBuilder(args);
var settings = ServerSettings.From(builder.Configuration);

builder.WebHost.UseUrls($"http://0.0.0.0:{settings.Port}");
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(new JsonDatabase(Path.Combine(builder.Environment.ContentRootPath, "data", "db.json")));
builder.Services.AddSingleton<GameServer>();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.MapGet("/", () => $"Crossy Deathmatch server is running. Point the client at this address. " +
                      $"Min players: {settings.MinPlayers}, round length: {settings.RoundSeconds}s.");
app.MapHub<GameHub>(HubRoutes.GameHub);

app.Lifetime.ApplicationStarted.Register(() =>
{
    // Resolve the server eagerly so its room-list timer is running before anyone connects.
    app.Services.GetRequiredService<GameServer>();

    Console.WriteLine();
    Console.WriteLine($"  Crossy Deathmatch server on port {settings.Port}");
    Console.WriteLine($"  Min players to start: {settings.MinPlayers}   Round length: {settings.RoundSeconds}s");
    Console.WriteLine($"  This machine:  http://localhost:{settings.Port}");
    foreach (var address in LanAddresses()) Console.WriteLine($"  Same network:  http://{address}:{settings.Port}");
    Console.WriteLine();
});

app.Run();

static IEnumerable<IPAddress> LanAddresses() => NetworkInterface.GetAllNetworkInterfaces()
    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
    .Select(a => a.Address)
    .Where(a => a.AddressFamily == AddressFamily.InterNetwork);
