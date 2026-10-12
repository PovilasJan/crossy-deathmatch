using System.Drawing.Drawing2D;
using System.Text.Json;
using CrossyDeathmatch.Shared;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrossyDeathmatch.Client;

/// <summary>
/// The whole client: the connect/lobby controls, the SignalR connection, and the game screen, which
/// is drawn with System.Drawing straight onto the form.
/// </summary>
public class MainForm : Form
{
    private static readonly Color GrassLight = Color.FromArgb(155, 215, 112);
    private static readonly Color GrassDark = Color.FromArgb(143, 204, 100);
    private static readonly Color Road = Color.FromArgb(87, 92, 100);
    private static readonly Color Rail = Color.FromArgb(140, 124, 107);
    private static readonly Color Sleeper = Color.FromArgb(93, 74, 59);
    private static readonly Color Steel = Color.FromArgb(207, 211, 216);
    private static readonly Color HudPanel = Color.FromArgb(200, 22, 26, 33);
    private static readonly Color Accent = Color.FromArgb(255, 212, 59);
    private static readonly Color Danger = Color.FromArgb(224, 49, 49);
    private static readonly Color[] CarColors =
    [
        Color.FromArgb(229, 72, 77), Color.FromArgb(18, 165, 148), Color.FromArgb(247, 107, 21),
        Color.FromArgb(110, 86, 207), Color.FromArgb(0, 144, 255),
    ];

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

    private HubConnection? _hub;
    private GameState? _state;
    private long _stateReceivedAt;
    private string _myName = "";
    private Ability? _selected;
    private Point _mouse;
    private float _cameraBottom = float.NaN;
    private float _tile = 40;
    private float _left;

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

        _connectButton.Click += async (_, _) => await Connect();
        _animalBox.SelectionChangeCommitted += (_, _) => Send("PickAnimal", _animalBox.SelectedItem);
        _readyButton.Click += (_, _) => Send("SetReady", !(Me?.Ready ?? false));
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
            if (_hub != null) await _hub.DisposeAsync();

            var hub = new HubConnectionBuilder().WithUrl(_serverBox.Text.Trim().TrimEnd('/') + "/game").Build();
            hub.On<string>("State", json => OnUi(() => OnState(json)));
            hub.On<string>("Message", text => OnUi(() => AddMessage(text)));
            hub.Closed += _ =>
            {
                OnUi(() =>
                {
                    if (_hub == hub) OnDisconnected();
                });
                return Task.CompletedTask;
            };
            _hub = hub;

            await hub.StartAsync();
            _myName = name;
            await hub.SendAsync("Join", name);
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

    private void Send(string method, params object?[] args)
    {
        if (_hub?.State == HubConnectionState.Connected) _ = _hub.SendCoreAsync(method, args);
    }

    /// <summary>SignalR calls back on a background thread; controls may only be touched on the UI thread.</summary>
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

    private void OnState(string json)
    {
        _state = JsonSerializer.Deserialize<GameState>(json);
        _stateReceivedAt = Environment.TickCount64;

        var me = Me;
        var playing = _state is { Playing: true } && me != null;
        if (playing == _lobby.Visible)
        {
            _lobby.Visible = !playing;
            if (playing)
            {
                _selected = null;
                _cameraBottom = float.NaN;
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
        _ = _hub?.DisposeAsync();
        base.OnFormClosing(e);
    }

    // ---- input ----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var me = Me;
        if (_lobby.Visible || me == null) return base.ProcessCmdKey(ref msg, keyData);

        if (me.IsMastermind)
        {
            if (keyData == Keys.Escape) _selected = null;
            else if (keyData is >= Keys.D1 and <= Keys.D4) Choose((Ability)(keyData - Keys.D1));
            else return base.ProcessCmdKey(ref msg, keyData);
            return true;
        }

        var (dx, dy) = keyData switch
        {
            Keys.W or Keys.Up => (0, 1),
            Keys.S or Keys.Down => (0, -1),
            Keys.A or Keys.Left => (-1, 0),
            Keys.D or Keys.Right => (1, 0),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0) return base.ProcessCmdKey(ref msg, keyData);
        Send("Move", dx, dy);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_lobby.Visible || Me is not { IsMastermind: true }) return;

        if (e.Button == MouseButtons.Right)
        {
            _selected = null;
            return;
        }

        foreach (var ability in Enum.GetValues<Ability>())
        {
            if (!AbilityButton(ability).Contains(e.Location)) continue;
            Choose(ability);
            return;
        }

        if (_selected is { } selected)
        {
            var (lane, column) = ScreenToWorld(e.Location);
            Send("UseAbility", selected, lane, column);
        }
    }

