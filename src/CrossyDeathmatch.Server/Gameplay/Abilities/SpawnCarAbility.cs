using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay.Abilities;

public sealed class SpawnCarAbility() : Ability(AbilityKind.Car)
{
    protected override string? Apply(IAbilityContext context, Lane? lane, int column, long now)
    {
        lane!.SpawnExtraCar();
        return null;
    }
}
