using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Factory: the one place obstacles are made. The map, the traffic and the Mastermind's abilities
/// ask for a kind and get a ready obstacle, so none of them needs to know its length, speed or timers.
/// </summary>
public static class ObstacleFactory
{
    /// <summary>Without an X, traffic enters from the lane's upstream edge. A fox always starts on a random side.</summary>
    public static Obstacle Create(ObstacleKind kind, Lane lane, long now, double? x = null)
    {
        switch (kind)
        {
            case ObstacleKind.Car:
            case ObstacleKind.Truck:
            case ObstacleKind.Train:
                var length = kind switch { ObstacleKind.Truck => 2, ObstacleKind.Train => 12, _ => 1 };
                return new Obstacle
                {
                    Kind = kind,
                    Length = length,
                    Speed = lane.Speed,
                    X = x ?? (lane.Speed > 0 ? -length : Rules.GridWidth),
                };

            case ObstacleKind.Fox:
                var fromLeft = Random.Shared.Next(2) == 0;
                return new Obstacle
                {
                    Kind = ObstacleKind.Fox,
                    X = fromLeft ? 0 : Rules.GridWidth - 1,
                    Speed = fromLeft ? 2.2 : -2.2,
                    ExpiresAt = now + Rules.FoxLifeMs,
                };

            case ObstacleKind.Boulder:
                return new Obstacle
                {
                    Kind = ObstacleKind.Boulder,
                    X = x ?? 0,
                    LandsAt = now + Rules.BoulderFallMs,
                    ExpiresAt = now + Rules.BoulderFallMs + Rules.BoulderLifeMs,
                };

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    /// <summary>A car, or now and then a truck.</summary>
    public static Obstacle CreateVehicle(Lane lane, long now, double? x = null) =>
        Create(Game.Chance(0.25) ? ObstacleKind.Truck : ObstacleKind.Car, lane, now, x);
}
