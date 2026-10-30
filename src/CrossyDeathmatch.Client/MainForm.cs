namespace CrossyDeathmatch.Client;

/// <summary>
/// The client window: the <see cref="LobbyPanel"/> between rounds, and during a round the game screen. Both reach
/// the game only through the <see cref="GameClient"/> facade; the window passes it input and asks it to draw each
/// frame, without knowing which role's view or which renderer (F3 switches it) does the work.
/// </summary>
public class MainForm : Form
{
    private readonly GameClient _client;
    private readonly LobbyPanel _lobby;
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 15 };

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

        _client = new GameClient(OnUi);
        _lobby = new LobbyPanel(_client, server, name);
        Controls.Add(_lobby);

        _client.StateChanged += OnStateChanged;
        _frameTimer.Tick += (_, _) => Invalidate();
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

    /// <summary>The game screen while a round this player is in is running, the lobby otherwise.</summary>
    private void OnStateChanged()
    {
        var playing = _client.Playing;
        if (playing == _lobby.Visible)
        {
            _lobby.Visible = !playing;
            if (playing)
            {
                ActiveControl = null;
                _frameTimer.Start();
            }
            else
            {
                _frameTimer.Stop();
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _frameTimer.Stop();
        _ = _client.DisposeAsync();
        base.OnFormClosing(e);
    }

    // ---- the game screen: input and drawing go through the facade ----

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_lobby.Visible) return base.ProcessCmdKey(ref msg, keyData);

        if (keyData == Keys.F3) _client.ToggleWireframe();
        else if (!_client.HandleKey(keyData)) return base.ProcessCmdKey(ref msg, keyData);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        _client.HandleMouseMove(e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!_lobby.Visible) _client.HandleMouseDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (!_lobby.Visible) _client.Draw(e.Graphics, ClientSize);
    }
}
