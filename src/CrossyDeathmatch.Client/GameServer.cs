using System.Text.Json;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrossyDeathmatch.Client;

/// <summary>Everything the client asks of the server, and what it hears back. Events arrive on a background thread.</summary>
public interface IGameServer : IAsyncDisposable
{
    event Action<GameState>? StateReceived;
    event Action<string>? MessageReceived;
    event Action? Disconnected;

    Task ConnectAsync(string url);
    Task JoinAsync(string name);
    void PickAnimal(Animal animal);
    void SetReady(bool ready);
    void Move(int dx, int dy);
    void UseAbility(Ability ability, int lane, int column);
    void UndoAbility();
}

/// <summary>
/// Adapter: makes a SignalR <see cref="HubConnection"/> usable as an <see cref="IGameServer"/>. The connection only
/// knows hub method names with loose arguments and JSON text; this class turns the typed game calls into those
/// and the "State" JSON back into a <see cref="GameState"/>, so nothing else in the client needs to know SignalR.
/// </summary>
public class SignalRGameServer : IGameServer
{
    private HubConnection? _connection;

    public event Action<GameState>? StateReceived;
    public event Action<string>? MessageReceived;
    public event Action? Disconnected;

    /// <summary>Drops any earlier connection first, so connecting again always starts clean.</summary>
    public async Task ConnectAsync(string url)
    {
        await DisposeAsync();

        var connection = new HubConnectionBuilder().WithUrl(url.Trim().TrimEnd('/') + "/game").Build();
        connection.On<string>("State", json => StateReceived?.Invoke(JsonSerializer.Deserialize<GameState>(json)!));
        connection.On<string>("Message", text => MessageReceived?.Invoke(text));
        connection.Closed += _ =>
        {
            // A connection that has been replaced still reports closing; only the current one counts.
            if (_connection == connection) Disconnected?.Invoke();
            return Task.CompletedTask;
        };
        _connection = connection;
        await connection.StartAsync();
    }

    public Task JoinAsync(string name) => _connection?.SendAsync("Join", name) ?? Task.CompletedTask;

    public void PickAnimal(Animal animal) => Send("PickAnimal", animal);

    public void SetReady(bool ready) => Send("SetReady", ready);

    public void Move(int dx, int dy) => Send("Move", dx, dy);

    public void UseAbility(Ability ability, int lane, int column) => Send("UseAbility", ability, lane, column);

    public void UndoAbility() => Send("UndoAbility");

    public async ValueTask DisposeAsync()
    {
        var connection = _connection;
        if (connection == null) return;
        _connection = null;
        await connection.DisposeAsync();
    }

    private void Send(string method, params object?[] args)
    {
        if (_connection?.State == HubConnectionState.Connected) _ = _connection.SendCoreAsync(method, args);
    }
}
