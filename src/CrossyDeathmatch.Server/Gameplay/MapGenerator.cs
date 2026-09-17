using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>
/// Produces lanes one at a time as the runners advance. Hazard groups (roads, rivers, rails)
/// alternate with grass so there is always somewhere to catch a breath. Difficulty grows with distance.
/// </summary>
public sealed class MapGenerator
{
    private const int ReachableColumnsPerGrassLane = 3;

    private LaneType _groupType = LaneType.Grass;
    private int _remainingInGroup;
    private Lane? _previous;

    public Lane CreateLane(int index)
    {
        var lane = index < GameRules.StartSafeLanes ? CreateStartLane(index) : CreateNext(index);
        _previous = lane;
        return lane;
    }

    private static double Difficulty(int index) => Math.Min(1.0, index / 150.0);

    private Lane CreateNext(int index)
    {
        if (_remainingInGroup <= 0) PickNextGroup(index);
        _remainingInGroup--;

        var difficulty = Difficulty(index);
        return _groupType switch
        {
            LaneType.Road => CreateRoad(index, difficulty),
            LaneType.River => CreateRiver(index, difficulty),
            LaneType.Rail => CreateRail(index),
            _ => CreateGrass(index, difficulty),
        };
    }

    private void PickNextGroup(int index)
    {
        var difficulty = Difficulty(index);
        if (_groupType != LaneType.Grass)
        {
            _groupType = LaneType.Grass;
            _remainingInGroup = Rng.Chance(0.35 - difficulty * 0.2) ? 2 : 1;
            return;
        }

        var roll = Random.Shared.NextDouble();
        (_groupType, _remainingInGroup) = roll switch
        {
            < 0.45 => (LaneType.Road, Rng.Int(1, 2 + (int)Math.Round(difficulty * 3))),
            < 0.75 => (LaneType.River, Rng.Int(1, 2 + (int)Math.Round(difficulty * 2))),
            _ => (LaneType.Rail, Rng.Int(1, difficulty > 0.4 ? 2 : 1)),
        };
    }

    private static Lane CreateStartLane(int index)
    {
        var lane = new Lane(index, LaneType.Grass);
        if (Rng.Chance(0.5)) lane.Trees.Add(0);
        if (Rng.Chance(0.5)) lane.Trees.Add(GameRules.GridWidth - 1);
        return lane;
    }

    private Lane CreateGrass(int index, double difficulty)
    {
        var lane = new Lane(index, LaneType.Grass);

        // Keep a few columns free that were also free on the grass lane just below, so trees never wall the way.
        var reachable = Enumerable.Range(0, GameRules.GridWidth)
            .Where(c => _previous is not { Type: LaneType.Grass } || !_previous.Trees.Contains(c))
            .OrderBy(_ => Random.Shared.Next())
            .Take(ReachableColumnsPerGrassLane)
            .ToHashSet();

        var treeChance = 0.12 + difficulty * 0.12;
        for (var column = 0; column < GameRules.GridWidth; column++)
        {
            if (!reachable.Contains(column) && Rng.Chance(treeChance)) lane.Trees.Add(column);
        }

        PlacePickup(lane, PickupKind.Coin, 0.3);
        PlacePickup(lane, PickupKind.Speed, 0.06);
        PlacePickup(lane, PickupKind.Life, 0.035);
        return lane;
    }

    private static Lane CreateRoad(int index, double difficulty)
    {
        var traffic = new TrafficSettings(
            Speed: Rng.Between(1.6, 2.8) + difficulty * 2.5,
            Heading: Rng.Chance(0.5) ? 1 : -1,
            MinGap: Math.Max(1.5, 2.5 - difficulty),
            MaxGap: 6 - difficulty * 2,
            MinLength: 1,
            MaxLength: 1,
            TruckChance: 0.25);
        var lane = new Lane(index, LaneType.Road, traffic);
        PlacePickup(lane, PickupKind.Coin, 0.1);
        lane.Prepopulate();
        return lane;
    }

    private Lane CreateRiver(int index, double difficulty)
    {
        var heading = _previous is { Type: LaneType.River } previousRiver ? -previousRiver.Heading : Rng.Chance(0.5) ? 1 : -1;
        var traffic = new TrafficSettings(
            Speed: Rng.Between(0.9, 1.6) + difficulty * 1.2,
            Heading: heading,
            MinGap: 1.2,
            MaxGap: 2.6 + difficulty,
            MinLength: difficulty > 0.5 ? 2 : 3,
            MaxLength: difficulty > 0.5 ? 3 : 4,
            TruckChance: 0);
        var lane = new Lane(index, LaneType.River, traffic);
        lane.Prepopulate();
        return lane;
    }

    private static Lane CreateRail(int index) =>
        new(index, LaneType.Rail, new TrafficSettings(0, Rng.Chance(0.5) ? 1 : -1, 0, 0, 0, 0, 0));

    private static void PlacePickup(Lane lane, PickupKind kind, double chance)
    {
        if (!Rng.Chance(chance)) return;
        var free = Enumerable.Range(0, GameRules.GridWidth)
            .Where(c => !lane.Trees.Contains(c) && !lane.Pickups.ContainsKey(c))
            .ToList();
        if (free.Count > 0) lane.Pickups[Rng.Pick(free)] = kind;
    }
}
