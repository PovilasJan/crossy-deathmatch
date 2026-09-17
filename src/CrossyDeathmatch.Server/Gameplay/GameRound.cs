using CrossyDeathmatch.Server.Gameplay.Abilities;
using CrossyDeathmatch.Server.Gameplay.Obstacles;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server.Gameplay;

public sealed record RoundParticipant(string Id, string Name, CharacterKind Character);

/// <summary>How a round reports back to whoever hosts it.</summary>
public interface IRoundEvents
{
    void OnSnapshot(SnapshotDto snapshot);
    void OnAnnouncement(string text, MessageTone tone);
    void OnFinished(GameResultDto result);
}

/// <summary>
/// One 3-minute round: the authoritative simulation. The owner calls <see cref="Tick"/> at
/// <see cref="GameRules.TickRate"/> Hz and forwards player input; the round never touches the network.
/// </summary>
public sealed class GameRound : IAbilityContext
{
    private const int SpawnLane = 1;
    private const int LanesGeneratedAhead = 40;
    private const int LanesKeptBehind = 20;
    private static readonly int[] SpawnColumns = [6, 3, 9, 1];

    private readonly Dictionary<string, Runner> _runners = [];
    private readonly Mastermind? _mastermind;
    private readonly Dictionary<AbilityKind, Ability> _abilities;
    private readonly CollisionResolver _collisions = new();
    private readonly ScoreBoard _scoreBoard = new();
    private readonly IRoundEvents _events;
    private readonly int _durationMs;
    private readonly long _startedAt;
    private long _lastTickAt;
    private bool _finished;

    public GameRound(IReadOnlyList<RoundParticipant> runners, RoundParticipant? mastermind, int durationMs, IRoundEvents events, long now)
    {
        _events = events;
        _durationMs = durationMs;
        _startedAt = now;
        _lastTickAt = now;

        for (var i = 0; i < runners.Count; i++)
        {
            var participant = runners[i];
            _runners[participant.Id] = new Runner(participant.Id, participant.Name, participant.Character, SpawnColumns[i % SpawnColumns.Length], SpawnLane);
            _scoreBoard.Register(participant.Id, participant.Name, participant.Character, SpawnLane);
        }
        _mastermind = mastermind is null ? null : new Mastermind(mastermind.Id, mastermind.Name);

        Ability[] abilities = [new SpawnCarAbility(), new DropBoulderAbility(), new SpawnFoxAbility(), new RainAbility(), new NightAbility()];
        _abilities = abilities.ToDictionary(a => a.Info.Kind);

        Map.GenerateUntil(LanesGeneratedAhead);
    }

    public GameMap Map { get; } = new();
    public WorldEffects Effects { get; } = new();
    public bool IsFinished => _finished;

    public void Tick(long now)
    {
        if (_finished) return;
        var dt = Math.Min(0.1, (now - _lastTickAt) / 1000.0);
        _lastTickAt = now;

        // Remember who stands on which log before the logs move, then carry them along.
        var riders = new List<(Runner Runner, Obstacle Log)>();
        foreach (var runner in _runners.Values)
        {
            if (runner.State == RunnerState.Alive &&
                Map.GetLane(runner.Lane) is { Type: LaneType.River } river &&
                river.FindLogUnder(runner.CenterX) is { } log)
            {
                riders.Add((runner, log));
            }
        }

        Map.Update(dt, now);
        foreach (var (runner, log) in riders) runner.X += log.Velocity * dt;

        foreach (var runner in _runners.Values)
        {
            runner.Update(now, Map, Effects);
            if (runner.State != RunnerState.Alive) continue;

            _scoreBoard.RecordProgress(runner.Id, runner.Lane);
            if (_collisions.FindHazard(runner, Map, now) is { } cause)
            {
                Kill(runner, cause, now);
                continue;
            }
            CollectPickup(runner, now);
        }

        _mastermind?.Update(dt);
        MaintainMap();

        if (CheckForEnd(now)) return;
        _events.OnSnapshot(CreateSnapshot(now));
    }

    public void Move(string playerId, Direction direction, long now)
    {
        if (_finished || !_runners.TryGetValue(playerId, out var runner)) return;
        runner.RequestHop(direction, now, Map, Effects);
    }

