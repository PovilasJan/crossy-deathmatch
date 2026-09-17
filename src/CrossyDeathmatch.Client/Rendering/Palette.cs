using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

/// <summary>Colours plus a cache of GDI+ brushes and pens, so painting 60 times a second allocates nothing.</summary>
internal static class Palette
{
    private static readonly Dictionary<int, SolidBrush> Brushes = [];
    private static readonly Dictionary<(int, float), Pen> Pens = [];

    public static readonly Color GrassLight = Hex("#9bd770");
    public static readonly Color GrassDark = Hex("#8fcc64");
    public static readonly Color Road = Hex("#575c64");
    public static readonly Color RoadLine = Hex("#e8e2cc");
    public static readonly Color River = Hex("#4aa2dd");
    public static readonly Color RiverLight = Hex("#74bdec");
    public static readonly Color Rail = Hex("#8c7c6b");
    public static readonly Color RailSleeper = Hex("#5d4a3b");
    public static readonly Color RailSteel = Hex("#cfd3d8");
    public static readonly Color Tree = Hex("#2f8a3e");
    public static readonly Color TreeLight = Hex("#46a556");
    public static readonly Color Trunk = Hex("#7a5230");
    public static readonly Color Wood = Hex("#8b5a2b");
    public static readonly Color WoodLight = Hex("#a8743f");
    public static readonly Color WoodEnd = Hex("#d6a86f");
    public static readonly Color Coin = Hex("#ffcb2f");
    public static readonly Color CoinDark = Hex("#d99a00");
    public static readonly Color Boost = Hex("#1c9ce6");
    public static readonly Color Heart = Hex("#e8364a");
    public static readonly Color Stone = Hex("#8a9097");
    public static readonly Color StoneLight = Hex("#b1b6bc");
    public static readonly Color Danger = Hex("#e03131");
    public static readonly Color Ink = Hex("#1f2328");
    public static readonly Color HudPanel = Color.FromArgb(200, 22, 26, 33);
    public static readonly Color Accent = Hex("#ffd43b");
    public static readonly Color Good = Hex("#51cf66");

    private static readonly Color[] CarColors = [Hex("#e5484d"), Hex("#12a594"), Hex("#f76b15"), Hex("#6e56cf"), Hex("#0090ff")];

    public static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    public static Color CarColor(int id) => CarColors[Math.Abs(id) % CarColors.Length];

    public static Color CharacterColor(CharacterKind kind) => kind switch
    {
        CharacterKind.Chicken => Hex("#f8f9fa"),
        CharacterKind.Rabbit => Hex("#d8c7b5"),
        CharacterKind.Duck => Hex("#ffd43b"),
        CharacterKind.Pig => Hex("#f7a8b8"),
        _ => Color.White,
    };

    public static SolidBrush Brush(Color color)
    {
        var key = color.ToArgb();
        if (!Brushes.TryGetValue(key, out var brush))
        {
            brush = new SolidBrush(color);
            Brushes[key] = brush;
        }
        return brush;
    }

    public static SolidBrush Brush(Color color, int alpha) =>
        Brush(Color.FromArgb(Math.Clamp(alpha * color.A / 255, 0, 255), color.R, color.G, color.B));

    public static Pen Pen(Color color, float width)
    {
        var key = (color.ToArgb(), MathF.Round(width, 1));
        if (!Pens.TryGetValue(key, out var pen))
        {
            pen = new Pen(color, key.Item2);
            Pens[key] = pen;
        }
        return pen;
    }

    public static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)(color.R * (1 - amount)),
        (int)(color.G * (1 - amount)),
        (int)(color.B * (1 - amount)));
}
