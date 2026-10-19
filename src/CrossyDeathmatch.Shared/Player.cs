namespace CrossyDeathmatch.Shared;

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
