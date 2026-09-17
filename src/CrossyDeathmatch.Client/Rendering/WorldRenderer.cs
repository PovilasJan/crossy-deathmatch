using System.Drawing.Drawing2D;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

internal sealed record ViewOptions(string? MeId, PlayerRole? Role, AbilityKind? SelectedAbility, PointF? Mouse);

/// <summary>
/// Draws the map, obstacles and animals. Runners see a close camera that follows them;
/// the Mastermind (and anyone knocked out) sees a zoomed-out view of the whole pack.
/// </summary>
internal sealed class WorldRenderer
{
    private readonly RunnerAnimator _animator = new();
    private readonly Font _tagFont = new("Segoe UI", 8f, FontStyle.Bold);

    public Camera Camera { get; } = new();

    public void Reset()
    {
        Camera.Reset();
        _animator.Clear();
    }

    public void Draw(Graphics g, SizeF viewport, SnapshotDto snapshot, ViewOptions view, long now, float dt)
    {
        var me = snapshot.Runners.FirstOrDefault(r => r.Id == view.MeId);
        var followingMe = view.Role == PlayerRole.Runner && me is { State: not RunnerState.Out };
        UpdateCamera(snapshot, viewport, followingMe ? me : null, dt);

        var lanes = snapshot.Lanes.ToDictionary(l => l.Index);
        var (from, to) = Camera.VisibleLanes();
        var t = Camera.Tile;

        g.SmoothingMode = SmoothingMode.None;
        g.Clear(Palette.GrassDark);
        for (var index = from; index <= to; index++) DrawGround(g, viewport, lanes.GetValueOrDefault(index), lanes.GetValueOrDefault(index + 1), index, now);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var obstacles = snapshot.Obstacles.Where(o => o.Lane >= from && o.Lane <= to).ToList();
        foreach (var log in obstacles.Where(o => o.Kind == ObstacleKind.Log))
        {
            Sprites.DrawLog(g, Camera.TileRect(log.X, log.Lane, log.Length));
        }

        for (var index = from; index <= to; index++)
        {
            if (!lanes.TryGetValue(index, out var lane)) continue;
            foreach (var column in lane.Trees) Sprites.DrawTree(g, Camera.TileRect(column, index));
            foreach (var pickup in lane.Pickups) Sprites.DrawPickup(g, pickup.Kind, Camera.TileRect(pickup.X, index), now);
        }

        foreach (var boulder in obstacles.Where(o => o.Kind == ObstacleKind.Boulder && !o.Landed))
        {
            Sprites.DrawBoulder(g, Camera.TileRect(boulder.X, boulder.Lane), landed: false, now);
        }

        // Animals, higher lanes first so nearer ones overlap them.
        var placed = snapshot.Runners
            .Where(r => r.State != RunnerState.Out)
            .Select(r => (Runner: r, Pose: _animator.Resolve(r, now)))
            .OrderByDescending(p => p.Pose.Lane)
            .ToList();
        PointF? myScreenPosition = null;
        foreach (var (runner, (x, lane, jump)) in placed)
        {
            var tile = Camera.TileRect(x, lane);
            var blinking = runner.Invulnerable && now / 120 % 2 == 0;
            var alpha = runner.State == RunnerState.Respawning ? 90 : blinking ? 120 : 255;
            Sprites.DrawCharacter(g, runner.Character, tile, jump, alpha);
            if (runner.Boosted) g.DrawEllipse(Palette.Pen(Color.FromArgb(160, Palette.Boost), t * 0.05f), tile.X + t * 0.05f, tile.Y + t * 0.05f, t * 0.9f, t * 0.9f);
            if (runner.Id == view.MeId) myScreenPosition = new PointF(tile.X + t / 2, tile.Y + t / 2);
        }

        foreach (var obstacle in obstacles)
        {
            var area = Camera.TileRect(obstacle.X, obstacle.Lane, obstacle.Length);
            switch (obstacle.Kind)
            {
                case ObstacleKind.Car or ObstacleKind.Truck: Sprites.DrawVehicle(g, area, obstacle); break;
                case ObstacleKind.Train: Sprites.DrawTrain(g, area, obstacle.Heading); break;
                case ObstacleKind.Fox: Sprites.DrawFox(g, area, obstacle.Heading); break;
                case ObstacleKind.Boulder when obstacle.Landed: Sprites.DrawBoulder(g, area, landed: true, now); break;
            }
        }

        // Shade the area outside the playable columns.
        var edge = Palette.Brush(Color.Black, 75);
        g.FillRectangle(edge, 0, 0, Math.Max(0, Camera.OffsetX), viewport.Height);
        g.FillRectangle(edge, Camera.ColumnLeft(GameRules.GridWidth), 0, viewport.Width, viewport.Height);

        foreach (var lane in lanes.Values.Where(l => l.Type == LaneType.Rail && l.Index >= from && l.Index <= to))
        {
            Sprites.DrawRailSignal(g, Camera.OffsetX - t * 0.5f, Camera.LaneTop(lane.Index), t, lane.Warning, now);
        }

        foreach (var (runner, (x, lane, jump)) in placed) DrawNameTag(g, runner, x, lane, jump, runner.Id == view.MeId);

        if (view.Role == PlayerRole.Mastermind) DrawTargeting(g, view, lanes);
        if (snapshot.RainMs > 0) DrawRain(g, viewport, now);
        if (snapshot.NightMs > 0)
        {
            if (followingMe) DrawNight(g, viewport, myScreenPosition, t);
            else g.FillRectangle(Palette.Brush(Color.FromArgb(70, 10, 15, 45)), 0, 0, viewport.Width, viewport.Height);
        }
    }

