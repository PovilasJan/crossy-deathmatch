using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

/// <summary>Floating log on a river. Runners standing on it are carried along.</summary>
public sealed class Log(double x, int length, double velocity) : Obstacle(ObstacleKind.Log, x, length, velocity)
{
    public override bool IsLethal(long now) => false;
}
