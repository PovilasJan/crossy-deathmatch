namespace CrossyDeathmatch.Shared;

public class Obstacle : IPrototype<Obstacle>
{
    public ObstacleKind Kind { get; set; }
    public double X { get; set; }
    public int Length { get; set; } = 1;
    public double Speed { get; set; }
    public long LandsAt { get; set; }
    public bool Landed { get; set; }
    public long ExpiresAt { get; set; }

    public Obstacle Clone() => (Obstacle)MemberwiseClone();
}