    /// <summary>Rain needs no target, so it fires at once; the others wait for a click on the map.</summary>
    private void Choose(Ability ability)
    {
        if (ability == Ability.Rain)
        {
            _selected = null;
            Send("UseAbility", ability, 0, 0);
            return;
        }
        _selected = _selected == ability ? null : ability;
    }

    // ---- drawing ----

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var state = _state;
        if (_lobby.Visible || state is not { Playing: true }) return;

        var g = e.Graphics;
        var me = Me;
        var sinceState = Environment.TickCount64 - _stateReceivedAt;
        var now = state.Time + sinceState; // estimated server clock
        var elapsed = Math.Min(0.15, sinceState / 1000.0);

        UpdateCamera(state, me);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(GrassDark);

        foreach (var lane in state.Lanes) DrawLane(g, lane, elapsed, now);
        var runners = state.Players.Where(p => !p.IsMastermind && !p.Out).OrderByDescending(p => p.Lane).ToList();
        foreach (var runner in runners) DrawRunner(g, runner, now);

        // Shade everything outside the 13 playable columns.
        Fill(g, Color.FromArgb(75, 0, 0, 0), new RectangleF(0, 0, Math.Max(0, _left), ClientSize.Height));
        Fill(g, Color.FromArgb(75, 0, 0, 0), new RectangleF(_left + Rules.GridWidth * _tile, 0, ClientSize.Width, ClientSize.Height));

