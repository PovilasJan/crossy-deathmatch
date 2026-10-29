using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>
/// Facade: the one object the window's parts talk to. Behind it are the server connection
/// (<see cref="SignalRGameServer"/>), the two role views (<see cref="RunnerView"/>, <see cref="MastermindView"/>) and
/// the two renderers (<see cref="GdiRenderer"/>, <see cref="WireframeRenderer"/>). The lobby joins, picks an animal
/// and readies up; the game screen passes input and asks for a frame. Which view and renderer do the work, and how
/// the server's clock is estimated between updates, stays in here.
/// </summary>
public class GameClient : IAsyncDisposable
{
    private readonly IGameServer _server = new SignalRGameServer();
    private readonly List<(string Text, long At)> _messages = [];
    private readonly RunnerView _runnerView;
    private readonly MastermindView _mastermindView;

    private long _stateReceivedAt;
    private string _myName = "";
    private GameView _view;
    private bool _wireframe;

    /// <param name="onUi">
    /// Runs an action on the UI thread. The server's events arrive on a background thread; they are handed over
    /// through this, so the state, the messages and this class's events only ever change on the UI thread.
    /// </param>
    public GameClient(Action<Action> onUi)
    {
        _runnerView = new RunnerView(_server, _messages);
        _mastermindView = new MastermindView(_server, _messages);
        _view = _runnerView;

        _server.StateReceived += state => onUi(() => OnState(state));
        _server.MessageReceived += text => onUi(() => AddMessage(text));
        _server.Disconnected += () => onUi(OnDisconnected);
    }

    /// <summary>A new state has arrived, or the connection was lost and <see cref="State"/> is null.</summary>
    public event Action? StateChanged;

    /// <summary>A message was added to <see cref="Messages"/>.</summary>
    public event Action? MessagesChanged;

    public GameState? State { get; private set; }

    public Player? Me => State?.Players.FirstOrDefault(p => p.Name == _myName);

    /// <summary>A round is running and this player is in it.</summary>
    public bool Playing => State is { Playing: true } && Me != null;

    /// <summary>The latest server messages, oldest first, with when each arrived.</summary>
    public IReadOnlyList<(string Text, long At)> Messages => _messages;

    // ---- the lobby ----

    /// <summary>Connects to the server (dropping any earlier connection) and joins its lobby.</summary>
    public async Task JoinAsync(string url, string name)
    {
        await _server.ConnectAsync(url);
        _myName = name;
        await _server.JoinAsync(name);
    }

    public void PickAnimal(Animal animal) => _server.PickAnimal(animal);

    public void ToggleReady() => _server.SetReady(!(Me?.Ready ?? false));

    // ---- the game screen ----

    /// <summary>Switches between the normal look and the wireframe debug look.</summary>
    public void ToggleWireframe() => _wireframe = !_wireframe;

    /// <summary>Returns false for keys the player's role doesn't use.</summary>
    public bool HandleKey(Keys key) => _view.HandleKey(key);

    public void HandleMouseMove(Point location) => _view.HandleMouseMove(location);

    public void HandleMouseDown(MouseEventArgs e) => _view.HandleMouseDown(e);

    /// <summary>Draws one frame of the game screen. Does nothing unless <see cref="Playing"/>.</summary>
    public void Draw(Graphics graphics, Size screen)
    {
        if (State is not { Playing: true } state || Me is not { } me) return;

        var sinceState = Environment.TickCount64 - _stateReceivedAt;
        var now = state.Time + sinceState; // estimated server clock
        var elapsed = Math.Min(0.15, sinceState / 1000.0);

        // A renderer wraps one frame's Graphics, so a new one is made for every frame.
        _view.Renderer = _wireframe ? new WireframeRenderer(graphics) : new GdiRenderer(graphics);
        _view.Draw(state, me, now, elapsed, screen);
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    // ---- from the server ----

    private void OnState(GameState state)
    {
        var wasPlaying = Playing;
        State = state;
        _stateReceivedAt = Environment.TickCount64;

        if (Playing && !wasPlaying)
        {
            // A player's role is fixed for the whole round, so the view is picked once, as it starts.
            _view = Me!.IsMastermind ? _mastermindView : _runnerView;
            _view.Reset();
        }
        StateChanged?.Invoke();
    }

    private void OnDisconnected()
    {
        State = null;
        StateChanged?.Invoke();
        AddMessage("Disconnected from the server.");
    }

    private void AddMessage(string text)
    {
        _messages.Add((text, Environment.TickCount64));
        if (_messages.Count > 20) _messages.RemoveAt(0);
        MessagesChanged?.Invoke();
    }
}
