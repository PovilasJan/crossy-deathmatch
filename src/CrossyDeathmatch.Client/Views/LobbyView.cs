using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Views;

/// <summary>Create a room, join by code, or pick one from the public list.</summary>
internal sealed class LobbyView : UserControl
{
    private readonly Label _profile;
    private readonly Label _notice;
    private readonly TextBox _code;
    private readonly ListView _rooms;

    public event Action<bool>? CreateRoomRequested;
    public event Action<string>? JoinRoomRequested;
    public event Action? RefreshRequested;

    public LobbyView()
    {
        Dock = DockStyle.Fill;
        BackColor = Ui.Background;
        Padding = new Padding(24);

        _profile = Ui.Caption("", muted: true);
        _notice = Ui.Caption("");
        _code = Ui.Input(width: 90);
        _code.MaxLength = 4;
        _code.CharacterCasing = CharacterCasing.Upper;
        _rooms = Ui.List(("Code", 90), ("Host", 220), ("Players", 90), ("Status", 160));

        var createPublic = Ui.Button("Create public room", primary: true);
        var createPrivate = Ui.Button("Create private room");
        var join = Ui.Button("Join");
        var joinSelected = Ui.Button("Join selected");
        var refresh = Ui.Button("Refresh");

        var play = Ui.Column();
        play.Dock = DockStyle.Top;
        play.Controls.Add(Ui.Title("Crossy Deathmatch"));
        play.Controls.Add(_profile);
        var createRow = Ui.Row();
        createRow.Controls.AddRange([createPublic, createPrivate]);
        play.Controls.Add(createRow);
        var joinRow = Ui.Row();
        joinRow.Controls.AddRange([Ui.Caption("Room code:"), _code, join]);
        play.Controls.Add(joinRow);
        play.Controls.Add(_notice);

        var listHeader = Ui.Row();
        listHeader.Dock = DockStyle.Top;
        listHeader.Padding = new Padding(0, 16, 0, 4);
        listHeader.Controls.AddRange([Ui.Title("Public rooms", 14f), joinSelected, refresh]);

        Controls.Add(_rooms);
        Controls.Add(listHeader);
        Controls.Add(play);

        createPublic.Click += (_, _) => CreateRoomRequested?.Invoke(true);
        createPrivate.Click += (_, _) => CreateRoomRequested?.Invoke(false);
        join.Click += (_, _) => JoinByCode();
        _code.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            JoinByCode();
        };
        joinSelected.Click += (_, _) => JoinSelected();
        _rooms.DoubleClick += (_, _) => JoinSelected();
        refresh.Click += (_, _) => RefreshRequested?.Invoke();
    }

    public void SetProfile(ProfileDto profile) =>
        _profile.Text = $"Signed in as {profile.Name}  ·  {profile.Coins} coins  ·  {profile.GamesPlayed} games  ·  {profile.Wins} wins  ·  best score {profile.BestScore}";

    public void SetRooms(IReadOnlyList<RoomSummaryDto> rooms)
    {
        _rooms.BeginUpdate();
        _rooms.Items.Clear();
        foreach (var room in rooms)
        {
            var status = room.Phase switch
            {
                RoomPhase.Lobby when room.PlayerCount >= room.MaxPlayers => "Full",
                RoomPhase.Lobby => "Waiting for players",
                RoomPhase.CharacterSelect => "Picking animals",
                _ => "Playing",
            };
            var item = new ListViewItem([room.Code, room.HostName, $"{room.PlayerCount}/{room.MaxPlayers}", status]) { Tag = room.Code };
            if (room.Phase != RoomPhase.Lobby || room.PlayerCount >= room.MaxPlayers) item.ForeColor = Ui.Muted;
            _rooms.Items.Add(item);
        }
        if (rooms.Count == 0) _rooms.Items.Add(new ListViewItem(["", "No public rooms yet. Create one!", "", ""]) { ForeColor = Ui.Muted });
        _rooms.EndUpdate();
    }

    public void ShowNotice(string text, MessageTone tone)
    {
        _notice.Text = text;
        _notice.ForeColor = tone == MessageTone.Bad ? Ui.Danger : Ui.Muted;
    }

    private void JoinByCode()
    {
        var code = _code.Text.Trim();
        if (code.Length == 4) JoinRoomRequested?.Invoke(code);
        else ShowNotice("Room codes have 4 letters.", MessageTone.Bad);
    }

    private void JoinSelected()
    {
        if (_rooms.SelectedItems.Count == 1 && _rooms.SelectedItems[0].Tag is string code) JoinRoomRequested?.Invoke(code);
    }
}
