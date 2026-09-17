using System.Drawing.Drawing2D;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

internal sealed record Toast(string Text, MessageTone Tone, long CreatedAt);

/// <summary>Timer, scoreboard, ability bar, toasts and the results card, painted on top of the world.</summary>
internal sealed class HudRenderer
{
    private const int ToastLifetimeMs = 4000;

    private readonly Font _timerFont = new("Segoe UI", 20f, FontStyle.Bold);
    private readonly Font _titleFont = new("Segoe UI", 18f, FontStyle.Bold);
    private readonly Font _boldFont = new("Segoe UI", 9.5f, FontStyle.Bold);
    private readonly Font _font = new("Segoe UI", 9.5f);
    private readonly Font _smallFont = new("Segoe UI", 8f);
    private readonly List<(AbilityKind Kind, RectangleF Bounds)> _abilityButtons = [];

    public AbilityKind? AbilityAt(PointF point)
    {
        foreach (var (kind, bounds) in _abilityButtons)
        {
            if (bounds.Contains(point)) return kind;
        }
        return null;
    }

    public void Draw(Graphics g, SizeF viewport, float scale, SnapshotDto snapshot, string? meId, PlayerRole? role, AbilityKind? selected, long now)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        _abilityButtons.Clear();

        DrawTimer(g, viewport, scale, snapshot);
        DrawStatus(g, scale, snapshot, meId, role);
        DrawScores(g, viewport, scale, snapshot, meId);

