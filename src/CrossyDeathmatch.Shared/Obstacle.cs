namespace CrossyDeathmatch.Shared;

/// <summary>Something inside a lane: traffic, a train, a fox or a boulder. X is the left edge in tiles.</summary>
public class Obstacle
{
    public ObstacleKind Kind { get; set; }
    public double X { get; set; }
    public int Length { get; set; } = 1;
    public double Speed { get; set; }
    public long LandsAt { get; set; }
    public bool Landed { get; set; }
    public long ExpiresAt { get; set; }
}
