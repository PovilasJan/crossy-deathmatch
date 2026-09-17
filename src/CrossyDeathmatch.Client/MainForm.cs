using CrossyDeathmatch.Client.Networking;
using CrossyDeathmatch.Client.Views;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>
/// The window. Owns the connection, switches between the four screens, and marshals every
/// server message onto the UI thread.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly ServerConnection _server = new();
    private readonly SnapshotBuffer _snapshots = new();
    private readonly ConnectView _connectView;
    private readonly LobbyView _lobbyView;
    private readonly RoomView _roomView;
    private readonly GameView _gameView;
    private string? _playerId;
    private string? _playerName;
    private RoomDto? _room;

    public MainForm(string defaultServer, string? defaultName)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "Crossy Deathmatch";
        ClientSize = new Size(1200, 800);
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10f);
        BackColor = Ui.Background;

        _connectView = new ConnectView(defaultServer, defaultName);
        _lobbyView = new LobbyView { Visible = false };
        _roomView = new RoomView { Visible = false };
        _gameView = new GameView(_server, _snapshots, () => _playerId, MyRole) { Visible = false };
        Controls.AddRange([_connectView, _lobbyView, _roomView, _gameView]);
        ResumeLayout(performLayout: true);

        WireViews();
        WireServer();
        Shown += (_, _) => _connectView.FocusName();
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _gameView.End();
        await _server.DisposeAsync();
    }

    private PlayerRole? MyRole() => _room?.Players.FirstOrDefault(p => p.Id == _playerId)?.Role;

    private void WireViews()
    {
        _connectView.ConnectRequested += async (url, name) =>
        {
            _connectView.SetBusy(true);
            _connectView.ShowMessage("", isError: false);
            try
            {
                if (!_server.IsConnected) await _server.ConnectAsync(url);
                _playerName = name;
                await _server.Hello(name);
            }
            catch (Exception ex)
            {
                _connectView.ShowMessage($"Could not reach {url}: {ex.Message}", isError: true);
            }
            finally
            {
                _connectView.SetBusy(false);
            }
        };

        _lobbyView.CreateRoomRequested += isPublic => _ = _server.CreateRoom(isPublic);
        _lobbyView.JoinRoomRequested += code => _ = _server.JoinRoom(code);
        _lobbyView.RefreshRequested += () => _ = _server.ListRooms();

        _roomView.LeaveRequested += () => _ = _server.LeaveRoom();
        _roomView.ReadyRequested += ready => _ = _server.SetReady(ready);
        _roomView.VisibilityRequested += isPublic => _ = _server.SetVisibility(isPublic);
        _roomView.CharacterPicked += kind => _ = _server.PickCharacter(kind);
    }

    private void WireServer()
    {
        // Snapshots arrive 20 times a second; they go straight into the thread-safe buffer.
        _server.Snapshot += _snapshots.Push;

        _server.Welcome += (id, profile) => OnUi(() =>
        {
            _playerId = id;
            _lobbyView.SetProfile(profile);
            _lobbyView.ShowNotice("", MessageTone.Info);
            Text = $"Crossy Deathmatch — {profile.Name}";
            Show(_lobbyView);
        });
        _server.ProfileUpdated += profile => OnUi(() => _lobbyView.SetProfile(profile));
        _server.RoomList += rooms => OnUi(() => _lobbyView.SetRooms(rooms));
        _server.Error += message => OnUi(() => Notify(message, MessageTone.Bad));
        _server.GameEvent += (text, tone) => OnUi(() => Notify(text, tone));

        _server.RoomState += room => OnUi(() =>
        {
            var joinedNewRoom = _room?.Code != room.Code;
            _room = room;
            if (joinedNewRoom) _roomView.ClearLog();

            switch (room.Phase)
            {
                case RoomPhase.Lobby or RoomPhase.CharacterSelect:
                    _roomView.Render(room, _playerId);
                    if (!_roomView.Visible)
                    {
                        _gameView.End();
                        Show(_roomView);
                    }
                    break;
                case RoomPhase.Playing when !_gameView.Visible:
                    StartGameView();
                    break;
            }
        });

        _server.LeftRoom += () => OnUi(() =>
        {
            _room = null;
            _gameView.End();
            Show(_lobbyView);
        });

        _server.GameStarted += _ => OnUi(StartGameView);
        _server.GameOver += result => OnUi(() => _gameView.ShowResult(result));

        _server.Closed += reason => OnUi(() =>
        {
            _playerId = null;
            _room = null;
            _gameView.End();
            _connectView.ShowMessage(reason is null ? "Disconnected from the server." : $"Disconnected: {reason}", isError: true);
            Show(_connectView);
        });
    }

    private void StartGameView()
    {
        _snapshots.Clear();
        Show(_gameView);
        _gameView.Begin();
    }

    private void Notify(string text, MessageTone tone)
    {
        if (_gameView.Visible) _gameView.AddToast(text, tone);
        else if (_roomView.Visible) _roomView.AddLog(text, tone);
        else if (_lobbyView.Visible) _lobbyView.ShowNotice(text, tone);
        else _connectView.ShowMessage(text, tone == MessageTone.Bad);
    }

    private void Show(Control view)
    {
        foreach (Control control in Controls) control.Visible = control == view;
        view.Focus();
    }

    private void OnUi(Action action)
    {
        if (IsDisposed || Disposing) return;
        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // Window is closing.
        }
    }
}
