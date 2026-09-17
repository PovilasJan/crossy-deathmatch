using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

/// <summary>Enemy animal released by the Mastermind. Paces back and forth across a lane until it expires.</summary>
public sealed class Fox(double x, double velocity, long expiresAt) : Obstacle(ObstacleKind.Fox, x, 1, velocity)
{
    public override void Update(double dt, long now)
    {
        X += Velocity * dt;
        var max = GameRules.GridWidth - 1;
        if (X < 0)
        {
            X = -X;
            Velocity = Math.Abs(Velocity);
        }
        else if (X > max)
        {
            X = 2 * max - X;
            Velocity = -Math.Abs(Velocity);
        }
    }

    public override bool IsLethal(long now) => true;

    public override bool IsExpired(long now) => now >= expiresAt;
}
