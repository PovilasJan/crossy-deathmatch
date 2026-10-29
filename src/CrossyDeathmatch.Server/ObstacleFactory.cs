using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Factory Method client: the one place obstacles are asked for. The map, the traffic and the Mastermind's
/// abilities ask for a kind and get a ready obstacle from that kind's creator, so none of them needs to know
/// its length, speed or timers.
/// </summary>
public static class ObstacleFactory
{
    public static readonly PrototypeRegistry Registry = CreateRegistry();

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

    // Factory Method: one concrete creator per kind; each one makes and places its own obstacles.
    private static readonly Dictionary<ObstacleKind, ObstacleCreator> Creators = new()
    {
        [ObstacleKind.Car] = new CarCreator(),
        [ObstacleKind.Truck] = new TruckCreator(),
        [ObstacleKind.Train] = new TrainCreator(),
        [ObstacleKind.Fox] = new FoxCreator(),
        [ObstacleKind.Boulder] = new BoulderCreator(),
    };

    /// <summary>Picks the creator for the kind and lets it spawn the obstacle on the lane.</summary>
    public static Obstacle Create(ObstacleKind kind, Lane lane, long now, double? x = null) =>
        Creators[kind].Spawn(lane, now, x);

    /// <summary>A car, or now and then a truck.</summary>
    public static Obstacle CreateVehicle(Lane lane, long now, double? x = null) =>
        Create(Game.Chance(0.25) ? ObstacleKind.Truck : ObstacleKind.Car, lane, now, x);
}
