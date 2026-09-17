namespace CrossyDeathmatch.Shared;

public sealed record CharacterStats(CharacterKind Kind, string Name, int Lives, int HopCooldownMs, string Description);

public static class CharacterCatalog
{
    public static readonly IReadOnlyList<CharacterStats> All =
    [
        new(CharacterKind.Chicken, "Chicken", 3, 170, "All-rounder"),
        new(CharacterKind.Rabbit, "Rabbit", 2, 110, "Fastest hops, fewest lives"),
        new(CharacterKind.Duck, "Duck", 4, 230, "Tough, but hops slower"),
        new(CharacterKind.Pig, "Pig", 5, 320, "Tank with very slow hops"),
    ];

    public static CharacterStats Get(CharacterKind kind) => All.First(c => c.Kind == kind);
}
