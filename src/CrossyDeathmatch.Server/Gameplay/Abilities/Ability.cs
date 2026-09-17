using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

/// <summary>What an ability is allowed to touch.</summary>
public interface IAbilityContext
{
    GameMap Map { get; }
    WorldEffects Effects { get; }
}

public abstract class Ability(AbilityKind kind)
{
    public AbilityInfo Info { get; } = AbilityCatalog.Get(kind);

    /// <summary>Validates the target and applies the effect. Returns an error message, or null on success.</summary>
    public string? Use(IAbilityContext context, int? laneIndex, int? column, long now)
    {
        if (Info.Target == AbilityTarget.None) return Apply(context, null, 0, now);
        if (laneIndex is null) return "Choose a target on the map";

        var lane = context.Map.GetLane(laneIndex.Value);
        if (lane is null) return "That lane is out of reach";
        if (!Info.WorksOn(lane.Type))
        {
            return $"{Info.Name} only works on {string.Join(" or ", Info.AllowedLanes).ToLowerInvariant()} lanes";
        }
        if (Info.Target == AbilityTarget.Tile && column is not (>= 0 and < GameRules.GridWidth))
        {
            return "Choose a tile inside the map";
        }

        return Apply(context, lane, column ?? 0, now);
    }

    protected abstract string? Apply(IAbilityContext context, Lane? lane, int column, long now);
}
