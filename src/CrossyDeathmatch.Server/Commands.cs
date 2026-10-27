using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Server;

public interface ICommand
{
    bool Execute();
}

public class MoveCommand(Player receiver, int dx, int dy, long now) : ICommand
{
    private readonly Player _receiver = receiver;
    private readonly int _dx = dx;
    private readonly int _dy = dy;
    private readonly long _now = now;

    public bool Execute()
    {
        _receiver.Hop(_dx, _dy, _now);
        return true;
    }
}

public class UseAbilityCommand(IAbilityStrategy receiver, GameState state, Lane? lane, int column, long now) : ICommand
{
    private readonly IAbilityStrategy _receiver = receiver;
    private readonly GameState _state = state;
    private readonly Lane? _lane = lane;
    private readonly int _column = column;
    private readonly long _now = now;

    public bool Execute() => _receiver.Use(_state, _lane, _column, _now);
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

    public void Clear() => _history.Clear();
}
