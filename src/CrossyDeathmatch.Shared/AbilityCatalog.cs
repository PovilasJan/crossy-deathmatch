namespace CrossyDeathmatch.Shared;

public sealed record AbilityInfo(
    AbilityKind Kind,
    string Name,
    int Key,
    int Cost,
    int CooldownMs,
    AbilityTarget Target,
    int DurationMs,
    IReadOnlyList<LaneType> AllowedLanes,
    string Description)
{
    public bool WorksOn(LaneType type) => AllowedLanes.Count == 0 || AllowedLanes.Contains(type);
}

public static class AbilityCatalog
{
    public static readonly IReadOnlyList<AbilityInfo> All =
    [
        new(AbilityKind.Car, "Send car", 1, 12, 1200, AbilityTarget.Lane, 0,
            [LaneType.Road], "Launch an extra car down a road lane"),
        new(AbilityKind.Boulder, "Drop boulder", 2, 25, 3000, AbilityTarget.Tile, 8000,
            [LaneType.Grass, LaneType.Road, LaneType.Rail], "Crushes a tile after 1 s, then blocks it"),
        new(AbilityKind.Fox, "Release fox", 3, 30, 6000, AbilityTarget.Lane, 15000,
            [LaneType.Grass], "A fox patrols a grass lane for 15 s"),
        new(AbilityKind.Rain, "Rain", 4, 45, 20000, AbilityTarget.None, 10000,
            [], "Every runner hops slower for 10 s"),
        new(AbilityKind.Night, "Night", 5, 55, 25000, AbilityTarget.None, 10000,
            [], "Runners can barely see for 10 s"),
    ];

    public static AbilityInfo Get(AbilityKind kind) => All.First(a => a.Kind == kind);
}
