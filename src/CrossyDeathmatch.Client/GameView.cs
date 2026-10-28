using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>
/// Bridge (abstraction): what the game screen shows. The map, the runners and the shared parts of the HUD are
/// drawn here; the subclasses add what differs per role (camera, controls, input). How any of it reaches the
/// screen is up to the <see cref="IRenderer"/>, so new roles and new renderers don't need to know about each other.
/// </summary>
public abstract class GameView(IReadOnlyList<(string Text, long At)> messages)
{
    protected static readonly Color HudPanel = Color.FromArgb(200, 22, 26, 33);
    protected static readonly Color Accent = Color.FromArgb(255, 212, 59);
    protected static readonly Color Danger = Color.FromArgb(224, 49, 49);
    protected static readonly RectangleF StatusBox = new(12, 10, 240, 44);
    private static readonly Color GrassLight = Color.FromArgb(155, 215, 112);
    private static readonly Color GrassDark = Color.FromArgb(143, 204, 100);
    private static readonly Color Road = Color.FromArgb(87, 92, 100);
    private static readonly Color Rail = Color.FromArgb(140, 124, 107);
    private static readonly Color Sleeper = Color.FromArgb(93, 74, 59);
    private static readonly Color Steel = Color.FromArgb(207, 211, 216);
    private static readonly Color[] CarColors =
    [
        Color.FromArgb(229, 72, 77), Color.FromArgb(18, 165, 148), Color.FromArgb(247, 107, 21),
        Color.FromArgb(110, 86, 207), Color.FromArgb(0, 144, 255),
    ];

    private readonly IReadOnlyList<(string Text, long At)> _messages = messages;
    private float _cameraBottom = float.NaN;
    private float _tile = 40;
    private float _left;

    /// <summary>Set by the form before every frame, because a renderer wraps that frame's <see cref="Graphics"/>.</summary>
    public IRenderer Renderer { get; set; } = null!;

    /// <summary>The size of the drawing area in pixels, as of the last frame.</summary>
    protected Size Screen { get; private set; }

    /// <summary>The largest tile that still fits the playable columns and a margin across the screen.</summary>
    protected float MaxTile => Screen.Width / (Rules.GridWidth + 2f);

    /// <summary>Called when a round starts.</summary>
    public virtual void Reset() => _cameraBottom = float.NaN;

    /// <summary>Returns false for keys the view doesn't use.</summary>
    public virtual bool HandleKey(Keys key) => false;

    public virtual void HandleMouseDown(MouseEventArgs e) { }

    public virtual void HandleMouseMove(Point location) { }

    public void Draw(GameState state, Player me, long now, double elapsed, Size screen)
    {
        Screen = screen;
        UpdateCamera(state, me);
        Renderer.Clear(GrassDark);

        foreach (var lane in state.Lanes) DrawLane(lane, elapsed, now);
        var runners = state.Players.Where(p => !p.IsMastermind && !p.Out).OrderByDescending(p => p.Lane).ToList();
        foreach (var runner in runners) DrawRunner(runner, now);

        // Shade everything outside the 13 playable columns.
        Renderer.FillRect(Color.FromArgb(75, 0, 0, 0), new RectangleF(0, 0, Math.Max(0, _left), Screen.Height));
        Renderer.FillRect(Color.FromArgb(75, 0, 0, 0), new RectangleF(_left + Rules.GridWidth * _tile, 0, Screen.Width, Screen.Height));

        foreach (var runner in runners) DrawNameTag(runner, runner == me);
        DrawOverlay(state);
        if (now < state.RainUntil) DrawRain();
        DrawHud(state, me, now);
    }

    // ---- what each role adds ----

    /// <summary>Where the camera should head: the lowest lane on screen, and the tile size in pixels.</summary>
    protected abstract (float Bottom, float Tile) CameraTarget(GameState state, Player me);

    /// <summary>Drawn over the map, under the rain and the HUD.</summary>
    protected virtual void DrawOverlay(GameState state) { }

    /// <summary>The inside of <see cref="StatusBox"/>, and the controls along the bottom.</summary>
    protected abstract void DrawRoleHud(Player me, long now);

    /// <summary>Every runner still in the game, or everyone if all of them are out.</summary>
    protected (float Bottom, float Tile) Overview(GameState state)
    {
        var runners = state.Players.Where(p => !p.IsMastermind).ToList();
        var active = runners.Where(r => !r.Out).ToList();
        if (active.Count == 0) active = runners;
        var lowest = active.Count > 0 ? active.Min(r => r.Lane) : 0;
        var highest = active.Count > 0 ? active.Max(r => r.Lane) : 0;
        var bottom = lowest - 3f;
        return (bottom, Math.Min(MaxTile, Screen.Height / Math.Max(13, highest + 9 - bottom)));
    }

