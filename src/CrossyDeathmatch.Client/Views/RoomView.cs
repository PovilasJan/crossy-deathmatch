using CrossyDeathmatch.Client.Rendering;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Views;

/// <summary>The room before a round: who's here, ready states, role reveal and animal selection.</summary>
internal sealed class RoomView : UserControl
{
    private readonly Label _title;
    private readonly Label _status;
    private readonly Label _roleBanner;
    private readonly Button _visibility;
    private readonly Button _ready;
    private readonly ListView _players;
    private readonly FlowLayoutPanel _characters;
    private readonly ListBox _log;
    private readonly Dictionary<CharacterKind, CharacterCard> _cards = [];
    private RoomDto? _room;
    private bool _meReady;

    public event Action? LeaveRequested;
    public event Action<bool>? ReadyRequested;
    public event Action<bool>? VisibilityRequested;
    public event Action<CharacterKind>? CharacterPicked;

    public RoomView()
    {
        Dock = DockStyle.Fill;
        BackColor = Ui.Background;
        Padding = new Padding(24);

        _title = Ui.Title("Room");
        _visibility = Ui.Button("");
        var leave = Ui.Button("Leave room");
        _status = Ui.Caption("", muted: true, size: 11f);
        _roleBanner = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(760, 0),
            Font = new Font("Segoe UI", 11f),
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 8, 0, 8),
            Visible = false,
        };
        _ready = Ui.Button("Ready", primary: true);
        _ready.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _ready.Padding = new Padding(28, 10, 28, 10);

        _players = Ui.List(("Player", 260), ("Ready", 90), ("Role", 130), ("Animal", 120));
        _players.Dock = DockStyle.None;
        _players.Size = new Size(620, 150);
        _players.Scrollable = false;

        _characters = Ui.Row();
        _characters.Visible = false;
        foreach (var character in CharacterCatalog.All)
        {
            var card = new CharacterCard(character.Kind);
            card.Click += (_, _) => CharacterPicked?.Invoke(card.Kind);
            _cards[character.Kind] = card;
            _characters.Controls.Add(card);
        }

        _log = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Ui.Muted,
            IntegralHeight = false,
        };

        var header = Ui.Row();
        header.Controls.AddRange([_title, _visibility, leave]);

        var main = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        main.Controls.AddRange([header, _status, _players, _roleBanner, _characters, _ready]);

        var logTitle = Ui.Caption("Room log", bold: true);
        logTitle.Dock = DockStyle.Top;

        Controls.Add(_log);
        Controls.Add(logTitle);
        Controls.Add(main);

        leave.Click += (_, _) => LeaveRequested?.Invoke();
        _ready.Click += (_, _) => ReadyRequested?.Invoke(!_meReady);
        _visibility.Click += (_, _) =>
        {
            if (_room is not null) VisibilityRequested?.Invoke(!_room.IsPublic);
        };
    }

    public void Render(RoomDto room, string? meId)
    {
        _room = room;
        var me = room.Players.FirstOrDefault(p => p.Id == meId);
        var isHost = room.HostId == meId;
        _meReady = me?.Ready ?? false;

        _title.Text = $"Room {room.Code}";
        _visibility.Text = room.IsPublic ? "Public (make private)" : "Private (make public)";
        _visibility.Enabled = isHost;
        _visibility.Visible = isHost;

        RenderPlayers(room, meId);

        var readyCount = room.Players.Count(p => p.Ready);
        _status.Text = room.Phase switch
        {
            RoomPhase.Lobby when room.Players.Count < room.MinPlayers =>
                $"Waiting for players: {room.Players.Count}/{room.MaxPlayers}. The game needs {room.MinPlayers} to start. Share the code {room.Code}.",
            RoomPhase.Lobby =>
                $"{readyCount}/{room.Players.Count} ready. Roles are dealt as soon as everyone is ready.",
            RoomPhase.CharacterSelect =>
                "Roles are dealt! Runners pick an animal, then everyone gets ready again to start the round.",
            _ => "A round is in progress.",
        };

        var selecting = room.Phase == RoomPhase.CharacterSelect;
        _roleBanner.Visible = selecting && me?.Role is not null;
        if (me?.Role == PlayerRole.Mastermind)
        {
            _roleBanner.Text = "You are the MASTERMIND. You see the whole map and throw cars, boulders, foxes, rain and night at the runners. You win if every runner is out before the timer ends.";
            _roleBanner.BackColor = Palette.Hex("#ffe3e3");
            _roleBanner.ForeColor = Palette.Hex("#862e2e");
        }
        else if (me?.Role == PlayerRole.Runner)
        {
            _roleBanner.Text = $"You are a RUNNER. Hop as far as you can: +{GameRules.PointsPerLane} per new lane, +{GameRules.PointsPerCoin} per coin. Survive until the timer ends with the best score to win.";
            _roleBanner.BackColor = Palette.Hex("#d3f9d8");
            _roleBanner.ForeColor = Palette.Hex("#1b5e20");
        }

        _characters.Visible = selecting && me?.Role == PlayerRole.Runner;
        foreach (var (kind, card) in _cards)
        {
            var takenBy = room.Players.FirstOrDefault(p => p.Character == kind && p.Id != meId)?.Name;
            card.SetState(me?.Character == kind, takenBy, enabled: takenBy is null && selecting);
        }

        var needsAnimal = selecting && me?.Role == PlayerRole.Runner && me.Character is null;
        _ready.Enabled = room.Phase is RoomPhase.Lobby or RoomPhase.CharacterSelect && !needsAnimal;
        _ready.Text = needsAnimal ? "Pick an animal first" : _meReady ? "Not ready" : "Ready";
        Ui.Style(_ready, primary: !_meReady && _ready.Enabled);
        _ready.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        _ready.Padding = new Padding(28, 10, 28, 10);
    }

    public void AddLog(string text, MessageTone tone)
    {
        _log.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        while (_log.Items.Count > 100) _log.Items.RemoveAt(_log.Items.Count - 1);
    }

    public void ClearLog() => _log.Items.Clear();

    private void RenderPlayers(RoomDto room, string? meId)
    {
        _players.BeginUpdate();
        _players.Items.Clear();
        foreach (var player in room.Players)
        {
            var name = player.Name + (player.IsHost ? "  (host)" : "") + (player.Id == meId ? "  ← you" : "");
            var role = player.Role switch
            {
                PlayerRole.Mastermind => "Mastermind",
                PlayerRole.Runner => "Runner",
                _ => "",
            };
            var animal = player.Character is { } kind ? CharacterCatalog.Get(kind).Name : "";
            var item = new ListViewItem([name, player.Ready ? "✔ Ready" : "…", role, animal]);
            if (player.Ready) item.ForeColor = Ui.Primary;
            _players.Items.Add(item);
        }
        for (var slot = room.Players.Count; slot < room.MaxPlayers; slot++)
        {
            _players.Items.Add(new ListViewItem(["Open slot", "", "", ""]) { ForeColor = Palette.Hex("#adb5bd") });
        }
        _players.EndUpdate();
    }
}
