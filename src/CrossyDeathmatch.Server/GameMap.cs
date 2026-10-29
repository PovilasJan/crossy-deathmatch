using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

/// <summary>
/// Facade: the one entry point to the endless map. Behind it, a lane factory (Abstract Factory) per lane type
/// supplies the builder (Builder) that lays a new lane out and the traffic that keeps it moving, and obstacles
/// come from <see cref="ObstacleFactory"/> (Factory), which copies them from prototypes (Prototype). Callers
/// only start, advance and look up the map; none of them needs to know how a lane or an obstacle is made.
/// </summary>
public class GameMap(List<Lane> lanes)
{
    private const int LanesAhead = 20;
    private const int LanesBehind = 8;

    // Abstract Factory: one factory per lane type, each making a builder and traffic that belong together.
    private static readonly Dictionary<LaneType, ILaneFactory> LaneFactories = new()
    {
        [LaneType.Grass] = new GrassLaneFactory(),
        [LaneType.Road] = new RoadLaneFactory(),
        [LaneType.Rail] = new RailLaneFactory(),
    };

    private static readonly Dictionary<LaneType, LaneBuilder> Builders =
        LaneFactories.ToDictionary(f => f.Key, f => f.Value.CreateBuilder());

    private static readonly Dictionary<LaneType, ILaneTraffic> Traffic =
        LaneFactories.ToDictionary(f => f.Key, f => f.Value.CreateTraffic());

    private readonly List<Lane> _lanes = lanes;

    /// <summary>Whether new obstacles are deep or shallow copies of their prototypes.</summary>
    public static bool UseDeepCopy
    {
        get => ObstacleFactory.Registry.UseDeepCopy;
        set => ObstacleFactory.Registry.UseDeepCopy = value;
    }

    /// <summary>Throws the old map away and lays out the start area and the lanes ahead of it.</summary>
    public void Reset(long now)
    {
        _lanes.Clear();
        while (_lanes.Count <= LanesAhead) AddLane(now);
    }

    public void Clear() => _lanes.Clear();

    /// <summary>Moves every obstacle on and brings in new traffic. Returns the boulders that landed during this update.</summary>
    public List<(Lane Lane, Obstacle Boulder)> Update(double dt, long now)
    {
        var landed = new List<(Lane, Obstacle)>();
        foreach (var lane in _lanes) UpdateLane(lane, dt, now, landed);
        return landed;
    }

    /// <summary>Generates lanes ahead of the leading runner and drops the ones far behind the last one.</summary>
    public void Follow(int lowestLane, int highestLane, long now)
    {
        while (_lanes[^1].Index < highestLane + LanesAhead) AddLane(now);
        while (_lanes[0].Index < lowestLane - LanesBehind) _lanes.RemoveAt(0);
    }

    public Lane? GetLane(int index)
    {
        if (_lanes.Count == 0) return null;
        var position = index - _lanes[0].Index;
        return position >= 0 && position < _lanes.Count ? _lanes[position] : null;
    }

    private static void UpdateLane(Lane lane, double dt, long now, List<(Lane, Obstacle)> landed)
    {
        foreach (var obstacle in lane.Obstacles)
        {
            obstacle.X += obstacle.Speed * dt;

            if (obstacle.Kind == ObstacleKind.Fox)
            {
                // A fox turns around at the edges instead of leaving.
                var max = Rules.GridWidth - 1;
                if (obstacle.X < 0)
                {
                    obstacle.X = -obstacle.X;
                    obstacle.Speed = Math.Abs(obstacle.Speed);
                }
                else if (obstacle.X > max)
                {
                    obstacle.X = 2 * max - obstacle.X;
                    obstacle.Speed = -Math.Abs(obstacle.Speed);
                }
            }

            if (obstacle.Kind == ObstacleKind.Boulder && !obstacle.Landed && now >= obstacle.LandsAt)
            {
                obstacle.Landed = true;
                landed.Add((lane, obstacle));
            }
        }

        lane.Obstacles.RemoveAll(o =>
            (o.ExpiresAt > 0 && now >= o.ExpiresAt) ||
            (o.Kind != ObstacleKind.Fox && o.Speed > 0 && o.X > Rules.GridWidth + 1) ||
            (o.Kind != ObstacleKind.Fox && o.Speed < 0 && o.X + o.Length < -1));

        Traffic[lane.Type].Update(lane, dt, now);
    }

    /// <summary>
    /// Adds the next lane on top of the map: the first lanes are grass, after that a random type.
    /// Builder: this is the director, the lane builders do the steps that differ per lane type.
    /// </summary>
    private void AddLane(long now)
    {
        var index = _lanes.Count > 0 ? _lanes[^1].Index + 1 : 0;
        var type = index < Game.StartLanes ? LaneType.Grass : (LaneType)Random.Shared.Next(3); // 0 Grass, 1 Road, 2 Rail

        var builder = Builders[type];
        builder.Reset(index, type);
        builder.BuildTrees();
        builder.BuildTraffic(now);
        builder.BuildCoins();
        _lanes.Add(builder.GetLane());
    }
}
