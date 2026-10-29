using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Factory Method: the creator. <see cref="Spawn"/> is the logic every obstacle shares;
/// <see cref="CreateObstacle"/> is the factory method each concrete creator overrides to make its own kind.
/// </summary>
public abstract class ObstacleCreator
{
    public abstract Obstacle CreateObstacle();

    /// <summary>Makes an obstacle and sets the fields that depend on the lane and the time.</summary>
    public Obstacle Spawn(Lane lane, long now, double? x = null)
    {
        var obstacle = CreateObstacle();
        Place(obstacle, lane, now, x);
        return obstacle;
    }

    /// <summary>Without an X, traffic enters from the lane's upstream edge.</summary>
    protected virtual void Place(Obstacle obstacle, Lane lane, long now, double? x)
    {
        obstacle.Speed = lane.Speed;
        obstacle.X = x ?? (lane.Speed > 0 ? -obstacle.Length : Rules.GridWidth);
    }
}

public class CarCreator : ObstacleCreator
{
    public override Obstacle CreateObstacle() => ObstacleFactory.Registry.Create(ObstacleKind.Car);
}

public class TruckCreator : ObstacleCreator
{
    public override Obstacle CreateObstacle() => ObstacleFactory.Registry.Create(ObstacleKind.Truck);
}

public class TrainCreator : ObstacleCreator
{
    public override Obstacle CreateObstacle() => ObstacleFactory.Registry.Create(ObstacleKind.Train);
}

public class FoxCreator : ObstacleCreator
{
    public override Obstacle CreateObstacle() => ObstacleFactory.Registry.Create(ObstacleKind.Fox);

    /// <summary>A fox always starts on a random side and leaves after a while.</summary>
    protected override void Place(Obstacle obstacle, Lane lane, long now, double? x)
    {
        var fromLeft = Random.Shared.Next(2) == 0;
        obstacle.X = fromLeft ? 0 : Rules.GridWidth - 1;
        obstacle.Speed = fromLeft ? obstacle.Speed : -obstacle.Speed;
        obstacle.ExpiresAt = now + Rules.FoxLifeMs;
    }
}

public class BoulderCreator : ObstacleCreator
{
    public override Obstacle CreateObstacle() => ObstacleFactory.Registry.Create(ObstacleKind.Boulder);

    protected override void Place(Obstacle obstacle, Lane lane, long now, double? x)
    {
        obstacle.X = x ?? 0;
        obstacle.LandsAt = now + Rules.BoulderFallMs;
        obstacle.ExpiresAt = now + Rules.BoulderFallMs + Rules.BoulderLifeMs;
    }
}
