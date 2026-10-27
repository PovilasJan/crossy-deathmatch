using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public interface ICommand
{
    bool Execute();
    void Undo();
}

public class MoveCommand(Player receiver, int dx, int dy, long now) : ICommand
{
    private readonly Player _receiver = receiver;
    private readonly int _dx = dx;
    private readonly int _dy = dy;
    private readonly long _now = now;
    private int _prevX;
    private int _prevLane;
    private long _prevLastHopAt;
    private int _prevScore;
    private int _prevMaxLane;

    public bool Execute()
    {
        _prevX = _receiver.X;
        _prevLane = _receiver.Lane;
        _prevLastHopAt = _receiver.LastHopAt;
        _prevScore = _receiver.Score;
        _prevMaxLane = _receiver.MaxLane;
        _receiver.Hop(_dx, _dy, _now);
        return true;
    }

    public void Undo()
    {
        _receiver.X = _prevX;
        _receiver.Lane = _prevLane;
        _receiver.LastHopAt = _prevLastHopAt;
        _receiver.Score = _prevScore;
        _receiver.MaxLane = _prevMaxLane;
    }
}

public class UseAbilityCommand(Ability ability, IAbilityStrategy receiver, GameState state, Lane? lane, int column, long now) : ICommand
{
    private readonly IAbilityStrategy _receiver = receiver;
    private readonly GameState _state = state;
    private readonly Lane? _lane = lane;
    private readonly int _column = column;
    private readonly long _now = now;
    private long _prevRainUntil;
    private List<Obstacle> _added = [];

    public Ability Ability { get; } = ability;

    public bool Execute()
    {
        _prevRainUntil = _state.RainUntil;
        var before = _lane?.Obstacles.ToList() ?? [];
        if (!_receiver.Use(_state, _lane, _column, _now)) return false;
        _added = _lane?.Obstacles.Except(before).ToList() ?? [];
        return true;
    }

    public void Undo()
    {
        foreach (var obstacle in _added) _lane?.Obstacles.Remove(obstacle);
        _state.RainUntil = _prevRainUntil;
    }
}

public class Invoker
{
    private readonly List<ICommand> _history = [];

    public bool Run(ICommand command)
    {
        if (!command.Execute()) return false;
        _history.Add(command);
        return true;
    }

    public ICommand? Undo()
    {
        if (_history.Count == 0) return null;
        var command = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        command.Undo();
        return command;
    }

    public void Clear() => _history.Clear();
}
