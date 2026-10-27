namespace CrossyDeathmatch.Shared;

/// <summary>Game numbers used by both the server and the client.</summary>
public static class Rules
{
    public const int GridWidth = 13;
    public const int TickRate = 20;
    public const int MaxPlayers = 4;
    public const int MaxNameLength = 16;
    public const int BacktrackLimit = 5;
    public const int RespawnDelayMs = 1500;
    public const int InvulnerableMs = 2000;
    public const int PointsPerCoin = 5;
    public const int TrainWarningMs = 1500;
    public const int BoulderFallMs = 1000;
    public const int BoulderLifeMs = 8000;
    public const int FoxLifeMs = 15000;
    public const int RainMs = 10000;
    public const double RainSlowdown = 1.8;
    public const double AdrenalineSpeedup = 0.7;
    public const double BackwardSlowdown = 1.5;
    public const double MaxEnergy = 100;
    public const double StartEnergy = 40;
    public const double EnergyPerSecond = 6;

    public static int Lives(Animal animal) => animal switch
    {
        Animal.Chicken => 3,
        Animal.Rabbit => 2,
        Animal.Duck => 4,
        _ => 5,
    };

    public static int HopCooldownMs(Animal animal) => animal switch
    {
        Animal.Chicken => 170,
        Animal.Rabbit => 110,
        Animal.Duck => 230,
        _ => 320,
    };

    public static int Cost(Ability ability) => ability switch
    {
        Ability.Car => 12,
        Ability.Boulder => 25,
        Ability.Fox => 30,
        _ => 45,
    };

    public static int CooldownMs(Ability ability) => ability switch
    {
        Ability.Car => 1200,
        Ability.Boulder => 3000,
        Ability.Fox => 6000,
        _ => 20000,
    };

    public static string Name(Ability ability) => ability switch
    {
        Ability.Car => "Send car",
        Ability.Boulder => "Drop boulder",
        Ability.Fox => "Release fox",
        _ => "Rain",
    };

    /// <summary>Cars only go on roads, foxes only on grass, boulders anywhere. Rain needs no target.</summary>
    public static bool WorksOn(Ability ability, LaneType type) => ability switch
    {
        Ability.Car => type == LaneType.Road,
        Ability.Fox => type == LaneType.Grass,
        _ => true,
    };
}
