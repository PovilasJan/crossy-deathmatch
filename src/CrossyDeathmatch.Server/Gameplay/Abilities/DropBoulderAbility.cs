using CrossyDeathmatch.Server.Gameplay.Obstacles;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

public sealed class DropBoulderAbility() : Ability(AbilityKind.Boulder)
{
    protected override string? Apply(IAbilityContext context, Lane? lane, int column, long now)
    {
        if (lane!.Trees.Contains(column)) return "A tree is in the way";
        if (lane.Obstacles.Any(o => o.Kind == ObstacleKind.Boulder && (int)o.X == column)) return "There is already a boulder there";

        var landsAt = now + GameRules.BoulderFallMs;
        lane.AddObstacle(new Boulder(column, landsAt, landsAt + Info.DurationMs));
        return null;
    }
}
