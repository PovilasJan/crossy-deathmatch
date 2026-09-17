using System.Drawing.Drawing2D;
using CrossyDeathmatch.Shared;

namespace CrossyDeathmatch.Client.Rendering;

/// <summary>Blocky, Crossy-Road-ish sprites drawn from rectangles and ellipses. Each takes the tile it occupies.</summary>
internal static class Sprites
{
    public static void DrawCharacter(Graphics g, CharacterKind kind, RectangleF tile, float jump = 0f, int alpha = 255)
    {
        var t = tile.Width;
        var s = t * 0.62f;
        var centerX = tile.X + t / 2;
        var feet = tile.Y + t * 0.86f;
        var shadowWidth = s * (0.95f - jump * 0.35f);
        g.FillEllipse(Palette.Brush(Color.Black, alpha * 60 / 255), centerX - shadowWidth / 2, feet - s * 0.1f, shadowWidth, s * 0.22f);

        var left = centerX - s / 2;
        var top = feet - s - jump * t * 0.4f;
        void Box(Color color, float x, float y, float w, float h) =>
            g.FillRectangle(Palette.Brush(color, alpha), left + x * s, top + y * s, w * s, h * s);

        var body = Palette.CharacterColor(kind);
        var eye = Palette.Ink;
        switch (kind)
        {
            case CharacterKind.Chicken:
                Box(Palette.Hex("#e03131"), 0.38f, -0.2f, 0.24f, 0.24f);
                Box(body, 0, 0, 1, 1);
                Box(Palette.Hex("#dee2e6"), 0, 0.8f, 1, 0.2f);
                Box(Palette.Hex("#f59f00"), 0.38f, 0.42f, 0.24f, 0.16f);
                Box(Palette.Hex("#e03131"), 0.44f, 0.58f, 0.12f, 0.12f);
                break;
            case CharacterKind.Rabbit:
                Box(body, 0.14f, -0.5f, 0.2f, 0.55f);
                Box(body, 0.66f, -0.5f, 0.2f, 0.55f);
                Box(Palette.Hex("#f3a6b6"), 0.19f, -0.42f, 0.1f, 0.4f);
                Box(Palette.Hex("#f3a6b6"), 0.71f, -0.42f, 0.1f, 0.4f);
                Box(body, 0, 0, 1, 1);
                Box(Palette.Hex("#bfae9b"), 0, 0.8f, 1, 0.2f);
                Box(Palette.Hex("#e8839b"), 0.43f, 0.46f, 0.14f, 0.1f);
                break;
            case CharacterKind.Duck:
                Box(body, 0.44f, -0.14f, 0.12f, 0.16f);
                Box(body, 0, 0, 1, 1);
                Box(Palette.Hex("#fab005"), 0, 0.8f, 1, 0.2f);
                Box(Palette.Hex("#f76707"), 0.26f, 0.42f, 0.48f, 0.18f);
                break;
            case CharacterKind.Pig:
                Box(Palette.Hex("#e8839b"), 0.06f, -0.1f, 0.22f, 0.18f);
                Box(Palette.Hex("#e8839b"), 0.72f, -0.1f, 0.22f, 0.18f);
                Box(body, 0, 0, 1, 1);
                Box(Palette.Hex("#ee8fa4"), 0, 0.8f, 1, 0.2f);
                Box(Palette.Hex("#e8839b"), 0.3f, 0.42f, 0.4f, 0.26f);
                Box(Palette.Hex("#a8435a"), 0.38f, 0.5f, 0.07f, 0.1f);
                Box(Palette.Hex("#a8435a"), 0.55f, 0.5f, 0.07f, 0.1f);
                break;
        }

        Box(eye, 0.18f, 0.22f, 0.14f, 0.14f);
        Box(eye, 0.68f, 0.22f, 0.14f, 0.14f);
    }

    public static void DrawTree(Graphics g, RectangleF tile)
    {
        var t = tile.Width;
        g.FillEllipse(Palette.Brush(Color.Black, 45), tile.X + t * 0.16f, tile.Y + t * 0.74f, t * 0.68f, t * 0.18f);
        g.FillRectangle(Palette.Brush(Palette.Trunk), tile.X + t * 0.42f, tile.Y + t * 0.55f, t * 0.16f, t * 0.3f);
        g.FillRectangle(Palette.Brush(Palette.Tree), tile.X + t * 0.15f, tile.Y + t * 0.02f, t * 0.7f, t * 0.58f);
        g.FillRectangle(Palette.Brush(Palette.TreeLight), tile.X + t * 0.15f, tile.Y + t * 0.02f, t * 0.7f, t * 0.16f);
    }