        if (role == PlayerRole.Mastermind && snapshot.Mastermind is { } mastermind)
        {
            DrawAbilityBar(g, viewport, scale, mastermind, selected);
        }
        else
        {
            var me = snapshot.Runners.FirstOrDefault(r => r.Id == meId);
            var hint = me?.State switch
            {
                RunnerState.Out => "You're out. Watch the others finish the round.",
                RunnerState.Respawning => "Respawning…",
                _ => "Hop with W A S D or the arrow keys",
            };
            DrawBottomHint(g, viewport, scale, hint);
        }
    }

    public void DrawToasts(Graphics g, SizeF viewport, float scale, IReadOnlyList<Toast> toasts, long now)
    {
        var y = viewport.Height - 150 * scale;
        foreach (var toast in toasts.Where(t => now - t.CreatedAt < ToastLifetimeMs).TakeLast(5).Reverse())
        {
            var age = now - toast.CreatedAt;
            var alpha = age > ToastLifetimeMs - 600 ? (int)(255 * (ToastLifetimeMs - age) / 600f) : 255;
            var accent = toast.Tone switch
            {
                MessageTone.Good => Palette.Good,
                MessageTone.Bad => Palette.Danger,
                _ => Palette.Accent,
            };
            var size = g.MeasureString(toast.Text, _font);
            var box = new RectangleF(14 * scale, y - size.Height - 10 * scale, size.Width + 22 * scale, size.Height + 10 * scale);
            g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel, alpha), box, new SizeF(8 * scale, 8 * scale));
            g.FillRectangle(Palette.Brush(accent, alpha), box.X, box.Y + 6 * scale, 4 * scale, box.Height - 12 * scale);
            g.DrawString(toast.Text, _font, Palette.Brush(Color.White, alpha), box.X + 12 * scale, box.Y + 5 * scale);
            y = box.Y - 6 * scale;
        }
    }

    public void DrawResults(Graphics g, SizeF viewport, float scale, GameResultDto result, string? meId)
    {
        g.FillRectangle(Palette.Brush(Color.FromArgb(150, 0, 0, 0)), 0, 0, viewport.Width, viewport.Height);

        var width = 460 * scale;
        var rowHeight = 30 * scale;
        var height = (150 + result.Ranking.Count * 30 + 40) * scale;
        var card = new RectangleF((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
        g.FillRoundedRectangle(Brushes.White, card, new SizeF(16 * scale, 16 * scale));

        var title = result.WinnerRole == PlayerRole.Mastermind ? "The Mastermind wins!" : $"{result.WinnerName} wins!";
        if (result.WinnerId == meId) title = "You win!";
        var x = card.X + 28 * scale;
        var y = card.Y + 22 * scale;
        g.DrawString(title, _titleFont, Palette.Brush(Palette.Ink), x, y);
        y += 42 * scale;
        g.DrawString(result.Reason, _font, Brushes.DimGray, x, y);
        y += 26 * scale;
        if (result.MastermindName is { } mastermind)
        {
            g.DrawString($"Mastermind: {mastermind}", _smallFont, Brushes.DimGray, x, y);
        }
        y += 30 * scale;

        g.DrawString("Runner", _boldFont, Palette.Brush(Palette.Ink), x, y);
        g.DrawString("Coins", _boldFont, Palette.Brush(Palette.Ink), card.Right - 170 * scale, y);
        g.DrawString("Score", _boldFont, Palette.Brush(Palette.Ink), card.Right - 90 * scale, y);
        y += rowHeight * 0.8f;

        var place = 1;
        foreach (var entry in result.Ranking)
        {
            if (entry.Id == meId) g.FillRectangle(Palette.Brush(Palette.Accent, 90), card.X + 12 * scale, y - 3 * scale, card.Width - 24 * scale, rowHeight - 2 * scale);
            Sprites.DrawCharacter(g, entry.Character, new RectangleF(x, y - 4 * scale, 24 * scale, 24 * scale));
            var status = entry.Survived ? "" : "  (out)";
            g.DrawString($"{place}. {entry.Name}{status}", _font, Palette.Brush(Palette.Ink), x + 32 * scale, y);
            g.DrawString(entry.Coins.ToString(), _font, Palette.Brush(Palette.Ink), card.Right - 170 * scale, y);
            g.DrawString(entry.Score.ToString(), _boldFont, Palette.Brush(Palette.Ink), card.Right - 90 * scale, y);
            y += rowHeight;
            place++;
        }

        g.DrawString("Back to the room in a few seconds…", _smallFont, Brushes.Gray, x, card.Bottom - 30 * scale);
    }

    private void DrawTimer(Graphics g, SizeF viewport, float scale, SnapshotDto snapshot)
    {
        var seconds = (int)Math.Ceiling(snapshot.TimeLeftMs / 1000.0);
        var text = $"{seconds / 60}:{seconds % 60:00}";
        var box = new RectangleF(viewport.Width / 2 - 55 * scale, 10 * scale, 110 * scale, 46 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), box, new SizeF(12 * scale, 12 * scale));
        DrawCentered(g, text, _timerFont, seconds <= 30 ? Palette.Brush(Palette.Hex("#ff6b6b")) : Brushes.White, box);

        var effects = new List<string>();
        if (snapshot.RainMs > 0) effects.Add($"RAIN {Math.Ceiling(snapshot.RainMs / 1000.0)}s");
        if (snapshot.NightMs > 0) effects.Add($"NIGHT {Math.Ceiling(snapshot.NightMs / 1000.0)}s");
        if (effects.Count == 0) return;

        var label = string.Join("   ", effects);
        var size = g.MeasureString(label, _boldFont);
        var chip = new RectangleF(viewport.Width / 2 - size.Width / 2 - 10 * scale, box.Bottom + 6 * scale, size.Width + 20 * scale, size.Height + 6 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.Hex("#364fc7")), chip, new SizeF(10 * scale, 10 * scale));
        DrawCentered(g, label, _boldFont, Brushes.White, chip);
    }

    private void DrawStatus(Graphics g, float scale, SnapshotDto snapshot, string? meId, PlayerRole? role)
    {
        var box = new RectangleF(12 * scale, 10 * scale, 230 * scale, 46 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), box, new SizeF(12 * scale, 12 * scale));

        if (role == PlayerRole.Mastermind)
        {
            g.DrawString("MASTERMIND", _boldFont, Palette.Brush(Palette.Hex("#ff8787")), box.X + 12 * scale, box.Y + 5 * scale);
            g.DrawString("Stop every runner before time runs out", _smallFont, Brushes.Gainsboro, box.X + 12 * scale, box.Y + 24 * scale);
            return;
        }

        var me = snapshot.Runners.FirstOrDefault(r => r.Id == meId);
        if (me is null) return;
        Sprites.DrawCharacter(g, me.Character, new RectangleF(box.X + 8 * scale, box.Y + 6 * scale, 34 * scale, 34 * scale));
        g.DrawString(CharacterCatalog.Get(me.Character).Name.ToUpperInvariant() + (me.Boosted ? "  ⚡ BOOST" : ""), _boldFont, Brushes.White, box.X + 48 * scale, box.Y + 5 * scale);
        for (var i = 0; i < me.Lives; i++)
        {
            Sprites.FillHeart(g, Palette.Brush(Palette.Heart), box.X + (56 + i * 18) * scale, box.Y + 32 * scale, 6.5f * scale);
        }
    }

    private void DrawScores(Graphics g, SizeF viewport, float scale, SnapshotDto snapshot, string? meId)
    {
        var rows = snapshot.Runners.OrderByDescending(r => r.Score).ToList();
        var width = 240 * scale;
        var box = new RectangleF(viewport.Width - width - 12 * scale, 10 * scale, width, (34 + rows.Count * 28) * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), box, new SizeF(12 * scale, 12 * scale));
        g.DrawString("SCOREBOARD", _smallFont, Brushes.Silver, box.X + 12 * scale, box.Y + 8 * scale);

        var y = box.Y + 30 * scale;
        foreach (var runner in rows)
        {
            var isOut = runner.State == RunnerState.Out;
            var textBrush = isOut ? Brushes.Gray : runner.Id == meId ? Palette.Brush(Palette.Accent) : Brushes.White;
            Sprites.DrawCharacter(g, runner.Character, new RectangleF(box.X + 8 * scale, y - 4 * scale, 24 * scale, 24 * scale), alpha: isOut ? 110 : 255);
            var name = runner.Name.Length > 12 ? runner.Name[..12] + "…" : runner.Name;
            g.DrawString(name, runner.Id == meId ? _boldFont : _font, textBrush, box.X + 38 * scale, y);
            var lives = isOut ? (runner.Connected ? "OUT" : "LEFT") : $"♥{runner.Lives}";
            g.DrawString(lives, _smallFont, isOut ? Brushes.Gray : Palette.Brush(Palette.Hex("#ff8787")), box.Right - 88 * scale, y + 2 * scale);
            var score = runner.Score.ToString();
            var scoreWidth = g.MeasureString(score, _boldFont).Width;
            g.DrawString(score, _boldFont, textBrush, box.Right - 12 * scale - scoreWidth, y);
            y += 28 * scale;
        }
    }

    private void DrawAbilityBar(Graphics g, SizeF viewport, float scale, MastermindDto mastermind, AbilityKind? selected)
    {
        var abilities = AbilityCatalog.All;
        var buttonWidth = 128 * scale;
        var buttonHeight = 62 * scale;
        var gap = 8 * scale;
        var total = abilities.Count * buttonWidth + (abilities.Count - 1) * gap;
        var left = (viewport.Width - total) / 2;
        var top = viewport.Height - buttonHeight - 14 * scale;

        // Energy bar
        var bar = new RectangleF(left, top - 26 * scale, total, 16 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), bar, new SizeF(8 * scale, 8 * scale));
        var fill = bar with { Width = bar.Width * mastermind.Energy / mastermind.MaxEnergy };
        if (fill.Width > 1) g.FillRoundedRectangle(Palette.Brush(Palette.Hex("#845ef7")), fill, new SizeF(8 * scale, 8 * scale));
        DrawCentered(g, $"ENERGY {(int)mastermind.Energy}/{(int)mastermind.MaxEnergy}", _smallFont, Brushes.White, bar);

        for (var i = 0; i < abilities.Count; i++)
        {
            var ability = abilities[i];
            var bounds = new RectangleF(left + i * (buttonWidth + gap), top, buttonWidth, buttonHeight);
            _abilityButtons.Add((ability.Kind, bounds));

            var cooldown = mastermind.CooldownsMs.GetValueOrDefault(ability.Kind);
            var usable = cooldown == 0 && mastermind.Energy >= ability.Cost;
            var isSelected = selected == ability.Kind;

            g.FillRoundedRectangle(Palette.Brush(isSelected ? Palette.Hex("#5f3dc4") : Palette.HudPanel), bounds, new SizeF(10 * scale, 10 * scale));
            if (cooldown > 0)
            {
                var portion = Math.Clamp(cooldown / (float)ability.CooldownMs, 0f, 1f);
                g.FillRectangle(Palette.Brush(Color.FromArgb(120, 0, 0, 0)), bounds.X, bounds.Y, bounds.Width, bounds.Height * portion);
            }
            if (isSelected) g.DrawRoundedRectangle(Palette.Pen(Palette.Accent, 2.5f * scale), bounds, new SizeF(10 * scale, 10 * scale));

            var text = usable ? Brushes.White : Brushes.Gray;
            g.DrawString($"{ability.Key}", _boldFont, Palette.Brush(Palette.Accent), bounds.X + 8 * scale, bounds.Y + 6 * scale);
            g.DrawString(ability.Name, _boldFont, text, bounds.X + 24 * scale, bounds.Y + 6 * scale);
            var detail = cooldown > 0 ? $"{Math.Ceiling(cooldown / 1000.0)}s" : $"{ability.Cost} energy";
            g.DrawString(detail, _smallFont, text, bounds.X + 24 * scale, bounds.Y + 26 * scale);
            var target = ability.Target switch
            {
                AbilityTarget.Lane => "click a lane",
                AbilityTarget.Tile => "click a tile",
                _ => "instant",
            };
            g.DrawString(target, _smallFont, Brushes.DarkGray, bounds.X + 24 * scale, bounds.Y + 42 * scale);
        }

        var hint = selected is { } kind
            ? $"{AbilityCatalog.Get(kind).Description}. Click the map to use it, Esc or right-click to cancel."
            : "Press 1 to 5 or click a button to pick an ability";
        var hintSize = g.MeasureString(hint, _font);
        var hintBox = new RectangleF(viewport.Width / 2 - hintSize.Width / 2 - 10 * scale, bar.Y - hintSize.Height - 14 * scale, hintSize.Width + 20 * scale, hintSize.Height + 6 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), hintBox, new SizeF(8 * scale, 8 * scale));
        DrawCentered(g, hint, _font, Brushes.White, hintBox);
    }

    private void DrawBottomHint(Graphics g, SizeF viewport, float scale, string hint)
    {
        var size = g.MeasureString(hint, _font);
        var box = new RectangleF(viewport.Width / 2 - size.Width / 2 - 12 * scale, viewport.Height - size.Height - 22 * scale, size.Width + 24 * scale, size.Height + 8 * scale);
        g.FillRoundedRectangle(Palette.Brush(Palette.HudPanel), box, new SizeF(10 * scale, 10 * scale));
        DrawCentered(g, hint, _font, Brushes.White, box);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, RectangleF box)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, box, format);
    }
}
