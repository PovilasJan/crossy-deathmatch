using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

/// <summary>A player-controlled animal that hops tile by tile.</summary>
public sealed class Runner
{
    private const int BufferedHopMs = 250;

    private (Direction Direction, long At)? _bufferedHop;
    private long _lastHopAt;
    private long _respawnAt;
    private long _invulnerableUntil;
    private long _boostUntil;
    private int _furthestLane;

    public Runner(string id, string name, CharacterKind character, int column, int lane)
    {
        Id = id;
        Name = name;
        Character = character;
        Stats = CharacterCatalog.Get(character);
        X = column;
        Lane = lane;
        Lives = Stats.Lives;
        _furthestLane = lane;
    }

    public string Id { get; }
    public string Name { get; }
    public CharacterKind Character { get; }
    public CharacterStats Stats { get; }

    /// <summary>Left edge in tiles. Fractional while riding a log.</summary>
    public double X { get; set; }
    public int Lane { get; private set; }
    public int Lives { get; private set; }
    public RunnerState State { get; private set; } = RunnerState.Alive;
    public bool Connected { get; private set; } = true;

    public double CenterX => X + 0.5;
    public int Column => Math.Clamp(RoundX(), 0, GameRules.GridWidth - 1);

    public bool IsInvulnerable(long now) => now < _invulnerableUntil;

    public double HopCooldownMs(long now, WorldEffects effects)
    {
        double cooldown = Stats.HopCooldownMs;
        if (effects.IsRaining(now)) cooldown *= GameRules.RainSlowdown;
        if (now < _boostUntil) cooldown *= GameRules.SpeedBoostFactor;
        return cooldown;
    }

    /// <summary>Hops now if the cooldown allows, otherwise remembers the input for a short moment.</summary>
    public void RequestHop(Direction direction, long now, GameMap map, WorldEffects effects)
    {
        if (State != RunnerState.Alive) return;
        if (now - _lastHopAt < HopCooldownMs(now, effects))
        {
            _bufferedHop = (direction, now);
            return;
        }
        Hop(direction, now, map);
    }

    public void Update(long now, GameMap map, WorldEffects effects)
    {
        if (State == RunnerState.Respawning && now >= _respawnAt) Respawn(now, map);
        if (State != RunnerState.Alive || _bufferedHop is not { } buffered) return;

        if (now - buffered.At > BufferedHopMs)
        {
            _bufferedHop = null;
        }
        else if (now - _lastHopAt >= HopCooldownMs(now, effects))
        {
            _bufferedHop = null;
            Hop(buffered.Direction, now, map);
        }
    }

    /// <summary>Loses a life. Returns true when that was the last one.</summary>
    public bool TakeDamage(long now)
    {
        Lives--;
        _bufferedHop = null;
        if (Lives <= 0)
        {
            State = RunnerState.Out;
            return true;
        }
        State = RunnerState.Respawning;
        _respawnAt = now + GameRules.RespawnDelayMs;
        return false;
    }

    public void GrantSpeedBoost(long now) => _boostUntil = now + GameRules.SpeedBoostMs;

    public void AddLife() => Lives = Math.Min(GameRules.MaxLives, Lives + 1);

    public void Leave()
    {
        Connected = false;
        State = RunnerState.Out;
    }

    public RunnerDto ToDto(long now, ScoreEntry score) => new(
        Id, Name, Character,
        (float)Math.Round(X, 2), Lane,
        Lives, score.Score, score.Coins,
        State, IsInvulnerable(now), now < _boostUntil, Connected);

    private void Hop(Direction direction, long now, GameMap map)
    {
        var column = Column;
        var lane = Lane;
        switch (direction)
        {
            case Direction.Up: lane++; break;
            case Direction.Down: lane--; break;
            case Direction.Left: column = RoundX() - 1; break;
            case Direction.Right: column = RoundX() + 1; break;
        }

        if (column is < 0 or >= GameRules.GridWidth) return;
        if (lane < Math.Max(0, _furthestLane - GameRules.BacktrackLimit)) return;
        var target = map.GetLane(lane);
        if (target is null || target.IsBlocked(column)) return;

        X = column;
        Lane = lane;
        _lastHopAt = now;
        _furthestLane = Math.Max(_furthestLane, lane);
    }

    private void Respawn(long now, GameMap map)
    {
        var (column, lane) = map.FindRespawnSpot(Lane, Column);
        X = column;
        Lane = lane;
        State = RunnerState.Alive;
        _invulnerableUntil = now + GameRules.InvulnerableMs;
        _lastHopAt = now;
    }

    private int RoundX() => (int)Math.Round(X, MidpointRounding.AwayFromZero);
}
