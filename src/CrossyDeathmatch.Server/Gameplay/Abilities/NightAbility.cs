using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

public sealed class NightAbility() : Ability(AbilityKind.Night)
{
    protected override string? Apply(IAbilityContext context, Lane? lane, int column, long now)
    {
        context.Effects.NightUntil = now + Info.DurationMs;
        return null;
    }
}
