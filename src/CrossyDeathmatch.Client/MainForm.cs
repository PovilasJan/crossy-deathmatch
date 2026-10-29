using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>
/// The client window: the connect/lobby controls, and during a round the game screen. What the screen shows
/// depends on the player's role (<see cref="RunnerView"/> or <see cref="MastermindView"/>); how it is drawn depends
/// on the renderer, which F3 switches. It reaches the server only through <see cref="IGameServer"/>.
/// </summary>
public class MainForm : Form
{
    private readonly TextBox _serverBox;
    private readonly TextBox _nameBox;
    private readonly Button _connectButton;
    private readonly ComboBox _animalBox;
    private readonly Button _readyButton;
    private readonly ListBox _playerList;
    private readonly Label _infoLabel;
    private readonly Panel _lobby;
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 15 };
    private readonly List<(string Text, long At)> _messages = [];
    private readonly IGameServer _server = new SignalRGameServer();
    private readonly RunnerView _runnerView;
    private readonly MastermindView _mastermindView;

    private GameState? _state;
    private long _stateReceivedAt;
    private string _myName = "";
    private GameView _view;
    private bool _wireframe;

    /// <summary>Optional arguments: <c>--server http://192.168.1.20:5000 --name Povilas</c>.</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        string? Arg(string key)
        {
            var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(Arg("--server") ?? "http://localhost:5000", Arg("--name") ?? ""));
    }

    public MainForm(string server, string name)
    {
        Text = "Crossy Deathmatch";
        ClientSize = new Size(1100, 750);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(238, 243, 230);

        _serverBox = new TextBox { Text = server, Width = 260 };
        _nameBox = new TextBox { Text = name, Width = 160, MaxLength = Rules.MaxNameLength };
        _connectButton = new Button { Text = "Connect", AutoSize = true };
        _animalBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Enabled = false };
        _animalBox.Items.AddRange(Enum.GetValues<Animal>().Cast<object>().ToArray());
        _readyButton = new Button { Text = "Ready", AutoSize = true, Enabled = false };
        _playerList = new ListBox { Width = 420, Height = 100 };
        _infoLabel = new Label { AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(0, 12, 0, 0) };

        _lobby = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24),
        };
        _lobby.Controls.Add(new Label { Text = "Crossy Deathmatch", Font = new Font("Segoe UI", 22f, FontStyle.Bold), AutoSize = true });
        _lobby.Controls.Add(Caption("Three runners hop across roads and railways. One Mastermind tries to stop them."));
        _lobby.Controls.Add(Row(Caption("Server"), _serverBox, Caption("Nickname"), _nameBox, _connectButton));
        _lobby.Controls.Add(Row(Caption("Animal"), _animalBox, _readyButton));
        _lobby.Controls.Add(_playerList);
        _lobby.Controls.Add(_infoLabel);
        Controls.Add(_lobby);

        _runnerView = new RunnerView(_server, _messages);
        _mastermindView = new MastermindView(_server, _messages);
        _view = _runnerView;

        _server.StateReceived += state => OnUi(() => OnState(state));
        _server.MessageReceived += text => OnUi(() => AddMessage(text));
        _server.Disconnected += () => OnUi(OnDisconnected);

        _connectButton.Click += async (_, _) => await Connect();
        _animalBox.SelectionChangeCommitted += (_, _) => _server.PickAnimal((Animal)_animalBox.SelectedItem!);
        _readyButton.Click += (_, _) => _server.SetReady(!(Me?.Ready ?? false));
        _frameTimer.Tick += (_, _) => Invalidate();
    }

    private Player? Me => _state?.Players.FirstOrDefault(p => p.Name == _myName);

    // ---- connection ----

    private async Task Connect()
    {
        var name = _nameBox.Text.Trim();
        if (name.Length == 0)
        {
            _infoLabel.Text = "Pick a nickname first.";
            return;
        }

        _connectButton.Enabled = false;
        try
        {
            await _server.ConnectAsync(_serverBox.Text);
            _myName = name;
            await _server.JoinAsync(name);
        }
        catch (Exception ex)
        {
            _infoLabel.Text = $"Could not connect: {ex.Message}";
        }
        finally
        {
            _connectButton.Enabled = Me == null;
        }
    }

    /// <summary>The server's events arrive on a background thread; controls may only be touched on the UI thread.</summary>
    private void OnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window is closing.
        }
    }

    private void OnState(GameState state)
    {
        _state = state;
        _stateReceivedAt = Environment.TickCount64;

        var me = Me;
        var playing = _state is { Playing: true } && me != null;
        if (playing == _lobby.Visible)
        {
            _lobby.Visible = !playing;
            if (playing)
            {
                // A player's role is fixed for the whole round, so the view is picked once, as it starts.
                _view = me!.IsMastermind ? _mastermindView : _runnerView;
                _view.Reset();
                ActiveControl = null;
                _frameTimer.Start();
            }
            else
            {
                _frameTimer.Stop();
            }
        }

        _serverBox.Enabled = _nameBox.Enabled = _connectButton.Enabled = me == null;
        _animalBox.Enabled = _readyButton.Enabled = me != null;
        if (me != null && !_animalBox.DroppedDown) _animalBox.SelectedItem = me.Animal;
        _readyButton.Text = me?.Ready == true ? "Not ready" : "Ready";

        _playerList.BeginUpdate();
        _playerList.Items.Clear();
        foreach (var player in _state!.Players)
        {
            _playerList.Items.Add($"{player.Name}  -  {player.Animal}{(player.Ready ? "  -  ready" : "")}{(player == me ? "  (you)" : "")}");
        }
        _playerList.EndUpdate();
        UpdateInfo();
    }

    private void OnDisconnected()
    {
        _state = null;
        _frameTimer.Stop();
        _lobby.Visible = true;
        _serverBox.Enabled = _nameBox.Enabled = _connectButton.Enabled = true;
        _animalBox.Enabled = _readyButton.Enabled = false;
        _playerList.Items.Clear();
        AddMessage("Disconnected from the server.");
    }

    private void AddMessage(string text)
    {
        _messages.Add((text, Environment.TickCount64));
        if (_messages.Count > 20) _messages.RemoveAt(0);
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        var lines = new List<string>();
        if (_state is { Playing: false })
        {
            lines.Add("When everyone is ready, the round starts and one random player becomes the Mastermind.");
        }
        if (_state?.Result is { } result) lines.Add("\nLast round: " + result);
        lines.Add("");
        lines.AddRange(_messages.TakeLast(6).Select(m => m.Text));
        _infoLabel.Text = string.Join("\n", lines);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _frameTimer.Stop();
        _ = _server.DisposeAsync();
        base.OnFormClosing(e);
    }

    // ---- the game screen: input goes to the view, which draws through the renderer ----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_lobby.Visible) return base.ProcessCmdKey(ref msg, keyData);

        if (keyData == Keys.F3) _wireframe = !_wireframe;
        else if (!_view.HandleKey(keyData)) return base.ProcessCmdKey(ref msg, keyData);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _view.HandleMouseMove(e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_lobby.Visible) _view.HandleMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_lobby.Visible || _state is not { Playing: true } state || Me is not { } me) return;

        var sinceState = Environment.TickCount64 - _stateReceivedAt;
        var now = state.Time + sinceState; // estimated server clock
        var elapsed = Math.Min(0.15, sinceState / 1000.0);

        _view.Renderer = _wireframe ? new WireframeRenderer(e.Graphics) : new GdiRenderer(e.Graphics);
        _view.Draw(state, me, now, elapsed, ClientSize);
    }

    // ---- small helpers ----

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 7, 6, 0) };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 4) };
        row.Controls.AddRange(controls);
        return row;
    }
}
