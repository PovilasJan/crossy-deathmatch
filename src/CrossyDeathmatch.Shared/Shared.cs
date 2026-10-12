using System.Text.Json.Serialization;

namespace CrossyDeathmatch.Shared;

public enum LaneType { Grass, Road, Rail }

public enum ObstacleKind { Car, Truck, Train, Fox, Boulder }

public enum Animal { Chicken, Rabbit, Duck, Pig }

public enum Ability { Car, Boulder, Fox, Rain }

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

/// <summary>A connected player. During a round they are either a runner or the Mastermind.</summary>
public class Player
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Animal Animal { get; set; }
    public bool Ready { get; set; }
    public bool IsMastermind { get; set; }
    public bool Left { get; set; }

    // Runner
    public int X { get; set; }
    public int Lane { get; set; }
    public int Lives { get; set; }
    public int Score { get; set; }
    public int Coins { get; set; }
    public int MaxLane { get; set; }
    public bool Out { get; set; }
    public long RespawnAt { get; set; }
    public long InvulnerableUntil { get; set; }
    public long LastHopAt { get; set; }

    // Mastermind
    public double Energy { get; set; }

    /// <summary>Server time at which each ability (indexed by <see cref="Ability"/>) can be used again.</summary>
    public long[] ReadyAt { get; set; } = new long[4];
}

/// <summary>Something inside a lane: traffic, a train, a fox or a boulder. X is the left edge in tiles.</summary>
public class Obstacle
{
    public ObstacleKind Kind { get; set; }
    public double X { get; set; }
    public int Length { get; set; } = 1;
    public double Speed { get; set; }
    public long LandsAt { get; set; }
    public bool Landed { get; set; }
    public long ExpiresAt { get; set; }
}

/// <summary>One horizontal strip of the map.</summary>
public class Lane
{
    public int Index { get; set; }
    public LaneType Type { get; set; }

    /// <summary>Tiles per second of the lane's traffic; the sign is the direction.</summary>
    public double Speed { get; set; }

    public List<int> Trees { get; set; } = [];
    public List<int> Coins { get; set; } = [];
    public List<Obstacle> Obstacles { get; set; } = [];
    public long NextTrainAt { get; set; }

    [JsonIgnore]
    public double SpawnTimer { get; set; }
}

/// <summary>Everything the server sends to the clients, as JSON. 20 times a second during a round.</summary>
public class GameState
{
    public long Time { get; set; }
    public bool Playing { get; set; }
    public int TimeLeftMs { get; set; }
    public long RainUntil { get; set; }
    public string? Result { get; set; }
    public List<Player> Players { get; set; } = [];
    public List<Lane> Lanes { get; set; } = [];
}
