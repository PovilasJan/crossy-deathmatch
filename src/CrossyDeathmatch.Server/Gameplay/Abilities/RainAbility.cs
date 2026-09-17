using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

public sealed class RainAbility() : Ability(AbilityKind.Rain)
{
    protected override string? Apply(IAbilityContext context, Lane? lane, int column, long now)
    {
        context.Effects.RainUntil = now + Info.DurationMs;
        return null;
    }
}
