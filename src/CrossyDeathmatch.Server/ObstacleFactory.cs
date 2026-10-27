using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Factory: the one place obstacles are made. The map, the traffic and the Mastermind's abilities
/// ask for a kind and get a ready obstacle, so none of them needs to know its length, speed or timers.
/// </summary>
public static class ObstacleFactory
{
    private static readonly PrototypeRegistry Registry = CreateRegistry();

    private static PrototypeRegistry CreateRegistry()
    {
        var registry = new PrototypeRegistry();
        registry.Add(ObstacleKind.Car, new Obstacle { Kind = ObstacleKind.Car, Length = 1 });
        registry.Add(ObstacleKind.Truck, new Obstacle { Kind = ObstacleKind.Truck, Length = 2 });
        registry.Add(ObstacleKind.Train, new Obstacle { Kind = ObstacleKind.Train, Length = 12 });
        registry.Add(ObstacleKind.Fox, new Obstacle { Kind = ObstacleKind.Fox, Speed = 2.2 });
        registry.Add(ObstacleKind.Boulder, new Obstacle { Kind = ObstacleKind.Boulder });
        return registry;
    }

    /// <summary>Without an X, traffic enters from the lane's upstream edge. A fox always starts on a random side.</summary>
    public static Obstacle Create(ObstacleKind kind, Lane lane, long now, double? x = null)
    {
        var obstacle = Registry.Create(kind);
        switch (kind)
        {
            case ObstacleKind.Car:
            case ObstacleKind.Truck:
            case ObstacleKind.Train:
                obstacle.Speed = lane.Speed;
                obstacle.X = x ?? (lane.Speed > 0 ? -obstacle.Length : Rules.GridWidth);
                break;

            case ObstacleKind.Fox:
                var fromLeft = Random.Shared.Next(2) == 0;
                obstacle.X = fromLeft ? 0 : Rules.GridWidth - 1;
                obstacle.Speed = fromLeft ? obstacle.Speed : -obstacle.Speed;
                obstacle.ExpiresAt = now + Rules.FoxLifeMs;
                break;

            case ObstacleKind.Boulder:
                obstacle.X = x ?? 0;
                obstacle.LandsAt = now + Rules.BoulderFallMs;
                obstacle.ExpiresAt = now + Rules.BoulderFallMs + Rules.BoulderLifeMs;
                break;
        }
        return obstacle;
    }

    /// <summary>A car, or now and then a truck.</summary>
    public static Obstacle CreateVehicle(Lane lane, long now, double? x = null) =>
        Create(Game.Chance(0.25) ? ObstacleKind.Truck : ObstacleKind.Car, lane, now, x);
}
