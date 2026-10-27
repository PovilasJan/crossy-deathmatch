using System.Text.Json.Serialization;

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

    [JsonIgnore]
    public Hitbox Hitbox { get; set; } = new();

    public Obstacle ShallowClone() => (Obstacle)MemberwiseClone();

    public Obstacle DeepClone()
    {
        var copy = (Obstacle)MemberwiseClone();
        copy.Hitbox = new Hitbox { Inset = Hitbox.Inset };
        return copy;
    }
}
