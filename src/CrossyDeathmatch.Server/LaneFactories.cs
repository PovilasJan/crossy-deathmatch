using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Abstract Factory: makes the family of objects one lane type needs. A road needs the road builder
/// and road traffic, a rail the rail builder and trains; the factory makes sure they always match.
/// </summary>
public interface ILaneFactory
{
    LaneBuilder CreateBuilder();
    ILaneTraffic CreateTraffic();
}

/// <summary>
/// The setup code: the only place that names the concrete factories. Game gets them back as
/// <see cref="ILaneFactory"/>, so it never knows which concrete factory it is using.
/// </summary>
public static class LaneFactorySetup
{
    public static Dictionary<LaneType, ILaneFactory> CreateFactories() => new()
    {
        [LaneType.Grass] = new GrassLaneFactory(),
        [LaneType.Road] = new RoadLaneFactory(),
        [LaneType.Rail] = new RailLaneFactory(),
    };
}

public class GrassLaneFactory : ILaneFactory
{
    public LaneBuilder CreateBuilder() => new GrassLaneBuilder();
    public ILaneTraffic CreateTraffic() => new GrassTraffic();
}

public class RoadLaneFactory : ILaneFactory
{
    public LaneBuilder CreateBuilder() => new RoadLaneBuilder();
    public ILaneTraffic CreateTraffic() => new RoadTraffic();
}

public class RailLaneFactory : ILaneFactory
{
    public LaneBuilder CreateBuilder() => new RailLaneBuilder();
    public ILaneTraffic CreateTraffic() => new RailTraffic();
}

/// <summary>Brings new traffic into a lane during the round, every tick.</summary>
public interface ILaneTraffic
{
    void Update(Lane lane, double dt, long now);
}

/// <summary>Grass has no traffic; foxes only come from the Mastermind.</summary>
public class GrassTraffic : ILaneTraffic
{
    public void Update(Lane lane, double dt, long now) { }
}

public class RoadTraffic : ILaneTraffic
{
    public void Update(Lane lane, double dt, long now)
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

public class RailTraffic : ILaneTraffic
{
    public void Update(Lane lane, double dt, long now)
    {
        if (now < lane.NextTrainAt) return;
        lane.Obstacles.Add(ObstacleFactory.Create(ObstacleKind.Train, lane, now));
        lane.NextTrainAt = now + (long)Game.Between(5000, 10000);
    }
}