    public static void DrawPickup(Graphics g, PickupKind kind, RectangleF tile, long now)
    {
        var t = tile.Width;
        var bob = MathF.Sin(now / 220f + tile.X * 0.05f) * t * 0.05f;
        var r = t * 0.22f;
        var cx = tile.X + t / 2;
        var cy = tile.Y + t / 2 + bob;

        g.FillEllipse(Palette.Brush(Color.Black, 40), cx - r, tile.Y + t * 0.72f, r * 2, r * 0.6f);
        switch (kind)
        {
            case PickupKind.Coin:
                g.FillEllipse(Palette.Brush(Palette.CoinDark), cx - r, cy - r, r * 2, r * 2);
                g.FillEllipse(Palette.Brush(Palette.Coin), cx - r * 0.8f, cy - r * 0.8f, r * 1.6f, r * 1.6f);
                g.FillRectangle(Palette.Brush(Palette.CoinDark), cx - r * 0.12f, cy - r * 0.5f, r * 0.24f, r);
                break;
            case PickupKind.Speed:
                g.FillEllipse(Palette.Brush(Palette.Boost), cx - r * 1.1f, cy - r * 1.1f, r * 2.2f, r * 2.2f);
                PointF[] bolt =
                [
                    new(cx + r * 0.15f, cy - r * 0.85f), new(cx - r * 0.5f, cy + r * 0.1f), new(cx - r * 0.02f, cy + r * 0.1f),
                    new(cx - r * 0.2f, cy + r * 0.85f), new(cx + r * 0.5f, cy - r * 0.15f), new(cx + r * 0.02f, cy - r * 0.15f),
                ];
                g.FillPolygon(Brushes.White, bolt);
                break;
            case PickupKind.Life:
                FillHeart(g, Palette.Brush(Palette.Heart), cx, cy, r * 1.1f);
                break;
        }
    }

    public static void FillHeart(Graphics g, Brush brush, float cx, float cy, float r)
    {
        using var path = new GraphicsPath();
        path.AddBezier(cx, cy + r * 0.9f, cx - r * 1.5f, cy - r * 0.1f, cx - r * 0.6f, cy - r * 1.2f, cx, cy - r * 0.35f);
        path.AddBezier(cx, cy - r * 0.35f, cx + r * 0.6f, cy - r * 1.2f, cx + r * 1.5f, cy - r * 0.1f, cx, cy + r * 0.9f);
        g.FillPath(brush, path);
    }