    // ---- camera ----

    private void UpdateCamera(GameState state, Player me)
    {
        var (targetBottom, targetTile) = CameraTarget(state, me);
        if (float.IsNaN(_cameraBottom))
        {
            _cameraBottom = targetBottom;
            _tile = targetTile;
        }
        else
        {
            _cameraBottom += (targetBottom - _cameraBottom) * 0.12f;
            _tile += (targetTile - _tile) * 0.12f;
        }
        _left = (Screen.Width - Rules.GridWidth * _tile) / 2;
    }

    private float LaneTop(float lane) => Screen.Height - (lane - _cameraBottom + 1) * _tile;

    protected RectangleF Cell(double x, float lane, double width = 1) =>
        new(_left + (float)x * _tile, LaneTop(lane), (float)width * _tile, _tile);

    protected (int Lane, int Column) ScreenToWorld(Point point) => (
        (int)MathF.Floor(_cameraBottom + (Screen.Height - point.Y) / _tile),
        (int)MathF.Floor((point.X - _left) / _tile));

    // ---- the map ----

    private void DrawLane(Lane lane, double elapsed, long now)
    {
        var t = _tile;
        var top = LaneTop(lane.Index);
        var row = new RectangleF(0, top, Screen.Width, t + 1);

        switch (lane.Type)
        {
            case LaneType.Grass:
                Renderer.FillRect(lane.Index % 2 == 0 ? GrassLight : GrassDark, row);
                break;
            case LaneType.Road:
                Renderer.FillRect(Road, row);
                break;
            case LaneType.Rail:
                Renderer.FillRect(Rail, row);
                for (var column = -4; column < Rules.GridWidth + 4; column++)
                {
                    Renderer.FillRect(Sleeper, new RectangleF(_left + (column + 0.15f) * t, top + t * 0.14f, t * 0.16f, t * 0.72f));
                    Renderer.FillRect(Sleeper, new RectangleF(_left + (column + 0.65f) * t, top + t * 0.14f, t * 0.16f, t * 0.72f));
                }
                Renderer.FillRect(Steel, new RectangleF(0, top + t * 0.26f, Screen.Width, t * 0.07f));
                Renderer.FillRect(Steel, new RectangleF(0, top + t * 0.66f, Screen.Width, t * 0.07f));
                break;
        }

        foreach (var column in lane.Trees)
        {
            var cell = Cell(column, lane.Index);
            Renderer.FillRect(Color.FromArgb(122, 82, 48), new RectangleF(cell.X + t * 0.42f, cell.Y + t * 0.55f, t * 0.16f, t * 0.3f));
            Renderer.FillRect(Color.FromArgb(47, 138, 62), new RectangleF(cell.X + t * 0.15f, cell.Y + t * 0.05f, t * 0.7f, t * 0.55f));
        }

        foreach (var column in lane.Coins)
        {
            var cell = Cell(column, lane.Index);
            Renderer.FillEllipse(Color.FromArgb(217, 154, 0), new RectangleF(cell.X + t * 0.28f, cell.Y + t * 0.28f, t * 0.44f, t * 0.44f));
            Renderer.FillEllipse(Color.FromArgb(255, 203, 47), new RectangleF(cell.X + t * 0.33f, cell.Y + t * 0.33f, t * 0.34f, t * 0.34f));
        }

        foreach (var obstacle in lane.Obstacles)
        {
            // The server sends 20 updates a second; move things on by their speed so they glide between updates.
            var x = obstacle.X + (obstacle.Kind == ObstacleKind.Boulder ? 0 : obstacle.Speed * elapsed);
            DrawObstacle(obstacle, Cell(x, lane.Index, obstacle.Length), CarColors[Math.Abs(lane.Index) % CarColors.Length], now);
        }

        if (lane.Type == LaneType.Rail)
        {
            var warning = lane.Obstacles.Any(o => o.Kind == ObstacleKind.Train) || now >= lane.NextTrainAt - Rules.TrainWarningMs;
            var lit = warning && Environment.TickCount64 / 180 % 2 == 0;
            Renderer.FillEllipse(Color.FromArgb(31, 35, 40), new RectangleF(_left - t * 0.5f, top + t * 0.1f, t * 0.4f, t * 0.4f));
            Renderer.FillEllipse(lit ? Danger : Color.FromArgb(92, 26, 26), new RectangleF(_left - t * 0.44f, top + t * 0.16f, t * 0.28f, t * 0.28f));
        }
    }

