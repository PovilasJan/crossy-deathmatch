using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

/// <summary>
/// Maps world tiles to screen pixels. Lane numbers grow upwards (forward), so lane <see cref="Bottom"/>
/// sits at the bottom edge of the window. Follows its target smoothly.
/// </summary>
internal sealed class Camera
{
    private bool _initialized;

    public float Tile { get; private set; } = 40;
    public float Bottom { get; private set; }
    public float OffsetX { get; private set; }
    public float Height { get; private set; }

    public void Follow(float targetBottom, float targetTile, SizeF viewport, float dt)
    {
        if (!_initialized)
        {
            Bottom = targetBottom;
            Tile = targetTile;
            _initialized = true;
        }
        else
        {
            var k = Math.Min(1f, dt * 6f);
            Bottom += (targetBottom - Bottom) * k;
            Tile += (targetTile - Tile) * k;
        }

        Height = viewport.Height;
        OffsetX = (viewport.Width - GameRules.GridWidth * Tile) / 2;
    }

    public void Reset() => _initialized = false;

    public float LaneTop(float lane) => Height - (lane - Bottom + 1) * Tile;

    public float ColumnLeft(float column) => OffsetX + column * Tile;

    public RectangleF TileRect(float column, float lane, float width = 1) =>
        new(ColumnLeft(column), LaneTop(lane), Tile * width, Tile);

    public (int From, int To) VisibleLanes() =>
        ((int)MathF.Floor(Bottom) - 1, (int)MathF.Ceiling(Bottom + Height / Tile) + 1);

    public (int Lane, int Column) ScreenToWorld(PointF point) =>
        ((int)MathF.Floor(Bottom + (Height - point.Y) / Tile), (int)MathF.Floor((point.X - OffsetX) / Tile));
}