    public static void DrawVehicle(Graphics g, RectangleF area, ObstacleDto vehicle)
    {
        var t = area.Height;
        var body = new RectangleF(area.X + t * 0.06f, area.Y + t * 0.17f, area.Width - t * 0.12f, t * 0.62f);
        var color = Palette.CarColor(vehicle.Id);

        g.FillEllipse(Palette.Brush(Color.Black, 50), body.X, body.Bottom - t * 0.06f, body.Width, t * 0.16f);
        foreach (var fraction in new[] { 0.12f, 0.7f })
        {
            var wheelX = body.X + body.Width * fraction;
            g.FillRectangle(Palette.Brush(Palette.Ink), wheelX, body.Y - t * 0.05f, t * 0.2f, t * 0.08f);
            g.FillRectangle(Palette.Brush(Palette.Ink), wheelX, body.Bottom - t * 0.03f, t * 0.2f, t * 0.08f);
        }

        if (vehicle.Kind == ObstacleKind.Truck)
        {
            var cabWidth = t * 0.72f;
            var cabX = vehicle.Heading >= 0 ? body.Right - cabWidth : body.X;
            var cargoX = vehicle.Heading >= 0 ? body.X : body.X + cabWidth + t * 0.05f;
            g.FillRoundedRectangle(Palette.Brush(Palette.Hex("#e9ecef")), new RectangleF(cargoX, body.Y, body.Width - cabWidth - t * 0.05f, body.Height), new SizeF(t * 0.1f, t * 0.1f));
            g.FillRectangle(Palette.Brush(Palette.Hex("#ced4da")), cargoX, body.Y + body.Height * 0.8f, body.Width - cabWidth - t * 0.05f, body.Height * 0.2f);
            g.FillRoundedRectangle(Palette.Brush(color), new RectangleF(cabX, body.Y, cabWidth, body.Height), new SizeF(t * 0.14f, t * 0.14f));
            g.FillRectangle(Palette.Brush(Palette.Hex("#d0ebff")), cabX + cabWidth * (vehicle.Heading >= 0 ? 0.5f : 0.15f), body.Y + body.Height * 0.15f, cabWidth * 0.35f, body.Height * 0.7f);
        }
        else
        {
            g.FillRoundedRectangle(Palette.Brush(color), body, new SizeF(t * 0.16f, t * 0.16f));
            g.FillRectangle(Palette.Brush(Palette.Hex("#d0ebff")), body.X + body.Width * 0.24f, body.Y + body.Height * 0.14f, body.Width * 0.52f, body.Height * 0.72f);
            g.FillRectangle(Palette.Brush(Palette.Darken(color, 0.12f)), body.X + body.Width * 0.34f, body.Y + body.Height * 0.2f, body.Width * 0.32f, body.Height * 0.6f);
        }

        var lightX = vehicle.Heading >= 0 ? body.Right - t * 0.08f : body.X;
        g.FillRectangle(Palette.Brush(Palette.Accent), lightX, body.Y + body.Height * 0.1f, t * 0.08f, body.Height * 0.2f);
        g.FillRectangle(Palette.Brush(Palette.Accent), lightX, body.Y + body.Height * 0.7f, t * 0.08f, body.Height * 0.2f);
    }

    public static void DrawLog(Graphics g, RectangleF area)
    {
        var t = area.Height;
        var body = new RectangleF(area.X + t * 0.04f, area.Y + t * 0.18f, area.Width - t * 0.08f, t * 0.64f);
        g.FillRoundedRectangle(Palette.Brush(Palette.Wood), body, new SizeF(t * 0.3f, t * 0.3f));
        g.FillRectangle(Palette.Brush(Palette.WoodLight), body.X + t * 0.2f, body.Y + t * 0.1f, body.Width - t * 0.4f, t * 0.1f);
        g.FillEllipse(Palette.Brush(Palette.WoodEnd), body.X, body.Y + t * 0.04f, t * 0.28f, body.Height - t * 0.08f);
        g.FillEllipse(Palette.Brush(Palette.WoodEnd), body.Right - t * 0.28f, body.Y + t * 0.04f, t * 0.28f, body.Height - t * 0.08f);
    }

    public static void DrawTrain(Graphics g, RectangleF area, int heading)
    {
        var t = area.Height;
        const int carTiles = 3;
        var cars = (int)Math.Round(area.Width / t / carTiles);
        for (var i = 0; i < cars; i++)
        {
            var isEngine = heading >= 0 ? i == cars - 1 : i == 0;
            var car = new RectangleF(area.X + i * carTiles * t + t * 0.08f, area.Y + t * 0.1f, carTiles * t - t * 0.16f, t * 0.8f);
            g.FillRectangle(Palette.Brush(Color.Black, 55), car.X, car.Bottom - t * 0.04f, car.Width, t * 0.1f);
            g.FillRoundedRectangle(Palette.Brush(isEngine ? Palette.Hex("#c92a2a") : Palette.Hex("#f1f3f5")), car, new SizeF(t * 0.12f, t * 0.12f));
            g.FillRectangle(Palette.Brush(isEngine ? Palette.Hex("#ffe066") : Palette.Hex("#1971c2")), car.X, car.Y + car.Height * 0.42f, car.Width, car.Height * 0.16f);
            for (var w = 0; w < 3; w++)
            {
                g.FillRectangle(Palette.Brush(Palette.Hex("#343a40")), car.X + t * (0.3f + w * 0.85f), car.Y + car.Height * 0.12f, t * 0.45f, car.Height * 0.22f);
            }
        }
    }