    private void DrawObstacle(Obstacle obstacle, RectangleF area, Color carColor, long now)
    {
        var t = _tile;
        switch (obstacle.Kind)
        {
            case ObstacleKind.Car:
            case ObstacleKind.Truck:
                var body = new RectangleF(area.X + t * 0.06f, area.Y + t * 0.18f, area.Width - t * 0.12f, t * 0.62f);
                Renderer.FillRect(Color.FromArgb(31, 35, 40), new RectangleF(body.X + t * 0.1f, body.Y - t * 0.06f, body.Width - t * 0.2f, body.Height + t * 0.12f));
                Renderer.FillRect(obstacle.Kind == ObstacleKind.Truck ? Color.FromArgb(233, 236, 239) : carColor, body);
                var cabWidth = obstacle.Kind == ObstacleKind.Truck ? t * 0.7f : body.Width;
                var cabX = obstacle.Speed >= 0 ? body.Right - cabWidth : body.X;
                Renderer.FillRect(carColor, new RectangleF(cabX, body.Y, cabWidth, body.Height));
                Renderer.FillRect(Color.FromArgb(208, 235, 255), new RectangleF(cabX + cabWidth * 0.3f, body.Y + body.Height * 0.15f, cabWidth * 0.4f, body.Height * 0.7f));
                break;

            case ObstacleKind.Train:
                Renderer.FillRect(Color.FromArgb(241, 243, 245), new RectangleF(area.X, area.Y + t * 0.1f, area.Width, t * 0.8f));
                Renderer.FillRect(Color.FromArgb(25, 113, 194), new RectangleF(area.X, area.Y + t * 0.42f, area.Width, t * 0.14f));
                var engineX = obstacle.Speed >= 0 ? area.Right - 3 * t : area.X;
                Renderer.FillRect(Color.FromArgb(201, 42, 42), new RectangleF(engineX, area.Y + t * 0.1f, 3 * t, t * 0.8f));
                break;

            case ObstacleKind.Fox:
                var orange = Color.FromArgb(247, 103, 7);
                Renderer.FillRect(orange, new RectangleF(area.X + t * 0.15f, area.Y + t * 0.3f, t * 0.7f, t * 0.42f));
                var headX = obstacle.Speed >= 0 ? area.X + t * 0.6f : area.X + t * 0.08f;
                Renderer.FillRect(orange, new RectangleF(headX, area.Y + t * 0.18f, t * 0.32f, t * 0.34f));
                Renderer.FillRect(Color.White, new RectangleF(headX, area.Y + t * 0.4f, t * 0.32f, t * 0.12f));
                break;

            case ObstacleKind.Boulder when !obstacle.Landed:
                // Still falling: show where it will land.
                var pulse = (int)(100 * (MathF.Sin(now / 80f) + 1) / 2);
                Renderer.OutlineEllipse(Color.FromArgb(150 + pulse, Danger), t * 0.07f, new RectangleF(area.X + t * 0.12f, area.Y + t * 0.12f, t * 0.76f, t * 0.76f));
                break;

            case ObstacleKind.Boulder:
                Renderer.FillEllipse(Color.FromArgb(138, 144, 151), new RectangleF(area.X + t * 0.08f, area.Y + t * 0.1f, t * 0.84f, t * 0.74f));
                Renderer.FillEllipse(Color.FromArgb(177, 182, 188), new RectangleF(area.X + t * 0.22f, area.Y + t * 0.16f, t * 0.34f, t * 0.22f));
                break;
        }
    }

    // ---- runners ----

    private void DrawRunner(Player runner, long now)
    {
        var respawning = runner.RespawnAt > 0;
        var blinking = now < runner.InvulnerableUntil && Environment.TickCount64 / 120 % 2 == 0;
        var alpha = respawning ? 90 : blinking ? 120 : 255;
        DrawAnimal(runner.Animal, Cell(runner.X, runner.Lane), alpha);
    }

    protected void DrawAnimal(Animal animal, RectangleF cell, int alpha = 255)
    {
        var s = cell.Width * 0.62f;
        var left = cell.X + (cell.Width - s) / 2;
        var top = cell.Y + cell.Height * 0.86f - s;
        void Box(Color color, float x, float y, float w, float h) =>
            Renderer.FillRect(Color.FromArgb(alpha, color), new RectangleF(left + x * s, top + y * s, w * s, h * s));

        var body = animal switch
        {
            Animal.Chicken => Color.FromArgb(248, 249, 250),
            Animal.Rabbit => Color.FromArgb(216, 199, 181),
            Animal.Duck => Color.FromArgb(255, 212, 59),
            _ => Color.FromArgb(247, 168, 184),
        };

        switch (animal)
        {
            case Animal.Chicken:
                Box(Color.FromArgb(224, 49, 49), 0.38f, -0.2f, 0.24f, 0.24f); // comb
                break;
            case Animal.Rabbit:
                Box(body, 0.14f, -0.5f, 0.2f, 0.55f); // ears
                Box(body, 0.66f, -0.5f, 0.2f, 0.55f);
                break;
        }
        Box(body, 0, 0, 1, 1);
        switch (animal)
        {
            case Animal.Chicken:
            case Animal.Duck:
                Box(Color.FromArgb(247, 103, 7), 0.3f, 0.42f, 0.4f, 0.18f); // beak
                break;
            case Animal.Pig:
                Box(Color.FromArgb(232, 131, 155), 0.3f, 0.42f, 0.4f, 0.26f); // snout
                break;
        }
        Box(Color.FromArgb(31, 35, 40), 0.18f, 0.22f, 0.14f, 0.14f); // eyes
        Box(Color.FromArgb(31, 35, 40), 0.68f, 0.22f, 0.14f, 0.14f);
    }