    private void UpdateCamera(SnapshotDto snapshot, SizeF viewport, RunnerDto? me, float dt)
    {
        var widthTile = viewport.Width / (GameRules.GridWidth + 2);
        if (me is not null)
        {
            Camera.Follow(me.Lane - 3, Math.Min(widthTile, viewport.Height / 11), viewport, dt);
            return;
        }

        // Overview: fit every runner still in the game, with room ahead of the leader.
        var active = snapshot.Runners.Where(r => r.State != RunnerState.Out).ToList();
        if (active.Count == 0) active = snapshot.Runners;
        var lowest = active.Count > 0 ? active.Min(r => r.Lane) : 0;
        var highest = active.Count > 0 ? active.Max(r => r.Lane) : 0;
        var bottom = lowest - 3;
        var span = Math.Max(13, highest + 9 - bottom);
        Camera.Follow(bottom, Math.Min(widthTile, viewport.Height / span), viewport, dt);
    }

    private void DrawGround(Graphics g, SizeF viewport, LaneDto? lane, LaneDto? above, int index, long now)
    {
        var t = Camera.Tile;
        var top = Camera.LaneTop(index);
        var row = new RectangleF(0, top, viewport.Width, t + 1);

        switch (lane?.Type ?? LaneType.Grass)
        {
            case LaneType.Grass:
                g.FillRectangle(Palette.Brush((index & 1) == 0 ? Palette.GrassLight : Palette.GrassDark), row);
                break;

            case LaneType.Road:
                g.FillRectangle(Palette.Brush(Palette.Road), row);
                if (above?.Type == LaneType.Road)
                {
                    for (var column = -4; column < GameRules.GridWidth + 4; column++)
                    {
                        g.FillRectangle(Palette.Brush(Palette.RoadLine), Camera.ColumnLeft(column) + t * 0.3f, top - t * 0.025f, t * 0.4f, t * 0.05f);
                    }
                }
                break;

            case LaneType.River:
                g.FillRectangle(Palette.Brush(Palette.River), row);
                var drift = now / 1000f * 0.35f % 2f;
                for (var column = -6; column < GameRules.GridWidth + 6; column += 2)
                {
                    var y = top + t * ((column + index & 1) == 0 ? 0.3f : 0.68f);
                    g.FillRectangle(Palette.Brush(Palette.RiverLight), Camera.ColumnLeft(column + drift) + t * 0.2f, y, t * 0.55f, t * 0.06f);
                }
                break;

            case LaneType.Rail:
                g.FillRectangle(Palette.Brush(Palette.Rail), row);
                for (var column = -4; column < GameRules.GridWidth + 4; column++)
                {
                    g.FillRectangle(Palette.Brush(Palette.RailSleeper), Camera.ColumnLeft(column) + t * 0.12f, top + t * 0.14f, t * 0.16f, t * 0.72f);
                    g.FillRectangle(Palette.Brush(Palette.RailSleeper), Camera.ColumnLeft(column) + t * 0.62f, top + t * 0.14f, t * 0.16f, t * 0.72f);
                }
                g.FillRectangle(Palette.Brush(Palette.RailSteel), 0, top + t * 0.26f, viewport.Width, t * 0.07f);
                g.FillRectangle(Palette.Brush(Palette.RailSteel), 0, top + t * 0.66f, viewport.Width, t * 0.07f);
                break;
        }
    }