    public static void DrawFox(Graphics g, RectangleF tile, int heading)
    {
        var t = tile.Width;
        var orange = Palette.Hex("#f76707");
        var facingRight = heading >= 0;
        float X(float fraction) => facingRight ? tile.X + fraction * t : tile.Right - fraction * t;

        g.FillEllipse(Palette.Brush(Color.Black, 50), tile.X + t * 0.1f, tile.Y + t * 0.72f, t * 0.8f, t * 0.16f);
        var tailX = Math.Min(X(0.02f), X(0.3f));
        g.FillRectangle(Palette.Brush(orange), tailX, tile.Y + t * 0.36f, t * 0.28f, t * 0.2f);
        g.FillRectangle(Brushes.White, facingRight ? tile.X + t * 0.02f : tile.Right - t * 0.12f, tile.Y + t * 0.36f, t * 0.1f, t * 0.2f);
        var bodyX = Math.Min(X(0.25f), X(0.75f));
        g.FillRectangle(Palette.Brush(orange), bodyX, tile.Y + t * 0.3f, t * 0.5f, t * 0.42f);
        var headX = Math.Min(X(0.62f), X(0.94f));
        g.FillRectangle(Palette.Brush(orange), headX, tile.Y + t * 0.22f, t * 0.32f, t * 0.34f);
        g.FillRectangle(Brushes.White, headX, tile.Y + t * 0.42f, t * 0.32f, t * 0.14f);
        g.FillRectangle(Palette.Brush(Palette.Hex("#d9480f")), headX + t * 0.02f, tile.Y + t * 0.1f, t * 0.1f, t * 0.14f);
        g.FillRectangle(Palette.Brush(Palette.Hex("#d9480f")), headX + t * 0.2f, tile.Y + t * 0.1f, t * 0.1f, t * 0.14f);
        g.FillRectangle(Palette.Brush(Palette.Ink), Math.Min(X(0.74f), X(0.82f)), tile.Y + t * 0.3f, t * 0.08f, t * 0.08f);
    }

    public static void DrawBoulder(Graphics g, RectangleF tile, bool landed, long now)
    {
        var t = tile.Width;
        if (!landed)
        {
            var pulse = (MathF.Sin(now / 80f) + 1) / 2;
            g.FillEllipse(Palette.Brush(Color.Black, 70), tile.X + t * 0.2f, tile.Y + t * 0.55f, t * 0.6f, t * 0.25f);
            g.DrawEllipse(Palette.Pen(Color.FromArgb(150 + (int)(100 * pulse), Palette.Danger), t * 0.07f), tile.X + t * 0.12f, tile.Y + t * 0.12f, t * 0.76f, t * 0.76f);
            g.DrawLine(Palette.Pen(Palette.Danger, t * 0.06f), tile.X + t * 0.35f, tile.Y + t * 0.35f, tile.X + t * 0.65f, tile.Y + t * 0.65f);
            g.DrawLine(Palette.Pen(Palette.Danger, t * 0.06f), tile.X + t * 0.65f, tile.Y + t * 0.35f, tile.X + t * 0.35f, tile.Y + t * 0.65f);
            return;
        }

        g.FillEllipse(Palette.Brush(Color.Black, 60), tile.X + t * 0.1f, tile.Y + t * 0.7f, t * 0.8f, t * 0.2f);
        g.FillEllipse(Palette.Brush(Palette.Stone), tile.X + t * 0.08f, tile.Y + t * 0.1f, t * 0.84f, t * 0.74f);
        g.FillEllipse(Palette.Brush(Palette.StoneLight), tile.X + t * 0.22f, tile.Y + t * 0.16f, t * 0.34f, t * 0.22f);
    }

    public static void DrawRailSignal(Graphics g, float x, float laneTop, float tile, bool warning, long now)
    {
        var lit = warning && now / 180 % 2 == 0;
        g.FillRectangle(Palette.Brush(Palette.Hex("#495057")), x + tile * 0.16f, laneTop + tile * 0.3f, tile * 0.08f, tile * 0.6f);
        g.FillEllipse(Palette.Brush(Palette.Ink), x, laneTop + tile * 0.06f, tile * 0.4f, tile * 0.4f);
        g.FillEllipse(Palette.Brush(lit ? Palette.Danger : Palette.Hex("#5c1a1a")), x + tile * 0.06f, laneTop + tile * 0.12f, tile * 0.28f, tile * 0.28f);
    }
}
