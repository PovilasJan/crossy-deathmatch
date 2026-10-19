using System.Text.Json.Serialization;

namespace CrossyDeathmatch.Shared;

/// <summary>One horizontal strip of the map.</summary>
public class Lane
{
    public int Index { get; set; }
    public LaneType Type { get; set; }

    /// <summary>Tiles per second of the lane's traffic; the sign is the direction.</summary>
    public double Speed { get; set; }

    public List<int> Trees { get; set; } = [];
    public List<int> Coins { get; set; } = [];
    public List<Obstacle> Obstacles { get; set; } = [];
    public long NextTrainAt { get; set; }

    [JsonIgnore]
    public double SpawnTimer { get; set; }
}
