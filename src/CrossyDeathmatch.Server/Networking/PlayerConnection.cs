using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Networking;

/// <summary>A signed-in player: their SignalR connection id, nickname, and the room they are in.</summary>
public sealed class PlayerConnection(string id, string name, IGameClient client)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public IGameClient Client { get; } = client;

    /// <summary>Only changed by <see cref="GameServer"/> while it holds its lock.</summary>
    public GameSession? Session { get; set; }
}
