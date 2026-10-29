using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client;

/// <summary>A runner's screen: a close-up that follows them, their lives, and hopping. Once out, they watch the whole field.</summary>
public class RunnerView(IGameServer server, IReadOnlyList<(string Text, long At)> messages) : GameView(messages)
{
    private readonly IGameServer _server = server;

    public override bool HandleKey(Keys key)
    {
        var (dx, dy) = key switch
        {
            Keys.W or Keys.Up => (0, 1),
            Keys.S or Keys.Down => (0, -1),
            Keys.A or Keys.Left => (-1, 0),
            Keys.D or Keys.Right => (1, 0),
            _ => (0, 0),
        };
        if (dx == 0 && dy == 0) return false;
        _server.Move(dx, dy);
        return true;
    }

    protected override (float Bottom, float Tile) CameraTarget(GameState state, Player me) =>
        me.Out ? Overview(state) : (me.Lane - 3f, Math.Min(MaxTile, Screen.Height / 11f));

    protected override void DrawRoleHud(Player me, long now)
    {
        DrawAnimal(me.Animal, new RectangleF(StatusBox.X + 6, StatusBox.Y + 4, 36, 36));
        Renderer.Text(me.Animal.ToString().ToUpperInvariant(), 9.5f, Color.White, StatusBox.X + 48, StatusBox.Y + 4, bold: true);
        Renderer.Text(new string('♥', Math.Max(0, me.Lives)), 11f, Color.FromArgb(232, 54, 74), StatusBox.X + 48, StatusBox.Y + 20);

        var hint = me.Out ? "You're out. Watch the others finish the round."
            : me.RespawnAt > 0 ? "Respawning..."
            : "Hop with W A S D or the arrow keys";
        var hintBox = new RectangleF(Screen.Width / 2f - 200, Screen.Height - 46, 400, 32);
        Renderer.FillRect(HudPanel, hintBox);
        Renderer.TextCentered(hint, 9.5f, Color.White, hintBox);
    }
}

/// <summary>The Mastermind's screen: the whole field, the ability bar, and aiming abilities with the mouse.</summary>
public class MastermindView(IGameServer server, IReadOnlyList<(string Text, long At)> messages) : GameView(messages)
{
    private readonly IGameServer _server = server;
    private Ability? _selected;
    private Point _mouse;

    public override void Reset()
    {
        base.Reset();
        _selected = null;
    }

    public override bool HandleKey(Keys key)
    {
        if (key == Keys.Escape) _selected = null;
        else if (key is >= Keys.D1 and <= Keys.D4) Choose((Ability)(key - Keys.D1));
        else if (key == Keys.Z) _server.UndoAbility();
        else return false;
        return true;
    }

    public override void HandleMouseMove(Point location) => _mouse = location;

    public override void HandleMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            _selected = null;
            return;
        }

        foreach (var ability in Enum.GetValues<Ability>())
        {
            if (!AbilityButton(ability).Contains(e.Location)) continue;
            Choose(ability);
            return;
        }

        if (_selected is { } selected)
        {
            var (lane, column) = ScreenToWorld(e.Location);
            _server.UseAbility(selected, lane, column);
        }
    }

    protected override (float Bottom, float Tile) CameraTarget(GameState state, Player me) => Overview(state);

    /// <summary>Shows where the picked ability would land, in red if it can't be used there.</summary>
    protected override void DrawOverlay(GameState state)
    {
        if (_selected is not { } ability) return;

        var (laneIndex, column) = ScreenToWorld(_mouse);
        var lane = state.Lanes.FirstOrDefault(l => l.Index == laneIndex);
        var tile = ability == Ability.Boulder;
        var valid = lane != null && Rules.WorksOn(ability, lane.Type) && (!tile || column is >= 0 and < Rules.GridWidth);
        var area = tile ? Cell(column, laneIndex) : Cell(0, laneIndex, Rules.GridWidth);
        var color = valid ? Color.White : Danger;

        Renderer.FillRect(Color.FromArgb(70, color), area);
        Renderer.OutlineRect(color, 2, area);
    }

    protected override void DrawRoleHud(Player me, long now)
    {
        Renderer.Text("MASTERMIND", 9.5f, Color.FromArgb(255, 135, 135), StatusBox.X + 12, StatusBox.Y + 4, bold: true);
        Renderer.Text("Stop every runner before time runs out", 8f, Color.Gainsboro, StatusBox.X + 12, StatusBox.Y + 24);
        DrawAbilityBar(me, now);
    }

    /// <summary>Rain needs no target, so it fires at once; the others wait for a click on the map.</summary>
    private void Choose(Ability ability)
    {
        if (ability == Ability.Rain)
        {
            _selected = null;
            _server.UseAbility(ability, 0, 0);
            return;
        }
        _selected = _selected == ability ? null : ability;
    }

    private RectangleF AbilityButton(Ability ability)
    {
        const float buttonWidth = 130, buttonHeight = 56, gap = 8;
        var total = 4 * buttonWidth + 3 * gap;
        var left = (Screen.Width - total) / 2 + (int)ability * (buttonWidth + gap);
        return new RectangleF(left, Screen.Height - buttonHeight - 12, buttonWidth, buttonHeight);
    }

    private void DrawAbilityBar(Player me, long now)
    {
        var first = AbilityButton(Ability.Car);
        var last = AbilityButton(Ability.Rain);
        var bar = new RectangleF(first.X, first.Y - 24, last.Right - first.X, 16);
        Renderer.FillRect(HudPanel, bar);
        Renderer.FillRect(Color.FromArgb(132, 94, 247), bar with { Width = bar.Width * (float)(me.Energy / Rules.MaxEnergy) });
        Renderer.TextCentered($"ENERGY {(int)me.Energy}/{(int)Rules.MaxEnergy}", 8f, Color.White, bar);

        foreach (var ability in Enum.GetValues<Ability>())
        {
            var box = AbilityButton(ability);
            var cooldown = Math.Max(0, me.ReadyAt[(int)ability] - now);
            var usable = cooldown == 0 && me.Energy >= Rules.Cost(ability);
            Renderer.FillRect(_selected == ability ? Color.FromArgb(95, 61, 196) : HudPanel, box);
            if (_selected == ability) Renderer.OutlineRect(Accent, 2.5f, box);

            var color = usable ? Color.White : Color.Gray;
            Renderer.Text($"{(int)ability + 1}", 9.5f, Accent, box.X + 8, box.Y + 6, bold: true);
            Renderer.Text(Rules.Name(ability), 9.5f, color, box.X + 24, box.Y + 6, bold: true);
            Renderer.Text(cooldown > 0 ? $"{Math.Ceiling(cooldown / 1000.0)}s" : $"{Rules.Cost(ability)} energy", 8f, color, box.X + 24, box.Y + 30);
        }

        var hint = _selected is { } selected
            ? $"Click the map to use {Rules.Name(selected)}. Esc or right-click to cancel."
            : "Press 1-4 to pick an ability, Z to undo the last one";
        var hintBox = new RectangleF(Screen.Width / 2f - 230, bar.Y - 34, 460, 26);
        Renderer.FillRect(HudPanel, hintBox);
        Renderer.TextCentered(hint, 9.5f, Color.White, hintBox);
    }
}
