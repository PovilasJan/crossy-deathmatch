using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>
/// The lobby, shown between rounds: the server address and nickname, the animal and the Ready button, who else is
/// here, and the last round's result and messages. Like <see cref="MainForm"/>, it reaches the game only through
/// the <see cref="GameClient"/> facade.
/// </summary>
public class LobbyPanel : FlowLayoutPanel
{
    private readonly GameClient _client;
    private readonly TextBox _serverBox;
    private readonly TextBox _nameBox;
    private readonly Button _connectButton;
    private readonly ComboBox _animalBox;
    private readonly Button _readyButton;
    private readonly ListBox _playerList;
    private readonly Label _infoLabel;

    public LobbyPanel(GameClient client, string server, string name)
    {
        _client = client;
        Dock = DockStyle.Fill;
        FlowDirection = FlowDirection.TopDown;
        WrapContents = false;
        AutoScroll = true;
        Padding = new Padding(24);

        _serverBox = new TextBox { Text = server, Width = 260 };
        _nameBox = new TextBox { Text = name, Width = 160, MaxLength = Rules.MaxNameLength };
        _connectButton = new Button { Text = "Connect", AutoSize = true };
        _animalBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Enabled = false };
        _animalBox.Items.AddRange(Enum.GetValues<Animal>().Cast<object>().ToArray());
        _readyButton = new Button { Text = "Ready", AutoSize = true, Enabled = false };
        _playerList = new ListBox { Width = 420, Height = 100 };
        _infoLabel = new Label { AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(0, 12, 0, 0) };

        Controls.Add(new Label { Text = "Crossy Deathmatch", Font = new Font("Segoe UI", 22f, FontStyle.Bold), AutoSize = true });
        Controls.Add(Caption("Three runners hop across roads and railways. One Mastermind tries to stop them."));
        Controls.Add(Row(Caption("Server"), _serverBox, Caption("Nickname"), _nameBox, _connectButton));
        Controls.Add(Row(Caption("Animal"), _animalBox, _readyButton));
        Controls.Add(_playerList);
        Controls.Add(_infoLabel);

        _client.StateChanged += OnStateChanged;
        _client.MessagesChanged += UpdateInfo;

        _connectButton.Click += async (_, _) => await Connect();
        _animalBox.SelectionChangeCommitted += (_, _) => _client.PickAnimal((Animal)_animalBox.SelectedItem!);
        _readyButton.Click += (_, _) => _client.ToggleReady();
    }

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
            await _client.JoinAsync(_serverBox.Text, name);
        }
        catch (Exception ex)
        {
            _infoLabel.Text = $"Could not connect: {ex.Message}";
        }
        finally
        {
            _connectButton.Enabled = _client.Me == null;
        }
    }

    /// <summary>Also runs when the connection is lost: then there is no state and nobody is "me".</summary>
    private void OnStateChanged()
    {
        var me = _client.Me;
        _serverBox.Enabled = _nameBox.Enabled = _connectButton.Enabled = me == null;
        _animalBox.Enabled = _readyButton.Enabled = me != null;
        if (me != null && !_animalBox.DroppedDown) _animalBox.SelectedItem = me.Animal;
        _readyButton.Text = me?.Ready == true ? "Not ready" : "Ready";

        _playerList.BeginUpdate();
        _playerList.Items.Clear();
        foreach (var player in _client.State?.Players ?? [])
        {
            _playerList.Items.Add($"{player.Name}  -  {player.Animal}{(player.Ready ? "  -  ready" : "")}{(player == me ? "  (you)" : "")}");
        }
        _playerList.EndUpdate();
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        var state = _client.State;
        var lines = new List<string>();
        if (state is { Playing: false })
        {
            lines.Add("When everyone is ready, the round starts and one random player becomes the Mastermind.");
        }
        if (state?.Result is { } result) lines.Add("\nLast round: " + result);
        lines.Add("");
        lines.AddRange(_client.Messages.TakeLast(6).Select(m => m.Text));
        _infoLabel.Text = string.Join("\n", lines);
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