    private void DrawNameTag(Graphics g, RunnerDto runner, float x, float lane, float jump, bool isMe)
    {
        var t = Camera.Tile;
        var label = runner.State == RunnerState.Respawning ? $"{runner.Name} (respawning)" : runner.Name;
        var size = g.MeasureString(label, _tagFont);
        var centerX = Camera.ColumnLeft(x) + t / 2;
        var top = Camera.LaneTop(lane) - size.Height - t * 0.05f - jump * t * 0.4f;
        var box = new RectangleF(centerX - size.Width / 2 - 4, top, size.Width + 8, size.Height);

        g.FillRoundedRectangle(Palette.Brush(isMe ? Palette.Accent : Color.FromArgb(170, 20, 24, 30)), box, new SizeF(6, 6));
        g.DrawString(label, _tagFont, isMe ? Palette.Brush(Palette.Ink) : Brushes.White, box.X + 4, box.Y);
    }

    private void DrawTargeting(Graphics g, ViewOptions view, Dictionary<int, LaneDto> lanes)
    {
        if (view.SelectedAbility is not { } kind || view.Mouse is not { } mouse) return;
        var ability = AbilityCatalog.Get(kind);
        if (ability.Target == AbilityTarget.None) return;

        var (laneIndex, column) = Camera.ScreenToWorld(mouse);
        var valid = lanes.TryGetValue(laneIndex, out var lane) && ability.WorksOn(lane.Type) &&
                    (ability.Target == AbilityTarget.Lane || column is >= 0 and < GameRules.GridWidth);
        var color = valid ? Color.White : Palette.Danger;
        var rect = ability.Target == AbilityTarget.Lane
            ? Camera.TileRect(0, laneIndex, GameRules.GridWidth)
            : Camera.TileRect(column, laneIndex);

        g.FillRectangle(Palette.Brush(color, 70), rect);
        g.DrawRectangle(Palette.Pen(color, 2), rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static void DrawRain(Graphics g, SizeF viewport, long now)
    {
        g.FillRectangle(Palette.Brush(Color.FromArgb(40, 30, 50, 90)), 0, 0, viewport.Width, viewport.Height);
        var pen = Palette.Pen(Color.FromArgb(140, 205, 225, 255), 1.4f);
        for (var i = 0; i < 160; i++)
        {
            var x = (i * 7919 % 1000) / 1000f * (viewport.Width + 40) - 20;
            var y = ((i * 104729 % 1000) / 1000f * (viewport.Height + 40) + now * 0.9f + i * 13) % (viewport.Height + 40) - 20;
            g.DrawLine(pen, x, y, x - 4, y + 14);
        }
    }

    private static void DrawNight(Graphics g, SizeF viewport, PointF? center, float tile)
    {
        var dark = Color.FromArgb(238, 6, 8, 24);
        if (center is not { } c)
        {
            g.FillRectangle(Palette.Brush(dark), 0, 0, viewport.Width, viewport.Height);
            return;
        }

        var radius = tile * 3.2f;
        var circle = new RectangleF(c.X - radius, c.Y - radius, radius * 2, radius * 2);
        using var path = new GraphicsPath();
        path.AddEllipse(circle);
        using var outside = new Region(new RectangleF(0, 0, viewport.Width, viewport.Height));
        outside.Exclude(path);
        g.FillRegion(Palette.Brush(dark), outside);

        using var glow = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb(0, dark),
            SurroundColors = [dark],
            FocusScales = new PointF(0.3f, 0.3f),
        };
        g.FillEllipse(glow, circle);
    }
}
