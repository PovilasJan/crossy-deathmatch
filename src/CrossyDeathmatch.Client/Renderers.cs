using System.Drawing.Drawing2D;

namespace CrossyDeathmatch.Client;

/// <summary>
/// Bridge (implementor): the drawing primitives the game screen is built from, in pixels. The views decide what
/// to draw; a renderer decides how it reaches the screen.
/// </summary>
public interface IRenderer
{
    void Clear(Color color);
    void FillRect(Color color, RectangleF area);
    void FillEllipse(Color color, RectangleF area);
    void OutlineRect(Color color, float width, RectangleF area);
    void OutlineEllipse(Color color, float width, RectangleF area);
    void Line(Color color, float width, PointF from, PointF to);
    void Text(string text, float size, Color color, float x, float y, bool bold = false);
    void TextCentered(string text, float size, Color color, RectangleF area);
    SizeF MeasureText(string text, float size, bool bold = false);
}

/// <summary>The normal look: filled, anti-aliased shapes drawn with GDI+ onto one frame's <see cref="Graphics"/>.</summary>
public class GdiRenderer : IRenderer
{
    private readonly Graphics _g;

    public GdiRenderer(Graphics g)
    {
        _g = g;
        _g.SmoothingMode = SmoothingMode.AntiAlias;
    }

    public virtual void Clear(Color color) => _g.Clear(color);

    public virtual void FillRect(Color color, RectangleF area)
    {
        using var brush = new SolidBrush(color);
        _g.FillRectangle(brush, area);
    }

    public virtual void FillEllipse(Color color, RectangleF area)
    {
        using var brush = new SolidBrush(color);
        _g.FillEllipse(brush, area);
    }

    public void OutlineRect(Color color, float width, RectangleF area)
    {
        using var pen = new Pen(color, width);
        _g.DrawRectangle(pen, area.X, area.Y, area.Width, area.Height);
    }

    public void OutlineEllipse(Color color, float width, RectangleF area)
    {
        using var pen = new Pen(color, width);
        _g.DrawEllipse(pen, area);
    }

    public void Line(Color color, float width, PointF from, PointF to)
    {
        using var pen = new Pen(color, width);
        _g.DrawLine(pen, from, to);
    }

    public virtual void Text(string text, float size, Color color, float x, float y, bool bold = false)
    {
        using var font = MakeFont(size, bold);
        using var brush = new SolidBrush(color);
        _g.DrawString(text, font, brush, x, y);
    }

    public virtual void TextCentered(string text, float size, Color color, RectangleF area)
    {
        using var font = MakeFont(size, bold: true);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        _g.DrawString(text, font, brush, area, format);
    }

    public SizeF MeasureText(string text, float size, bool bold = false)
    {
        using var font = MakeFont(size, bold);
        return _g.MeasureString(text, font);
    }

    private static Font MakeFont(float size, bool bold) => new("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
}

/// <summary>
/// A debug look (F3 during a round): every filled shape becomes a thin outline on a dark background, so the exact
/// rectangles behind lanes, cars, animals and panels show. Outlines and text are drawn as in <see cref="GdiRenderer"/>.
/// </summary>
public class WireframeRenderer(Graphics g) : GdiRenderer(g)
{
    private static readonly Color Background = Color.FromArgb(18, 20, 24);

    public override void Clear(Color color) => base.Clear(Background);

    public override void FillRect(Color color, RectangleF area) => OutlineRect(Visible(color), 1, area);

    public override void FillEllipse(Color color, RectangleF area) => OutlineEllipse(Visible(color), 1, area);

    public override void Text(string text, float size, Color color, float x, float y, bool bold = false) =>
        base.Text(text, size, Visible(color), x, y, bold);

    public override void TextCentered(string text, float size, Color color, RectangleF area) =>
        base.TextCentered(text, size, Visible(color), area);

    /// <summary>Nothing is filled in behind anything here, so dark colours would vanish on the dark background.</summary>
    private static Color Visible(Color color) => color.GetBrightness() < 0.3f ? Color.FromArgb(color.A, Color.White) : color;
}
