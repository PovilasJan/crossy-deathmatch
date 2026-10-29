using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Abstract Factory: makes the family of objects one lane type needs. A road needs the road builder, road
/// traffic and the road's respawn rule, a rail the rail builder, trains and the rail's respawn rule; the
/// factory makes sure they always match.
/// </summary>
public abstract class LaneFactory
{
    public abstract LaneBuilder CreateBuilder();
    public abstract LaneTraffic CreateTraffic();
    public abstract LaneRespawn CreateRespawn();
}

/// <summary>
/// The setup code: the only place that names the concrete factories. Game gets them back as
/// <see cref="LaneFactory"/>, so it never knows which concrete factory it is using.
/// </summary>
public static class LaneFactorySetup
{
    public static Dictionary<LaneType, LaneFactory> CreateFactories() => new()
    {
        [LaneType.Grass] = new GrassLaneFactory(),
        [LaneType.Road] = new RoadLaneFactory(),
        [LaneType.Rail] = new RailLaneFactory(),
    };
}

public class GrassLaneFactory : LaneFactory
{
    public override LaneBuilder CreateBuilder() => new GrassLaneBuilder();
    public override LaneTraffic CreateTraffic() => new GrassTraffic();
    public override LaneRespawn CreateRespawn() => new GrassRespawn();
}

public class RoadLaneFactory : LaneFactory
{
    public override LaneBuilder CreateBuilder() => new RoadLaneBuilder();
    public override LaneTraffic CreateTraffic() => new RoadTraffic();
    public override LaneRespawn CreateRespawn() => new RoadRespawn();
}

public class RailLaneFactory : LaneFactory
{
    public override LaneBuilder CreateBuilder() => new RailLaneBuilder();
    public override LaneTraffic CreateTraffic() => new RailTraffic();
    public override LaneRespawn CreateRespawn() => new RailRespawn();
}

/// <summary>Brings new traffic into a lane during the round, every tick.</summary>
public abstract class LaneTraffic
{
    public abstract void Update(Lane lane, double dt, long now);
}

/// <summary>Grass has no traffic; foxes only come from the Mastermind. A null object, so Game needs no check.</summary>
public class GrassTraffic : LaneTraffic
{
    public override void Update(Lane lane, double dt, long now) { }
}

public class RoadTraffic : LaneTraffic
{
    public override void Update(Lane lane, double dt, long now)
    {
        lane.SpawnTimer -= dt;
        if (lane.SpawnTimer > 0) return;

        var car = ObstacleFactory.CreateVehicle(lane, now);
        var entryBusy = lane.Obstacles.Any(o => o.Kind is ObstacleKind.Car or ObstacleKind.Truck &&
                                                o.X < car.X + car.Length + Game.MinCarGap && o.X + o.Length > car.X - Game.MinCarGap);
        if (entryBusy)
        {
            lane.SpawnTimer = 0.15;
            return;
        }
        lane.Obstacles.Add(car);
        lane.SpawnTimer = (car.Length + Game.Between(Game.MinCarGap, Game.MaxCarGap)) / Math.Abs(lane.Speed);
    }
}

public class RailTraffic : LaneTraffic
{
    public override void Update(Lane lane, double dt, long now)
    {
        if (now < lane.NextTrainAt) return;
        lane.Obstacles.Add(ObstacleFactory.Create(ObstacleKind.Train, lane, now));
        lane.NextTrainAt = now + (long)Game.Between(5000, 10000);
    }
}

/// <summary>Where a runner who lost a life can come back on a lane: a column, or null if not on this lane.</summary>
public abstract class LaneRespawn
{
    public abstract int? FindColumn(Lane lane, int nearColumn);
}

/// <summary>Grass is the safe lane: the free tile (no tree, no boulder) nearest to the column the runner was in.</summary>
public class GrassRespawn : LaneRespawn
{
    public override int? FindColumn(Lane lane, int nearColumn)
    {
        var free = Enumerable.Range(0, Rules.GridWidth)
            .Where(c => !Game.IsBlocked(lane, c))
            .OrderBy(c => Math.Abs(c - nearColumn))
            .ToList();
        return free.Count > 0 ? free[0] : null;
    }
}

/// <summary>Nobody comes back on a road. A null object, so Game needs no lane type check.</summary>
public class RoadRespawn : LaneRespawn
{
    public override int? FindColumn(Lane lane, int nearColumn) => null;
}

/// <summary>Nobody comes back on a railway. A null object, so Game needs no lane type check.</summary>
public class RailRespawn : LaneRespawn
{
    public override int? FindColumn(Lane lane, int nearColumn) => null;
}
