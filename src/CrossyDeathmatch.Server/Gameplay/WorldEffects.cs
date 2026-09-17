namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>Map-wide effects the Mastermind can switch on for a while.</summary>
public sealed class WorldEffects
{
    public long RainUntil { get; set; }
    public long NightUntil { get; set; }

    public bool IsRaining(long now) => now < RainUntil;

    public int RainRemainingMs(long now) => (int)Math.Max(0, RainUntil - now);

    public int NightRemainingMs(long now) => (int)Math.Max(0, NightUntil - now);
}
