using CrossyDeathmatch.Server.Persistence;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR;

namespace CrossyDeathmatch.Server.Networking;

/// <summary>
/// Owns every signed-in player and every room. Lock order is always server lock, then session lock;
/// sessions never call back into the server synchronously (room-list changes are only flagged).
/// </summary>
public sealed class GameServer : IDisposable
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    private readonly object _sync = new();
    private readonly Dictionary<string, PlayerConnection> _players = [];
    private readonly Dictionary<string, GameSession> _sessions = [];
    private readonly IHubContext<GameHub, IGameClient> _hub;
    private readonly ServerSettings _settings;
    private readonly JsonDatabase _database;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GameServer> _log;
    private readonly Timer _roomListTimer;
    private int _roomListDirty;

    public GameServer(IHubContext<GameHub, IGameClient> hub, ServerSettings settings, JsonDatabase database, ILoggerFactory loggerFactory)
    {
        _hub = hub;
        _settings = settings;
        _database = database;
        _loggerFactory = loggerFactory;
        _log = loggerFactory.CreateLogger<GameServer>();
        _roomListTimer = new Timer(_ => PublishRoomListIfChanged(), null, 250, 250);
    }

    public void Hello(string connectionId, string name)
    {
        var client = _hub.Clients.Client(connectionId);
        var nickname = (name ?? "").Trim();
        if (nickname.Length is 0 or > GameRules.MaxNameLength)
        {
            client.Error($"Nickname must be 1 to {GameRules.MaxNameLength} characters").Forget();
            return;
        }

        lock (_sync)
        {
            if (_players.ContainsKey(connectionId))
            {
                client.Error("You are already signed in").Forget();
                return;
            }
            if (_players.Values.Any(p => string.Equals(p.Name, nickname, StringComparison.OrdinalIgnoreCase)))
            {
                client.Error("That nickname is already playing").Forget();
                return;
            }

            _players[connectionId] = new PlayerConnection(connectionId, nickname, client);
            client.Welcome(connectionId, _database.GetProfile(nickname)).Forget();
            client.RoomList(BuildRoomList()).Forget();
        }
        _log.LogInformation("{Name} signed in", nickname);
    }

    public void ListRooms(string connectionId)
    {
        lock (_sync)
        {
            if (_players.TryGetValue(connectionId, out var player)) player.Client.RoomList(BuildRoomList()).Forget();
        }
    }

    public void CreateRoom(string connectionId, bool isPublic)
    {
        lock (_sync)
        {
            if (!_players.TryGetValue(connectionId, out var player)) return;
            LeaveSession(player);

            var code = NewRoomCode();
            var session = new GameSession(code, isPublic, _settings, _database, MarkRoomListChanged, _loggerFactory.CreateLogger<GameSession>());
            _sessions[code] = session;
            session.Join(player);
            player.Session = session;
            MarkRoomListChanged();
            _log.LogInformation("{Name} created {Visibility} room {Code}", player.Name, isPublic ? "public" : "private", code);
        }
    }

    public void JoinRoom(string connectionId, string code)
    {
        lock (_sync)
        {
            if (!_players.TryGetValue(connectionId, out var player)) return;
            var normalized = (code ?? "").Trim().ToUpperInvariant();
            if (!_sessions.TryGetValue(normalized, out var session))
            {
                player.Client.Error($"No room with code {normalized}").Forget();
                return;
            }
            if (player.Session == session) return;
            if (session.JoinBlocker() is { } reason)
            {
                player.Client.Error(reason).Forget();
                return;
            }

            LeaveSession(player);
            session.Join(player);
            player.Session = session;
            MarkRoomListChanged();
        }
    }

    public void LeaveRoom(string connectionId)
    {
        lock (_sync)
        {
            if (!_players.TryGetValue(connectionId, out var player) || player.Session is null) return;
            LeaveSession(player);
            player.Client.LeftRoom().Forget();
            player.Client.RoomList(BuildRoomList()).Forget();
        }
    }

    public void Disconnect(string connectionId)
    {
        lock (_sync)
        {
            if (!_players.Remove(connectionId, out var player)) return;
            LeaveSession(player);
            _log.LogInformation("{Name} disconnected", player.Name);
        }
    }

    /// <summary>Runs an in-room action outside the server lock, so busy rooms don't block each other.</summary>
    public void InSession(string connectionId, Action<GameSession, PlayerConnection> action)
    {
        PlayerConnection? player;
        GameSession? session;
        lock (_sync)
        {
            if (!_players.TryGetValue(connectionId, out player)) return;
            session = player.Session;
        }

        if (session is null)
        {
            player.Client.Error("You are not in a room").Forget();
            return;
        }
        action(session, player);
    }

    public void Dispose()
    {
        _roomListTimer.Dispose();
        lock (_sync)
        {
            foreach (var session in _sessions.Values) session.Dispose();
            _sessions.Clear();
        }
    }

    private void LeaveSession(PlayerConnection player)
    {
        var session = player.Session;
        if (session is null) return;

        session.Leave(player);
        player.Session = null;
        if (session.IsEmpty)
        {
            session.Dispose();
            _sessions.Remove(session.Code);
            _log.LogInformation("Room {Code} closed", session.Code);
        }
        MarkRoomListChanged();
    }

    private List<RoomSummaryDto> BuildRoomList() => _sessions.Values
        .Where(s => s.IsPublic)
        .Select(s => s.ToSummary())
        .OrderBy(s => s.Phase)
        .ThenByDescending(s => s.PlayerCount)
        .ToList();

    private void MarkRoomListChanged() => Interlocked.Exchange(ref _roomListDirty, 1);

    private void PublishRoomListIfChanged()
    {
        if (Interlocked.Exchange(ref _roomListDirty, 0) == 0) return;
        lock (_sync)
        {
            var rooms = BuildRoomList();
            foreach (var player in _players.Values.Where(p => p.Session is null)) player.Client.RoomList(rooms).Forget();
        }
    }

    private string NewRoomCode()
    {
        string code;
        do
        {
            code = string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]));
        } while (_sessions.ContainsKey(code));
        return code;
    }
}
