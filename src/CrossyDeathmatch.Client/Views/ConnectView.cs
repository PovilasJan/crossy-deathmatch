namespace CrossyDeathmatch.Client.Views;

/// <summary>First screen: server address and nickname.</summary>
internal sealed class ConnectView : UserControl
{
    private readonly TextBox _server;
    private readonly TextBox _name;
    private readonly Button _connect;
    private readonly Label _message;

    public event Action<string, string>? ConnectRequested;

    public ConnectView(string defaultServer, string? defaultName)
    {
        Dock = DockStyle.Fill;
        BackColor = Ui.Background;

        _server = Ui.Input(defaultServer, 320);
        _name = Ui.Input(defaultName ?? "", 320);
        _name.MaxLength = Shared.GameRules.MaxNameLength;
        _connect = Ui.Button("Play", primary: true);
        _message = Ui.Caption("", muted: true);
        _message.MaximumSize = new Size(320, 0);

        var card = Ui.Column();
        card.Anchor = AnchorStyles.None;
        card.Controls.Add(Ui.Title("Crossy Deathmatch", 26f));
        card.Controls.Add(Ui.Caption("Three runners hop for their lives. One Mastermind tries to stop them.", muted: true));
        card.Controls.Add(Ui.Caption("Server address", bold: true));
        card.Controls.Add(_server);
        card.Controls.Add(Ui.Caption("Nickname", bold: true));
        card.Controls.Add(_name);
        card.Controls.Add(_connect);
        card.Controls.Add(_message);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        center.Controls.Add(card);
        Controls.Add(center);

        _connect.Click += (_, _) => Submit();
        _name.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            Submit();
        };
    }

    public void FocusName() => (_name.Text.Length == 0 ? (Control)_name : _connect).Focus();

    public void SetBusy(bool busy)
    {
        _connect.Enabled = !busy;
        _connect.Text = busy ? "Connecting…" : "Play";
    }

    public void ShowMessage(string text, bool isError)
    {
        _message.Text = text;
        _message.ForeColor = isError ? Ui.Danger : Ui.Muted;
    }

    private void Submit()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            ShowMessage("Pick a nickname first.", isError: true);
            return;
        }
        ConnectRequested?.Invoke(_server.Text.Trim(), name);
    }
}
