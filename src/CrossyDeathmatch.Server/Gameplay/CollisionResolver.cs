using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>Decides whether a runner has just been killed, and by what.</summary>
public sealed class CollisionResolver
{
    // The runner's hitbox is a little narrower than its tile, so grazing a bumper is forgiven.
    private const double HitboxInset = 0.2;

    public string? FindHazard(Runner runner, GameMap map, long now)
    {
        var lane = map.GetLane(runner.Lane);
        if (lane is null) return null;

        if (lane.Type == LaneType.River)
        {
            if (runner.CenterX < 0 || runner.CenterX > GameRules.GridWidth) return "was swept away by the river";
            if (lane.FindLogUnder(runner.CenterX) is null) return "fell into the river";
        }

        if (runner.IsInvulnerable(now)) return null;

        var from = runner.X + HitboxInset;
        var to = runner.X + 1 - HitboxInset;
        var hit = lane.Obstacles.FirstOrDefault(o => o.IsLethal(now) && o.Overlaps(from, to));
        return hit is null ? null : Describe(hit.Kind);
    }

    private static string Describe(ObstacleKind kind) => kind switch
    {
        ObstacleKind.Car => "got flattened by a car",
        ObstacleKind.Truck => "got flattened by a truck",
        ObstacleKind.Train => "was hit by a train",
        ObstacleKind.Fox => "was caught by a fox",
        ObstacleKind.Boulder => "was crushed by a boulder",
        _ => "was hit",
    };
}