    public string? UseAbility(string playerId, AbilityKind kind, int? lane, int? column, long now)
    {
        if (_finished) return "The round is over";
        if (_mastermind is null || _mastermind.Id != playerId) return "Only the Mastermind can use abilities";
        if (!_abilities.TryGetValue(kind, out var ability)) return "Unknown ability";

        var blocked = _mastermind.CanUse(ability.Info, now) ?? ability.Use(this, lane, column, now);
        if (blocked is not null) return blocked;

        _mastermind.Spend(ability.Info, now);
        _events.OnAnnouncement($"Mastermind used {ability.Info.Name}!", MessageTone.Bad);
        return null;
    }

    public void RemovePlayer(string playerId)
    {
        if (_runners.TryGetValue(playerId, out var runner))
        {
            runner.Leave();
            _events.OnAnnouncement($"{runner.Name} left the round", MessageTone.Info);
        }
        else if (_mastermind?.Id == playerId)
        {
            _mastermind.Leave();
            _events.OnAnnouncement("The Mastermind left. Runners, keep going!", MessageTone.Good);
        }
    }

    private void Kill(Runner runner, string cause, long now)
    {
        var eliminated = runner.TakeDamage(now);
        var suffix = eliminated ? " and is OUT!" : $" ({runner.Lives} {(runner.Lives == 1 ? "life" : "lives")} left)";
        _events.OnAnnouncement($"{runner.Name} {cause}{suffix}", MessageTone.Bad);
    }

    private void CollectPickup(Runner runner, long now)
    {
        var lane = Map.GetLane(runner.Lane);
        if (lane is null || lane.Type is LaneType.River or LaneType.Rail) return;

        switch (lane.TakePickup(runner.Column))
        {
            case PickupKind.Coin:
                _scoreBoard.AddCoin(runner.Id);
                break;
            case PickupKind.Speed:
                runner.GrantSpeedBoost(now);
                _events.OnAnnouncement($"{runner.Name} grabbed a speed boost", MessageTone.Good);
                break;
            case PickupKind.Life:
                runner.AddLife();
                _events.OnAnnouncement($"{runner.Name} found an extra life", MessageTone.Good);
                break;
        }
    }

    private void MaintainMap()
    {
        var active = _runners.Values.Where(r => r.State != RunnerState.Out).ToList();
        if (active.Count == 0) return;
        Map.GenerateUntil(active.Max(r => r.Lane) + LanesGeneratedAhead);
        Map.PruneBelow(active.Min(r => r.Lane) - LanesKeptBehind);
    }

    private bool CheckForEnd(long now)
    {
        var everyoneOut = _runners.Values.All(r => r.State == RunnerState.Out);
        var timeUp = now - _startedAt >= _durationMs;
        if (!everyoneOut && !timeUp) return false;

        var ranking = _scoreBoard.Rank(_runners.Values);
        GameResultDto result;
        if (everyoneOut && _mastermind is not null)
        {
            result = new GameResultDto(PlayerRole.Mastermind, _mastermind.Id, _mastermind.Name,
                "The Mastermind eliminated every runner", ranking, _mastermind.Name);
        }
        else
        {
            var best = ranking[0];
            var reason = timeUp ? "Time is up, so the best surviving runner wins" : "Every runner is out, so the best score wins";
            result = new GameResultDto(PlayerRole.Runner, best.Id, best.Name, reason, ranking, _mastermind?.Name);
        }

        _finished = true;
        _events.OnSnapshot(CreateSnapshot(now));
        _events.OnFinished(result);
        return true;
    }

    private SnapshotDto CreateSnapshot(long now)
    {
        var tracked = _runners.Values.Where(r => r.State != RunnerState.Out).ToList();
        if (tracked.Count == 0) tracked = [.. _runners.Values];

        var lanes = Map.LanesBetween(tracked.Min(r => r.Lane) - 8, tracked.Max(r => r.Lane) + 18).ToList();
        return new SnapshotDto(
            ServerTimeMs: now,
            TimeLeftMs: (int)Math.Max(0, _durationMs - (now - _startedAt)),
            Lanes: lanes.Select(l => l.ToDto()).ToList(),
            Obstacles: lanes.SelectMany(l => l.Obstacles.Select(o => o.ToDto(l.Index))).ToList(),
            Runners: _runners.Values.Select(r => r.ToDto(now, _scoreBoard.Get(r.Id))).ToList(),
            Mastermind: _mastermind?.ToDto(now),
            RainMs: Effects.RainRemainingMs(now),
            NightMs: Effects.NightRemainingMs(now));
    }
}
