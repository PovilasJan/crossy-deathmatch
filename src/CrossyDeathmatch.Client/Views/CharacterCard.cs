using System.Drawing.Drawing2D;
using CrossyDeathmatch.Client.Rendering;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Views;

/// <summary>Clickable card showing an animal and its stats.</summary>
internal sealed class CharacterCard : Button
{
    private readonly CharacterStats _stats;
    private bool _selected;
    private string? _takenBy;
    private bool _hover;

    public CharacterCard(CharacterKind kind)
    {
        Kind = kind;
        _stats = CharacterCatalog.Get(kind);
        Size = new Size(170, 190);
        Margin = new Padding(0, 0, 12, 0);
        Cursor = Cursors.Hand;
        AccessibleName = _stats.Name;
        FlatStyle = FlatStyle.Flat;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    public CharacterKind Kind { get; }

    public void SetState(bool selected, string? takenBy, bool enabled)
    {
        _selected = selected;
        _takenBy = takenBy;
        Enabled = enabled;
        Cursor = enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.Background);

        var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
        var radius = new SizeF(14, 14);
        g.FillRoundedRectangle(Palette.Brush(_selected ? Palette.Hex("#ebfbee") : Color.White), bounds, radius);
        var border = _selected ? Ui.Primary : _hover && Enabled ? Palette.Hex("#adb5bd") : Palette.Hex("#dee2e6");
        g.DrawRoundedRectangle(Palette.Pen(border, _selected ? 3 : 1.5f), bounds, radius);
        if (Focused && ShowFocusCues)
        {
            g.DrawRoundedRectangle(Palette.Pen(Palette.Hex("#339af0"), 2), RectangleF.Inflate(bounds, -5, -5), new SizeF(10, 10));
        }

        var icon = Width * 0.42f;
        var alpha = _takenBy is null ? 255 : 90;
        Sprites.DrawCharacter(g, Kind, new RectangleF((Width - icon) / 2, 18, icon, icon), alpha: alpha);

        using var title = new Font("Segoe UI", 11f, FontStyle.Bold);
        using var body = new Font("Segoe UI", 8.5f);
        using var center = new StringFormat { Alignment = StringAlignment.Center };
        var text = Palette.Brush(_takenBy is null ? Ui.Text : Ui.Muted);
        var y = 20 + icon + 6;
        g.DrawString(_stats.Name, title, text, new RectangleF(0, y, Width, 22), center);
        g.DrawString($"{_stats.Lives} lives · {HopSpeed(_stats.HopCooldownMs)} hops", body, text, new RectangleF(0, y + 24, Width, 18), center);
        var caption = _takenBy is null ? _stats.Description : $"Taken by {_takenBy}";
        g.DrawString(caption, body, Palette.Brush(Ui.Muted), new RectangleF(8, y + 42, Width - 16, 34), center);
    }

    private static string HopSpeed(int cooldownMs) => cooldownMs switch
    {
        <= 130 => "very fast",
        <= 190 => "fast",
        <= 260 => "steady",
        _ => "slow",
    };
}
