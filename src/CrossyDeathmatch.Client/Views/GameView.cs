using CrossyDeathmatch.Client.Networking;
using CrossyDeathmatch.Client.Rendering;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Views;

/// <summary>The in-round screen: paints the world and HUD every frame and turns keys and clicks into commands.</summary>
internal sealed class GameView : Control
{
    private readonly ServerConnection _server;
    private readonly SnapshotBuffer _snapshots;
    private readonly Func<string?> _myId;
    private readonly Func<PlayerRole?> _myRole;
    private readonly WorldRenderer _world = new();
    private readonly HudRenderer _hud = new();
    private readonly List<Toast> _toasts = [];
    private readonly HashSet<Keys> _heldKeys = [];
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 15 };
    private readonly Font _waitingFont = new("Segoe UI", 14f);
    private AbilityKind? _selectedAbility;
    private PointF? _mouse;
    private GameResultDto? _result;
    private long _lastFrameAt;

    public GameView(ServerConnection server, SnapshotBuffer snapshots, Func<string?> myId, Func<PlayerRole?> myRole)
    {
        _server = server;
        _snapshots = snapshots;
        _myId = myId;
        _myRole = myRole;
        Dock = DockStyle.Fill;
        TabStop = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        _frameTimer.Tick += (_, _) => Invalidate();
    }

    public void Begin()
    {
        _result = null;
        _selectedAbility = null;
        _toasts.Clear();
        _heldKeys.Clear();
        _world.Reset();
        _lastFrameAt = 0;
        _frameTimer.Start();
        Focus();
    }

    public void End() => _frameTimer.Stop();

    public void ShowResult(GameResultDto result) => _result = result;

    public void AddToast(string text, MessageTone tone)
    {
        _toasts.Add(new Toast(text, tone, Environment.TickCount64));
        if (_toasts.Count > 20) _toasts.RemoveAt(0);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_heldKeys.Add(e.KeyCode)) return; // ignore auto-repeat: one press, one hop

        if (_myRole() == PlayerRole.Mastermind)
        {
            if (e.KeyCode == Keys.Escape) _selectedAbility = null;
            var number = e.KeyCode switch
            {
                >= Keys.D1 and <= Keys.D5 => e.KeyCode - Keys.D1 + 1,
                >= Keys.NumPad1 and <= Keys.NumPad5 => e.KeyCode - Keys.NumPad1 + 1,
                _ => 0,
            };
            if (AbilityCatalog.All.FirstOrDefault(a => a.Key == number) is { } ability) ChooseAbility(ability.Kind);
            return;
        }

        Direction? direction = e.KeyCode switch
        {
            Keys.W or Keys.Up => Direction.Up,
            Keys.S or Keys.Down => Direction.Down,
            Keys.A or Keys.Left => Direction.Left,
            Keys.D or Keys.Right => Direction.Right,
            _ => null,
        };
        if (direction is { } dir) _ = _server.Move(dir);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _heldKeys.Remove(e.KeyCode);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _heldKeys.Clear();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _mouse = e.Location;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _mouse = null;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (_myRole() != PlayerRole.Mastermind || _result is not null) return;

        if (e.Button == MouseButtons.Right)
        {
            _selectedAbility = null;
            return;
        }

        if (_hud.AbilityAt(e.Location) is { } clicked)
        {
            ChooseAbility(clicked);
            return;
        }

        if (_selectedAbility is { } selected && AbilityCatalog.Get(selected).Target != AbilityTarget.None)
        {
            var (lane, column) = _world.Camera.ScreenToWorld(e.Location);
            _ = _server.UseAbility(selected, lane, column);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var now = Environment.TickCount64;
        var dt = _lastFrameAt == 0 ? 0f : Math.Min(0.1f, (now - _lastFrameAt) / 1000f);
        _lastFrameAt = now;

        var viewport = new SizeF(ClientSize.Width, ClientSize.Height);
        var frame = _snapshots.Sample();
        var latest = _snapshots.Latest;
        if (frame is null || latest is null)
        {
            g.Clear(Palette.Ink);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Waiting for the server…", _waitingFont, Brushes.White, new RectangleF(PointF.Empty, viewport), format);
            return;
        }

        var scale = DeviceDpi / 96f;
        var role = _myRole();
        var me = _myId();
        _world.Draw(g, viewport, frame, new ViewOptions(me, role, _selectedAbility, _mouse), now, dt);
        _hud.Draw(g, viewport, scale, latest, me, role, _selectedAbility, now);
        _hud.DrawToasts(g, viewport, scale, _toasts, now);
        if (_result is not null) _hud.DrawResults(g, viewport, scale, _result, me);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _frameTimer.Dispose();
        base.Dispose(disposing);
    }

    private void ChooseAbility(AbilityKind kind)
    {
        if (AbilityCatalog.Get(kind).Target == AbilityTarget.None)
        {
            _selectedAbility = null;
            _ = _server.UseAbility(kind, null, null);
            return;
        }
        _selectedAbility = _selectedAbility == kind ? null : kind;
    }
}