    private void DrawNameTag(Player runner, bool isMe)
    {
        var label = runner.RespawnAt > 0 ? $"{runner.Name} (respawning)" : runner.Name;
        var size = Renderer.MeasureText(label, 8f, bold: true);
        var cell = Cell(runner.X, runner.Lane);
        var box = new RectangleF(cell.X + cell.Width / 2 - size.Width / 2 - 4, cell.Y - size.Height, size.Width + 8, size.Height);
        Renderer.FillRect(isMe ? Accent : Color.FromArgb(170, 20, 24, 30), box);
        Renderer.Text(label, 8f, isMe ? Color.Black : Color.White, box.X + 4, box.Y, bold: true);
    }

    private void DrawRain()
    {
        Renderer.FillRect(Color.FromArgb(40, 30, 50, 90), new RectangleF(0, 0, Screen.Width, Screen.Height));
        var drop = Color.FromArgb(140, 205, 225, 255);
        var time = Environment.TickCount64;
        for (var i = 0; i < 160; i++)
        {
            var x = i * 7919 % 1000 / 1000f * (Screen.Width + 40) - 20;
            var y = (i * 104729 % 1000 / 1000f * (Screen.Height + 40) + time * 0.9f + i * 13) % (Screen.Height + 40) - 20;
            Renderer.Line(drop, 1.4f, new PointF(x, y), new PointF(x - 4, y + 14));
        }
    }

    // ---- HUD ----

    private void DrawHud(GameState state, Player me, long now)
    {
        float width = Screen.Width, height = Screen.Height;

        // Timer, and rain if it is falling.
        var seconds = (int)Math.Ceiling(state.TimeLeftMs / 1000.0);
        var timerBox = new RectangleF(width / 2 - 55, 10, 110, 44);
        Renderer.FillRect(HudPanel, timerBox);
        Renderer.TextCentered($"{seconds / 60}:{seconds % 60:00}", 20f, seconds <= 30 ? Color.FromArgb(255, 107, 107) : Color.White, timerBox);
        if (now < state.RainUntil)
        {
            var rainBox = new RectangleF(width / 2 - 55, 60, 110, 26);
            Renderer.FillRect(Color.FromArgb(54, 79, 199), rainBox);
            Renderer.TextCentered($"RAIN {Math.Ceiling((state.RainUntil - now) / 1000.0)}s", 9.5f, Color.White, rainBox);
        }

        // Scoreboard.
        var rows = state.Players.Where(p => !p.IsMastermind).OrderByDescending(p => p.Score).ToList();
        var board = new RectangleF(width - 252, 10, 240, 34 + rows.Count * 26);
        Renderer.FillRect(HudPanel, board);
        Renderer.Text("SCOREBOARD", 8f, Color.Silver, board.X + 12, board.Y + 8);
        var y = board.Y + 30;
        foreach (var runner in rows)
        {
            var color = runner.Out ? Color.Gray : runner == me ? Accent : Color.White;
            DrawAnimal(runner.Animal, new RectangleF(board.X + 8, y - 4, 22, 22), runner.Out ? 110 : 255);
            Renderer.Text(runner.Name, 9.5f, color, board.X + 36, y);
            Renderer.Text(runner.Out ? runner.Left ? "LEFT" : "OUT" : $"x{runner.Lives}", 8f, color, board.Right - 90, y + 2);
            Renderer.Text(runner.Score.ToString(), 9.5f, color, board.Right - 40, y, bold: true);
            y += 26;
        }

        // Who you are, and what you can do.
        Renderer.FillRect(HudPanel, StatusBox);
        DrawRoleHud(me, now);

        // Recent messages.
        var messageY = height - 150f;
        foreach (var (text, _) in _messages.Where(m => Environment.TickCount64 - m.At < 4000).TakeLast(4).Reverse())
        {
            var size = Renderer.MeasureText(text, 9.5f);
            var box = new RectangleF(14, messageY - size.Height - 8, size.Width + 16, size.Height + 8);
            Renderer.FillRect(HudPanel, box);
            Renderer.Text(text, 9.5f, Color.White, box.X + 8, box.Y + 4);
            messageY = box.Y - 6;
        }
    }
}
