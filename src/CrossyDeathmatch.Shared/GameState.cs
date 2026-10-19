namespace CrossyDeathmatch.Shared;

/// <summary>Everything the server sends to the clients, as JSON. 20 times a second during a round.</summary>
public class GameState
{
    public long Time { get; set; }
    public bool Playing { get; set; }
    public int TimeLeftMs { get; set; }
    public long RainUntil { get; set; }
    public string? Result { get; set; }
    public List<Player> Players { get; set; } = [];
    public List<Lane> Lanes { get; set; } = [];
}
