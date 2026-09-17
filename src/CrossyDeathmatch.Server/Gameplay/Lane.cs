using CrossyDeathmatch.Server.Gameplay.Obstacles;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>How the moving things of a road, river or rail lane behave. Heading is +1 or -1.</summary>
public sealed record TrafficSettings(
    double Speed,
    int Heading,
    double MinGap,
    double MaxGap,
    int MinLength,
    int MaxLength,
    double TruckChance);

/// <summary>One horizontal strip of the map. Its type decides which hazards it spawns.</summary>
public sealed class Lane(int index, LaneType type, TrafficSettings? traffic = null)
{
    private readonly List<Obstacle> _obstacles = [];
    private double _spawnTimer;
    private long _nextTrainAt;

    public int Index { get; } = index;
    public LaneType Type { get; } = type;
    public int Heading => traffic?.Heading ?? 1;
    public HashSet<int> Trees { get; } = [];
    public Dictionary<int, PickupKind> Pickups { get; } = [];
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;
    public bool Warning { get; private set; }

    public bool IsBlocked(int column) => Trees.Contains(column) || _obstacles.Any(o => o.BlocksColumn(column));

    public int? NearestFreeColumn(int column)
    {
        for (var distance = 0; distance < GameRules.GridWidth; distance++)
        {
            foreach (var candidate in new[] { column - distance, column + distance })
            {
                if (candidate is >= 0 and < GameRules.GridWidth && !IsBlocked(candidate)) return candidate;
            }
        }
        return null;
    }

    public Obstacle? FindLogUnder(double centerX) =>
        _obstacles.FirstOrDefault(o => o.Kind == ObstacleKind.Log && centerX >= o.X - 0.1 && centerX <= o.X + o.Length + 0.1);

    public PickupKind? TakePickup(int column) => Pickups.Remove(column, out var kind) ? kind : null;

    public void AddObstacle(Obstacle obstacle) => _obstacles.Add(obstacle);

    /// <summary>Fills a road or river so it does not start empty.</summary>
    public void Prepopulate()
    {
        if (traffic is null || Type is not (LaneType.Road or LaneType.River)) return;
        var x = -2 + Rng.Between(0, traffic.MaxGap);
        while (x < GameRules.GridWidth + 1)
        {
            var mover = CreateMover(x);
            _obstacles.Add(mover);
            x += mover.Length + Rng.Between(traffic.MinGap, traffic.MaxGap);
        }
    }

    public void SpawnExtraCar()
    {
        var speed = traffic?.Speed ?? 2.5;
        var car = new Car(0, speed * Heading, isTruck: false);
        car.X = Heading > 0 ? -car.Length : GameRules.GridWidth;
        _obstacles.Add(car);
    }

    public void Update(double dt, long now)
    {
        foreach (var obstacle in _obstacles) obstacle.Update(dt, now);
        _obstacles.RemoveAll(o => o.IsExpired(now));

        switch (Type)
        {
            case LaneType.Road:
            case LaneType.River:
                UpdateTraffic(dt);
                break;
            case LaneType.Rail:
                UpdateRail(now);
                break;
        }
    }

    public LaneDto ToDto() => new(
        Index,
        Type,
        [.. Trees],
        Pickups.Select(p => new PickupDto(p.Key, p.Value)).ToList(),
        Warning);

    private void UpdateTraffic(double dt)
    {
        if (traffic is null) return;
        _spawnTimer -= dt;
        if (_spawnTimer > 0) return;

        var mover = CreateMover(0);
        mover.X = traffic.Heading > 0 ? -mover.Length : GameRules.GridWidth;
        var entryBusy = _obstacles.Any(o =>
            o.Kind is not (ObstacleKind.Fox or ObstacleKind.Boulder) &&
            o.Overlaps(mover.X - traffic.MinGap, mover.X + mover.Length + traffic.MinGap));
        if (entryBusy)
        {
            _spawnTimer = 0.15;
            return;
        }

        _obstacles.Add(mover);
        _spawnTimer = (mover.Length + Rng.Between(traffic.MinGap, traffic.MaxGap)) / traffic.Speed;
    }

    private void UpdateRail(long now)
    {
        if (_nextTrainAt == 0) _nextTrainAt = now + (long)Rng.Between(3000, 8000);
        if (now >= _nextTrainAt)
        {
            _obstacles.Add(new Train(Heading));
            _nextTrainAt = now + (long)Rng.Between(5000, 10000);
        }
        Warning = _obstacles.Any(o => o.Kind == ObstacleKind.Train) || now >= _nextTrainAt - GameRules.TrainWarningMs;
    }

    private Obstacle CreateMover(double x)
    {
        var velocity = traffic!.Speed * traffic.Heading;
        return Type == LaneType.River
            ? new Log(x, Rng.Int(traffic.MinLength, traffic.MaxLength), velocity)
            : new Car(x, velocity, Rng.Chance(traffic.TruckChance));
    }
}