        foreach (var runner in runners) DrawNameTag(g, runner, runner.Name == _myName);
        if (me is { IsMastermind: true }) DrawTargeting(g, state);
        if (now < state.RainUntil) DrawRain(g);
        DrawHud(g, state, me, now);
    }

    private void UpdateCamera(GameState state, Player? me)
    {
        float width = ClientSize.Width, height = ClientSize.Height;
        var widthTile = width / (Rules.GridWidth + 2);
        float targetBottom, targetTile;

        if (me is { IsMastermind: false, Out: false })
        {
            // A runner sees a close-up that follows them.
            targetBottom = me.Lane - 3;
            targetTile = Math.Min(widthTile, height / 11);
        }
        else
        {
            // The Mastermind (and anyone who is out) sees every runner still in the game.
            var runners = state.Players.Where(p => !p.IsMastermind).ToList();
            var active = runners.Where(r => !r.Out).ToList();
            if (active.Count == 0) active = runners;
            var lowest = active.Count > 0 ? active.Min(r => r.Lane) : 0;
            var highest = active.Count > 0 ? active.Max(r => r.Lane) : 0;
            targetBottom = lowest - 3;
            targetTile = Math.Min(widthTile, height / Math.Max(13, highest + 9 - targetBottom));
        }

        if (float.IsNaN(_cameraBottom))
        {
            _cameraBottom = targetBottom;
            _tile = targetTile;
        }
        else
        {
            _cameraBottom += (targetBottom - _cameraBottom) * 0.12f;
            _tile += (targetTile - _tile) * 0.12f;
        }
        _left = (width - Rules.GridWidth * _tile) / 2;
    }

    private float LaneTop(float lane) => ClientSize.Height - (lane - _cameraBottom + 1) * _tile;

    private RectangleF Cell(double x, float lane, double width = 1) =>
        new(_left + (float)x * _tile, LaneTop(lane), (float)width * _tile, _tile);

    private (int Lane, int Column) ScreenToWorld(Point point) => (
        (int)MathF.Floor(_cameraBottom + (ClientSize.Height - point.Y) / _tile),
        (int)MathF.Floor((point.X - _left) / _tile));

    private void DrawLane(Graphics g, Lane lane, double elapsed, long now)
    {
        var t = _tile;
        var top = LaneTop(lane.Index);
        var row = new RectangleF(0, top, ClientSize.Width, t + 1);

        switch (lane.Type)
        {
            case LaneType.Grass:
                Fill(g, lane.Index % 2 == 0 ? GrassLight : GrassDark, row);
                break;
            case LaneType.Road:
                Fill(g, Road, row);
                break;
            case LaneType.Rail:
                Fill(g, Rail, row);
                for (var column = -4; column < Rules.GridWidth + 4; column++)
                {
                    Fill(g, Sleeper, new RectangleF(_left + (column + 0.15f) * t, top + t * 0.14f, t * 0.16f, t * 0.72f));
                    Fill(g, Sleeper, new RectangleF(_left + (column + 0.65f) * t, top + t * 0.14f, t * 0.16f, t * 0.72f));
                }
                Fill(g, Steel, new RectangleF(0, top + t * 0.26f, ClientSize.Width, t * 0.07f));
                Fill(g, Steel, new RectangleF(0, top + t * 0.66f, ClientSize.Width, t * 0.07f));
                break;
        }

        foreach (var column in lane.Trees)
        {
            var cell = Cell(column, lane.Index);
            Fill(g, Color.FromArgb(122, 82, 48), new RectangleF(cell.X + t * 0.42f, cell.Y + t * 0.55f, t * 0.16f, t * 0.3f));
            Fill(g, Color.FromArgb(47, 138, 62), new RectangleF(cell.X + t * 0.15f, cell.Y + t * 0.05f, t * 0.7f, t * 0.55f));
        }

        foreach (var column in lane.Coins)
        {
            var cell = Cell(column, lane.Index);
            FillEllipse(g, Color.FromArgb(217, 154, 0), new RectangleF(cell.X + t * 0.28f, cell.Y + t * 0.28f, t * 0.44f, t * 0.44f));
            FillEllipse(g, Color.FromArgb(255, 203, 47), new RectangleF(cell.X + t * 0.33f, cell.Y + t * 0.33f, t * 0.34f, t * 0.34f));
        }

        foreach (var obstacle in lane.Obstacles)
        {
            // The server sends 20 updates a second; move things on by their speed so they glide between updates.
            var x = obstacle.X + (obstacle.Kind == ObstacleKind.Boulder ? 0 : obstacle.Speed * elapsed);
            DrawObstacle(g, obstacle, Cell(x, lane.Index, obstacle.Length), CarColors[Math.Abs(lane.Index) % CarColors.Length], now);
        }

        if (lane.Type == LaneType.Rail)
        {
            var warning = lane.Obstacles.Any(o => o.Kind == ObstacleKind.Train) || now >= lane.NextTrainAt - Rules.TrainWarningMs;
            var lit = warning && Environment.TickCount64 / 180 % 2 == 0;
            FillEllipse(g, Color.FromArgb(31, 35, 40), new RectangleF(_left - t * 0.5f, top + t * 0.1f, t * 0.4f, t * 0.4f));
            FillEllipse(g, lit ? Danger : Color.FromArgb(92, 26, 26), new RectangleF(_left - t * 0.44f, top + t * 0.16f, t * 0.28f, t * 0.28f));
        }
    }

    private void DrawObstacle(Graphics g, Obstacle obstacle, RectangleF area, Color carColor, long now)
    {
        var t = _tile;
        switch (obstacle.Kind)
        {
            case ObstacleKind.Car:
            case ObstacleKind.Truck:
                var body = new RectangleF(area.X + t * 0.06f, area.Y + t * 0.18f, area.Width - t * 0.12f, t * 0.62f);
                Fill(g, Color.FromArgb(31, 35, 40), new RectangleF(body.X + t * 0.1f, body.Y - t * 0.06f, body.Width - t * 0.2f, body.Height + t * 0.12f));
                Fill(g, obstacle.Kind == ObstacleKind.Truck ? Color.FromArgb(233, 236, 239) : carColor, body);
                var cabWidth = obstacle.Kind == ObstacleKind.Truck ? t * 0.7f : body.Width;
                var cabX = obstacle.Speed >= 0 ? body.Right - cabWidth : body.X;
                Fill(g, carColor, new RectangleF(cabX, body.Y, cabWidth, body.Height));
                Fill(g, Color.FromArgb(208, 235, 255), new RectangleF(cabX + cabWidth * 0.3f, body.Y + body.Height * 0.15f, cabWidth * 0.4f, body.Height * 0.7f));
                break;

            case ObstacleKind.Train:
                Fill(g, Color.FromArgb(241, 243, 245), new RectangleF(area.X, area.Y + t * 0.1f, area.Width, t * 0.8f));
                Fill(g, Color.FromArgb(25, 113, 194), new RectangleF(area.X, area.Y + t * 0.42f, area.Width, t * 0.14f));
                var engineX = obstacle.Speed >= 0 ? area.Right - 3 * t : area.X;
                Fill(g, Color.FromArgb(201, 42, 42), new RectangleF(engineX, area.Y + t * 0.1f, 3 * t, t * 0.8f));
                break;

            case ObstacleKind.Fox:
                var orange = Color.FromArgb(247, 103, 7);
                Fill(g, orange, new RectangleF(area.X + t * 0.15f, area.Y + t * 0.3f, t * 0.7f, t * 0.42f));
                var headX = obstacle.Speed >= 0 ? area.X + t * 0.6f : area.X + t * 0.08f;
                Fill(g, orange, new RectangleF(headX, area.Y + t * 0.18f, t * 0.32f, t * 0.34f));
                Fill(g, Color.White, new RectangleF(headX, area.Y + t * 0.4f, t * 0.32f, t * 0.12f));
                break;

            case ObstacleKind.Boulder when !obstacle.Landed:
                // Still falling: show where it will land.
                var pulse = (int)(100 * (MathF.Sin(now / 80f) + 1) / 2);
                using (var pen = new Pen(Color.FromArgb(150 + pulse, Danger), t * 0.07f))
                {
                    g.DrawEllipse(pen, area.X + t * 0.12f, area.Y + t * 0.12f, t * 0.76f, t * 0.76f);
                }
                break;

            case ObstacleKind.Boulder:
                FillEllipse(g, Color.FromArgb(138, 144, 151), new RectangleF(area.X + t * 0.08f, area.Y + t * 0.1f, t * 0.84f, t * 0.74f));
                FillEllipse(g, Color.FromArgb(177, 182, 188), new RectangleF(area.X + t * 0.22f, area.Y + t * 0.16f, t * 0.34f, t * 0.22f));
                break;
        }
    }

    private void DrawRunner(Graphics g, Player runner, long now)
    {
        var respawning = runner.RespawnAt > 0;
        var blinking = now < runner.InvulnerableUntil && Environment.TickCount64 / 120 % 2 == 0;
        var alpha = respawning ? 90 : blinking ? 120 : 255;
        DrawAnimal(g, runner.Animal, Cell(runner.X, runner.Lane), alpha);
    }

    private static void DrawAnimal(Graphics g, Animal animal, RectangleF cell, int alpha = 255)
    {
        var s = cell.Width * 0.62f;
        var left = cell.X + (cell.Width - s) / 2;
        var top = cell.Y + cell.Height * 0.86f - s;
        void Box(Color color, float x, float y, float w, float h) =>
            Fill(g, Color.FromArgb(alpha, color), new RectangleF(left + x * s, top + y * s, w * s, h * s));

        var body = animal switch
        {
            Animal.Chicken => Color.FromArgb(248, 249, 250),
            Animal.Rabbit => Color.FromArgb(216, 199, 181),
            Animal.Duck => Color.FromArgb(255, 212, 59),
            _ => Color.FromArgb(247, 168, 184),
        };

        switch (animal)
        {
            case Animal.Chicken:
                Box(Color.FromArgb(224, 49, 49), 0.38f, -0.2f, 0.24f, 0.24f); // comb
                break;
            case Animal.Rabbit:
                Box(body, 0.14f, -0.5f, 0.2f, 0.55f); // ears
                Box(body, 0.66f, -0.5f, 0.2f, 0.55f);
                break;
        }
        Box(body, 0, 0, 1, 1);
        switch (animal)
        {
            case Animal.Chicken:
            case Animal.Duck:
                Box(Color.FromArgb(247, 103, 7), 0.3f, 0.42f, 0.4f, 0.18f); // beak
                break;
            case Animal.Pig:
                Box(Color.FromArgb(232, 131, 155), 0.3f, 0.42f, 0.4f, 0.26f); // snout
                break;
        }
        Box(Color.FromArgb(31, 35, 40), 0.18f, 0.22f, 0.14f, 0.14f); // eyes
        Box(Color.FromArgb(31, 35, 40), 0.68f, 0.22f, 0.14f, 0.14f);
    }

    private void DrawNameTag(Graphics g, Player runner, bool isMe)
    {
        var label = runner.RespawnAt > 0 ? $"{runner.Name} (respawning)" : runner.Name;
        using var font = new Font("Segoe UI", 8f, FontStyle.Bold);
        var size = g.MeasureString(label, font);
        var cell = Cell(runner.X, runner.Lane);
        var box = new RectangleF(cell.X + cell.Width / 2 - size.Width / 2 - 4, cell.Y - size.Height, size.Width + 8, size.Height);
        Fill(g, isMe ? Accent : Color.FromArgb(170, 20, 24, 30), box);
        DrawText(g, label, 8f, isMe ? Color.Black : Color.White, box.X + 4, box.Y, bold: true);
    }

    private void DrawTargeting(Graphics g, GameState state)
    {
        if (_selected is not { } ability) return;

        var (laneIndex, column) = ScreenToWorld(_mouse);
        var lane = state.Lanes.FirstOrDefault(l => l.Index == laneIndex);
        var tile = ability == Ability.Boulder;
        var valid = lane != null && Rules.WorksOn(ability, lane.Type) && (!tile || column is >= 0 and < Rules.GridWidth);
        var area = tile ? Cell(column, laneIndex) : Cell(0, laneIndex, Rules.GridWidth);
        var color = valid ? Color.White : Danger;

        Fill(g, Color.FromArgb(70, color), area);
        using var pen = new Pen(color, 2);
        g.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);
    }

    private void DrawRain(Graphics g)
    {
        Fill(g, Color.FromArgb(40, 30, 50, 90), new RectangleF(0, 0, ClientSize.Width, ClientSize.Height));
        using var pen = new Pen(Color.FromArgb(140, 205, 225, 255), 1.4f);
        var time = Environment.TickCount64;
        for (var i = 0; i < 160; i++)
        {
            var x = i * 7919 % 1000 / 1000f * (ClientSize.Width + 40) - 20;
            var y = (i * 104729 % 1000 / 1000f * (ClientSize.Height + 40) + time * 0.9f + i * 13) % (ClientSize.Height + 40) - 20;
            g.DrawLine(pen, x, y, x - 4, y + 14);
        }
    }

    private void DrawHud(Graphics g, GameState state, Player? me, long now)
    {
        float width = ClientSize.Width, height = ClientSize.Height;

        // Timer, and rain if it is falling.
        var seconds = (int)Math.Ceiling(state.TimeLeftMs / 1000.0);
        var timerBox = new RectangleF(width / 2 - 55, 10, 110, 44);
        Fill(g, HudPanel, timerBox);
        TextCentered(g, $"{seconds / 60}:{seconds % 60:00}", 20f, seconds <= 30 ? Color.FromArgb(255, 107, 107) : Color.White, timerBox);
        if (now < state.RainUntil)
        {
            var rainBox = new RectangleF(width / 2 - 55, 60, 110, 26);
            Fill(g, Color.FromArgb(54, 79, 199), rainBox);
            TextCentered(g, $"RAIN {Math.Ceiling((state.RainUntil - now) / 1000.0)}s", 9.5f, Color.White, rainBox);
        }

        // Scoreboard.
        var rows = state.Players.Where(p => !p.IsMastermind).OrderByDescending(p => p.Score).ToList();
        var board = new RectangleF(width - 252, 10, 240, 34 + rows.Count * 26);
        Fill(g, HudPanel, board);
        DrawText(g, "SCOREBOARD", 8f, Color.Silver, board.X + 12, board.Y + 8);
        var y = board.Y + 30;
        foreach (var runner in rows)
        {
            var color = runner.Out ? Color.Gray : runner.Name == _myName ? Accent : Color.White;
            DrawAnimal(g, runner.Animal, new RectangleF(board.X + 8, y - 4, 22, 22), runner.Out ? 110 : 255);
            DrawText(g, runner.Name, 9.5f, color, board.X + 36, y);
            DrawText(g, runner.Out ? runner.Left ? "LEFT" : "OUT" : $"x{runner.Lives}", 8f, color, board.Right - 90, y + 2);
            DrawText(g, runner.Score.ToString(), 9.5f, color, board.Right - 40, y, bold: true);
            y += 26;
        }

        // Who you are.
        var status = new RectangleF(12, 10, 240, 44);
        Fill(g, HudPanel, status);
        if (me is { IsMastermind: true })
        {
            DrawText(g, "MASTERMIND", 9.5f, Color.FromArgb(255, 135, 135), status.X + 12, status.Y + 4, bold: true);
            DrawText(g, "Stop every runner before time runs out", 8f, Color.Gainsboro, status.X + 12, status.Y + 24);
        }
        else if (me != null)
        {
            DrawAnimal(g, me.Animal, new RectangleF(status.X + 6, status.Y + 4, 36, 36));
            DrawText(g, me.Animal.ToString().ToUpperInvariant(), 9.5f, Color.White, status.X + 48, status.Y + 4, bold: true);
            DrawText(g, new string('♥', Math.Max(0, me.Lives)), 11f, Color.FromArgb(232, 54, 74), status.X + 48, status.Y + 20);
        }

        // Bottom: what you can do.
        if (me is { IsMastermind: true })
        {
            DrawAbilityBar(g, me, now);
        }
        else
        {
            var hint = me?.Out == true ? "You're out. Watch the others finish the round."
                : me?.RespawnAt > 0 ? "Respawning..."
                : "Hop with W A S D or the arrow keys";
            var hintBox = new RectangleF(width / 2 - 200, height - 46, 400, 32);
            Fill(g, HudPanel, hintBox);
            TextCentered(g, hint, 9.5f, Color.White, hintBox);
        }

        // Recent messages.
        var messageY = height - 150f;
        foreach (var (text, at) in _messages.Where(m => Environment.TickCount64 - m.At < 4000).TakeLast(4).Reverse())
        {
            using var font = new Font("Segoe UI", 9.5f);
            var size = g.MeasureString(text, font);
            var box = new RectangleF(14, messageY - size.Height - 8, size.Width + 16, size.Height + 8);
            Fill(g, HudPanel, box);
            DrawText(g, text, 9.5f, Color.White, box.X + 8, box.Y + 4);
            messageY = box.Y - 6;
        }
    }

    private RectangleF AbilityButton(Ability ability)
    {
        const float buttonWidth = 130, buttonHeight = 56, gap = 8;
        var total = 4 * buttonWidth + 3 * gap;
        var left = (ClientSize.Width - total) / 2 + (int)ability * (buttonWidth + gap);
        return new RectangleF(left, ClientSize.Height - buttonHeight - 12, buttonWidth, buttonHeight);
    }

    private void DrawAbilityBar(Graphics g, Player me, long now)
    {
        var first = AbilityButton(Ability.Car);
        var last = AbilityButton(Ability.Rain);
        var bar = new RectangleF(first.X, first.Y - 24, last.Right - first.X, 16);
        Fill(g, HudPanel, bar);
        Fill(g, Color.FromArgb(132, 94, 247), bar with { Width = bar.Width * (float)(me.Energy / Rules.MaxEnergy) });
        TextCentered(g, $"ENERGY {(int)me.Energy}/{(int)Rules.MaxEnergy}", 8f, Color.White, bar);

        foreach (var ability in Enum.GetValues<Ability>())
        {
            var box = AbilityButton(ability);
            var cooldown = Math.Max(0, me.ReadyAt[(int)ability] - now);
            var usable = cooldown == 0 && me.Energy >= Rules.Cost(ability);
            Fill(g, _selected == ability ? Color.FromArgb(95, 61, 196) : HudPanel, box);
            if (_selected == ability)
            {
                using var pen = new Pen(Accent, 2.5f);
                g.DrawRectangle(pen, box.X, box.Y, box.Width, box.Height);
            }

            var color = usable ? Color.White : Color.Gray;
            DrawText(g, $"{(int)ability + 1}", 9.5f, Accent, box.X + 8, box.Y + 6, bold: true);
            DrawText(g, Rules.Name(ability), 9.5f, color, box.X + 24, box.Y + 6, bold: true);
            DrawText(g, cooldown > 0 ? $"{Math.Ceiling(cooldown / 1000.0)}s" : $"{Rules.Cost(ability)} energy", 8f, color, box.X + 24, box.Y + 30);
        }

        var hint = _selected is { } selected
            ? $"Click the map to use {Rules.Name(selected)}. Esc or right-click to cancel."
            : "Press 1-4 or click a button to pick an ability";
        var hintBox = new RectangleF(ClientSize.Width / 2f - 230, bar.Y - 34, 460, 26);
        Fill(g, HudPanel, hintBox);
        TextCentered(g, hint, 9.5f, Color.White, hintBox);
    }

    // ---- small helpers ----

    private static void Fill(Graphics g, Color color, RectangleF area)
    {
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, area);
    }

    private static void FillEllipse(Graphics g, Color color, RectangleF area)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, area);
    }

    private static void DrawText(Graphics g, string text, float size, Color color, float x, float y, bool bold = false)
    {
        using var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y);
    }

    private static void TextCentered(Graphics g, string text, float size, Color color, RectangleF area)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Bold);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, area, format);
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 7, 6, 0) };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 4) };
        row.Controls.AddRange(controls);
        return row;
    }
}
