using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>The fourth player: watches the whole map and spends energy on abilities.</summary>
public sealed class Mastermind(string id, string name)
{
    private readonly Dictionary<AbilityKind, long> _cooldownUntil = [];

    public string Id { get; } = id;
    public string Name { get; } = name;
    public double Energy { get; private set; } = GameRules.MastermindStartEnergy;
    public bool Connected { get; private set; } = true;

    public void Update(double dt) =>
        Energy = Math.Min(GameRules.MastermindMaxEnergy, Energy + GameRules.MastermindEnergyPerSecond * dt);

    public string? CanUse(AbilityInfo ability, long now)
    {
        if (!Connected) return "The Mastermind has left";
        if (_cooldownUntil.TryGetValue(ability.Kind, out var until) && until > now) return $"{ability.Name} is recharging";
        if (Energy < ability.Cost) return $"Not enough energy for {ability.Name} (needs {ability.Cost})";
        return null;
    }

    public void Spend(AbilityInfo ability, long now)
    {
        Energy -= ability.Cost;
        _cooldownUntil[ability.Kind] = now + ability.CooldownMs;
    }

    public void Leave() => Connected = false;

    public MastermindDto ToDto(long now) => new(
        Id,
        Name,
        (float)Math.Round(Energy, 1),
        GameRules.MastermindMaxEnergy,
        AbilityCatalog.All.ToDictionary(
            a => a.Kind,
            a => _cooldownUntil.TryGetValue(a.Kind, out var until) ? (int)Math.Max(0, until - now) : 0),
        Connected);
}
