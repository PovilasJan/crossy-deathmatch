using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

/// <summary>Anything that lives inside a lane and moves or blocks. Positions are in tiles.</summary>
public abstract class Obstacle
{
    private static int _nextId;

    protected Obstacle(ObstacleKind kind, double x, int length, double velocity)
    {
        Id = Interlocked.Increment(ref _nextId);
        Kind = kind;
        X = x;
        Length = length;
        Velocity = velocity;
    }

    public int Id { get; }
    public ObstacleKind Kind { get; }
    public double X { get; set; }
    public int Length { get; }
    public double Velocity { get; protected set; }

    public virtual void Update(double dt, long now) => X += Velocity * dt;

    public abstract bool IsLethal(long now);

    public virtual bool BlocksColumn(int column) => false;

    public virtual bool IsExpired(long now) => Velocity switch
    {
        > 0 => X > GameRules.GridWidth + 1,
        < 0 => X + Length < -1,
        _ => false,
    };

    public bool Overlaps(double from, double to) => X < to && X + Length > from;

    public virtual ObstacleDto ToDto(int lane) =>
        new(Id, Kind, lane, (float)Math.Round(X, 2), Length, Math.Sign(Velocity), Landed: true);
}
