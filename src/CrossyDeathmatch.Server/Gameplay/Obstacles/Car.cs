using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Obstacles;

public sealed class Car(double x, double velocity, bool isTruck)
    : Obstacle(isTruck ? ObstacleKind.Truck : ObstacleKind.Car, x, isTruck ? 2 : 1, velocity)
{
    public override bool IsLethal(long now) => true;
}
