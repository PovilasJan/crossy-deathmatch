using CrossyDeathmatch.Client.Rendering;

namespace CrossyDeathmatch.Client.Views;

/// <summary>Small factory for consistently styled WinForms controls.</summary>
internal static class Ui
{
    public static readonly Color Background = Palette.Hex("#eef3e6");
    public static readonly Color Card = Color.White;
    public static readonly Color Primary = Palette.Hex("#2f9e44");
    public static readonly Color PrimaryText = Color.White;
    public static readonly Color Muted = Palette.Hex("#6c757d");
    public static readonly Color Text = Palette.Hex("#212529");
    public static readonly Color Danger = Palette.Hex("#c92a2a");

    public static Label Title(string text, float size = 22f) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        ForeColor = Text,
        Margin = new Padding(0, 0, 0, 6),
    };

    public static Label Caption(string text, bool muted = false, float size = 10f, bool bold = false) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        ForeColor = muted ? Muted : Text,
        Margin = new Padding(0, 4, 0, 4),
    };

    public static Button Button(string text, bool primary = false) => Style(new Button { Text = text }, primary);

    public static Button Style(Button button, bool primary)
    {
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(14, 6, 14, 6);
        button.Margin = new Padding(0, 4, 8, 4);
        button.FlatStyle = FlatStyle.Flat;
        button.Font = new Font("Segoe UI", 10f, primary ? FontStyle.Bold : FontStyle.Regular);
        button.BackColor = primary ? Primary : Palette.Hex("#e9ecef");
        button.ForeColor = primary ? PrimaryText : Text;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        return button;
    }

    public static TextBox Input(string text = "", int width = 260) => new()
    {
        Text = text,
        Width = width,
        Font = new Font("Segoe UI", 11f),
        Margin = new Padding(0, 2, 8, 8),
    };

    public static TableLayoutPanel Column(int rows = 0) => new()
    {
        ColumnCount = 1,
        RowCount = rows,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = Card,
        Padding = new Padding(24),
    };

    public static FlowLayoutPanel Row() => new()
    {
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = new Padding(0),
        BackColor = Color.Transparent,
    };

    public static ListView List(params (string Header, int Width)[] columns)
    {
        var list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 10f),
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
        };
        foreach (var (header, width) in columns) list.Columns.Add(header, width);
        return list;
    }
}
