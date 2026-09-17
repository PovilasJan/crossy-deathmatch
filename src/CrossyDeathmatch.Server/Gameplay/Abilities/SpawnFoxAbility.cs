using CrossyDeathmatch.Server.Gameplay.Obstacles;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

public sealed class SpawnFoxAbility() : Ability(AbilityKind.Fox)
{
    private const double FoxSpeed = 2.2;
    private const int MaxFoxesPerLane = 2;

    protected override string? Apply(IAbilityContext context, Lane? lane, int column, long now)
    {
        if (lane!.Obstacles.Count(o => o.Kind == ObstacleKind.Fox) >= MaxFoxesPerLane) return "That lane already has enough foxes";

        var fromLeft = Rng.Chance(0.5);
        var start = fromLeft ? 0 : GameRules.GridWidth - 1;
        lane.AddObstacle(new Fox(start, fromLeft ? FoxSpeed : -FoxSpeed, now + Info.DurationMs));
        return null;
    }
}
