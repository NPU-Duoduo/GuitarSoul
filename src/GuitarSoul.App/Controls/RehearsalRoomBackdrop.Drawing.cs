using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace GuitarSoul.App.Controls;

public sealed partial class RehearsalRoomBackdrop
{
    // The room is authored on a fixed logical canvas and drawn on whole pixel blocks,
    // so one room pixel always equals one PixelCat pixel (the cat is 40 x 42 units).
    private const int StageWidth = 208;
    private const int StageHeight = 200;
    private const int FloorY = 128;
    private const int CenterX = 104;

    private static readonly SolidColorBrush WallHigh = Rgb(0x0C, 0x17, 0x12);
    private static readonly SolidColorBrush WallMid = Rgb(0x17, 0x2A, 0x1F);
    private static readonly SolidColorBrush WallLow = Rgb(0x21, 0x37, 0x28);
    private static readonly SolidColorBrush WallGrainDark = Rgb(0x0D, 0x19, 0x13);
    private static readonly SolidColorBrush WallGrainLight = Rgb(0x22, 0x38, 0x29);
    private static readonly SolidColorBrush PanelSeam = Rgb(0x0D, 0x1A, 0x14);
    private static readonly SolidColorBrush PanelEdge = Rgb(0x17, 0x28, 0x1E);
    private static readonly SolidColorBrush Wainscot = Rgb(0x0A, 0x13, 0x0F);
    private static readonly SolidColorBrush WainscotLine = Rgb(0x0E, 0x1B, 0x14);
    private static readonly SolidColorBrush WainscotRail = Rgb(0x2A, 0x3E, 0x2D);
    private static readonly SolidColorBrush BaseBoard = Rgb(0x22, 0x33, 0x26);
    private static readonly SolidColorBrush BaseBoardTop = Rgb(0x33, 0x48, 0x33);
    private static readonly SolidColorBrush FloorFar = Rgb(0x15, 0x10, 0x09);
    private static readonly SolidColorBrush FloorNear = Rgb(0x26, 0x1C, 0x12);
    private static readonly SolidColorBrush FloorLine = Rgb(0x30, 0x24, 0x17);
    private static readonly SolidColorBrush FloorSheen = Rgb(0x3D, 0x2E, 0x1D);
    private static readonly SolidColorBrush RugOutline = Rgb(0x09, 0x0B, 0x06);
    private static readonly SolidColorBrush RugBorder = Rgb(0x3F, 0x3C, 0x1E);
    private static readonly SolidColorBrush RugBorderLit = Rgb(0x52, 0x4C, 0x2A);
    private static readonly SolidColorBrush RugField = Rgb(0x2A, 0x2A, 0x17);
    private static readonly SolidColorBrush RugFieldLit = Rgb(0x33, 0x32, 0x1B);
    private static readonly SolidColorBrush RugMotif = Rgb(0x4C, 0x4A, 0x24);
    private static readonly SolidColorBrush RugShade = Rgb(0x1C, 0x1B, 0x0F);
    private static readonly SolidColorBrush Wood = Rgb(0x3E, 0x2D, 0x1D);
    private static readonly SolidColorBrush WoodLight = Rgb(0x5B, 0x43, 0x2A);
    private static readonly SolidColorBrush WoodDeep = Rgb(0x21, 0x17, 0x0E);
    private static readonly SolidColorBrush Metal = Rgb(0x4C, 0x51, 0x45);
    private static readonly SolidColorBrush MetalDark = Rgb(0x26, 0x2A, 0x23);
    private static readonly SolidColorBrush MetalLight = Rgb(0x78, 0x80, 0x6D);
    private static readonly SolidColorBrush Cream = Rgb(0xDC, 0xCF, 0xB3);
    private static readonly SolidColorBrush CreamDeep = Rgb(0xA8, 0x9A, 0x7C);
    private static readonly SolidColorBrush Warm = Rgb(0xD8, 0x9B, 0x45);
    private static readonly SolidColorBrush Glow = Rgb(0xF9, 0xDC, 0xA2);
    private static readonly SolidColorBrush WarmDeep = Rgb(0x8B, 0x61, 0x30);
    private static readonly SolidColorBrush Amber = Rgb(0xC0, 0x7C, 0x33);
    private static readonly SolidColorBrush Signal = Rgb(0x9C, 0xC0, 0x88);
    private static readonly SolidColorBrush AmpShell = Rgb(0x1B, 0x1A, 0x15);
    private static readonly SolidColorBrush AmpPanel = Rgb(0x2F, 0x2C, 0x21);
    private static readonly SolidColorBrush Grill = Rgb(0x12, 0x13, 0x0F);
    private static readonly SolidColorBrush GrillLine = Rgb(0x1B, 0x1C, 0x15);
    private static readonly SolidColorBrush Cone = Rgb(0x0A, 0x0B, 0x09);
    private static readonly SolidColorBrush CaseLine = Rgb(0x2F, 0x2D, 0x22);
    private static readonly SolidColorBrush Leaf = Rgb(0x22, 0x40, 0x2B);
    private static readonly SolidColorBrush LeafLit = Rgb(0x39, 0x62, 0x3F);
    private static readonly SolidColorBrush LeafDeep = Rgb(0x16, 0x2B, 0x1D);
    private static readonly SolidColorBrush LeafGlow = Rgb(0x50, 0x7E, 0x53);
    private static readonly SolidColorBrush Stem = Rgb(0x2C, 0x4A, 0x2F);
    private static readonly SolidColorBrush Pot = Rgb(0x50, 0x35, 0x24);
    private static readonly SolidColorBrush PotLit = Rgb(0x6B, 0x48, 0x30);
    private static readonly SolidColorBrush SkyTop = Rgb(0x05, 0x0B, 0x14);
    private static readonly SolidColorBrush SkyMid = Rgb(0x0A, 0x17, 0x24);
    private static readonly SolidColorBrush SkyLow = Rgb(0x14, 0x2B, 0x3C);
    private static readonly SolidColorBrush SkyDayTop = Rgb(0x6B, 0x8B, 0x9C);
    private static readonly SolidColorBrush SkyDayMid = Rgb(0x9A, 0xA7, 0x9B);
    private static readonly SolidColorBrush SkyDayLow = Rgb(0xD3, 0xAC, 0x7C);
    private static readonly SolidColorBrush SkyDawnTop = Rgb(0x3B, 0x4A, 0x63);
    private static readonly SolidColorBrush SkyDawnMid = Rgb(0x8B, 0x7C, 0x83);
    private static readonly SolidColorBrush SkyDawnLow = Rgb(0xD5, 0xA3, 0x72);
    private static readonly SolidColorBrush SkyDuskTop = Rgb(0x22, 0x2D, 0x3D);
    private static readonly SolidColorBrush SkyDuskMid = Rgb(0x4C, 0x3A, 0x43);
    private static readonly SolidColorBrush SkyDuskLow = Rgb(0x78, 0x43, 0x25);
    private static readonly SolidColorBrush SkyDuskGlow = Rgb(0x99, 0x60, 0x37);
    private static readonly SolidColorBrush Star = Rgb(0xD8, 0xE4, 0xEA);
    private static readonly SolidColorBrush Moon = Rgb(0xEC, 0xE4, 0xC6);
    private static readonly SolidColorBrush MoonShade = Rgb(0x33, 0x44, 0x4C);
    private static readonly SolidColorBrush Outline = Rgb(0x07, 0x0C, 0x09);
    private static readonly SolidColorBrush NearDark = Rgb(0x05, 0x09, 0x07);
    private static readonly SolidColorBrush NearMid = Rgb(0x0B, 0x14, 0x0E);
    private static readonly SolidColorBrush NearLit = Rgb(0x1A, 0x2C, 0x1E);

    private static readonly Pen FloorPen = FrozenPen(FloorLine, 1);
    private static readonly Pen CablePen = FrozenPen(Rgb(0x0E, 0x0E, 0x09), 1);

    // Sparse, deterministic grain: the wall is a flat plane, so a little noise keeps it
    // from reading as a single colour card without introducing a visible pattern.
    private static readonly (int X, int Y, byte Tone)[] WallGrain = BuildGrain(StageWidth, FloorY - 8, 560);
    private static readonly (int X, int Y, byte Tone)[] FloorGrain = BuildGrain(StageWidth, 72, 220);

    private void DrawRoom(DrawingContext dc, double width, double height, double time)
    {
        int scale = Math.Clamp(PixelScale, 2, 9);
        double ox = Math.Round((width - StageWidth * scale) / 2);
        double oy = StageTopFor(height, scale);
        // The flat planes run past the stage so a wider window never shows a gap.
        int overX = (int)Math.Ceiling(Math.Max(0, ox) / scale) + 2;
        int overY = (int)Math.Ceiling(Math.Max(0, oy) / scale) + 2;
        int left = -overX, right = StageWidth + overX, top = -overY, bottom = StageHeight + overY;
        // Room units visible in the viewport: every layer renders this exact rect so the
        // three planes line up pixel for pixel after the blit.
        var view = new Rect(-ox / scale, -oy / scale, width / scale, height / scale);

        // Defocus is real here, not a bigger pixel block. The far wall and the near leaves
        // are drawn into their own low-resolution targets and blurred, then blitted back
        // over the sharp middle plane. That is the same trick a camera does: one plane in
        // focus, everything else falling off around it.
        DrawBlurredPlane(dc, width, height, view, scale, FarBlurUnits, ref _farLayer,
            c => DrawFarPlane(c, left, right, top, time));
        DrawMidPlane(dc, scale, ox, oy, left, right, bottom, time);
        DrawVolumetricLight(dc, time, scale, ox, oy);
        DrawBlurredPlane(dc, width, height, view, scale, NearBlurUnits, ref _nearLayer,
            c => DrawNearPlane(c, time));
        DrawVignette(dc, left, right, top, bottom);
    }

    /// <summary>Blur radius of the wall plane, in room pixels.</summary>
    private const double FarBlurUnits = .18;

    /// <summary>Blur radius of the foreground foliage, in room pixels. Deliberately large.</summary>
    private const double NearBlurUnits = .85;

    private RenderTargetBitmap? _farLayer;
    private RenderTargetBitmap? _nearLayer;

    /// <summary>
    /// Renders one depth plane at half device resolution, blurs it and blits it back at
    /// full size. Rendering the layer below the output resolution is invisible once it is
    /// blurred, and it keeps a 25 fps scene cheap.
    /// </summary>
    private void DrawBlurredPlane(DrawingContext dc, double width, double height, Rect view,
        double scale, double blurUnits, ref RenderTargetBitmap? cache, Action<DrawingContext> draw)
    {
        const double Density = .5;
        int pw = Math.Max(1, (int)Math.Ceiling(width * Density));
        int ph = Math.Max(1, (int)Math.Ceiling(height * Density));
        double pxPerUnit = scale * Density;

        var target = cache;
        if (target is null || target.PixelWidth != pw || target.PixelHeight != ph)
        {
            target = new RenderTargetBitmap(pw, ph, 96, 96, PixelFormats.Pbgra32);
            cache = target;
        }

        var visual = new DrawingVisual();
        RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);
        if (blurUnits > 0)
        {
            visual.Effect = new BlurEffect
            {
                Radius = blurUnits * pxPerUnit,
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Quality
            };
        }
        using (var c = visual.RenderOpen())
        {
            c.PushTransform(new ScaleTransform(pxPerUnit, pxPerUnit));
            c.PushTransform(new TranslateTransform(-view.X, -view.Y));
            draw(c);
            c.Pop();
            c.Pop();
        }

        target.Clear();
        target.Render(visual);
        dc.DrawImage(target, new Rect(0, 0, width, height));
    }

    private void DrawFarPlane(DrawingContext dc, int left, int right, int top, double time)
    {
        DrawWall(dc, left, right, top);
        DrawWallWash(dc);
        DrawWindow(dc, time);
        DrawWallDecor(dc);
        DrawHangingPlant(dc, time);
        DrawDust(dc, time);
    }

    private void DrawMidPlane(DrawingContext dc, int scale, double ox, double oy,
        int left, int right, int bottom, double time)
    {
        dc.PushTransform(new TranslateTransform(ox, oy));
        dc.PushTransform(new ScaleTransform(scale, scale));

        DrawFloor(dc, left, right, bottom);
        DrawFloorWash(dc, left, right, bottom);
        DrawRug(dc, time);
        DrawShelf(dc, time);
        DrawAcousticGuitar(dc, time);
        DrawFloorLamp(dc, time);
        DrawAmp(dc, time);
        DrawGuitarCase(dc);
        DrawFloorPlant(dc, time);
        DrawCables(dc, time);
        DrawPedalboard(dc);
        DrawCatShadow(dc);

        dc.Pop();
        dc.Pop();
    }

    private void DrawNearPlane(DrawingContext dc, double time) => DrawForeground(dc, time);

    // ---------------------------------------------------------------- architecture

    private static void DrawWall(DrawingContext dc, int left, int right, int top)
    {
        var wall = new LinearGradientBrush(WallHigh.Color, WallLow.Color, 90);
        wall.Freeze();
        dc.DrawRectangle(wall, null, new Rect(left, top, right - left, FloorY - top));

        // Wide stud bays instead of a picket fence: the seam is one tone off the wall and
        // stops at the chair rail, so it reads as plaster board and never as hanging strings.
        for (int x = 52; x < right; x += 52)
        {
            Fill(dc, PanelSeam, x, top, 1, 110 - top);
            Fill(dc, PanelEdge, x + 1, top, 1, 110 - top);
        }
        for (int x = 52; x > left; x -= 52)
        {
            Fill(dc, PanelSeam, x, top, 1, 110 - top);
            Fill(dc, PanelEdge, x + 1, top, 1, 110 - top);
        }

        foreach (var (x, y, tone) in WallGrain)
        {
            if (tone == 2) Fill(dc, WallGrainLight, x, y, 1, 1);
            else Fill(dc, WallGrainDark, x, y, tone == 0 ? 2 : 1, 1);
        }

        // Chair rail and beadboard wainscot.
        Fill(dc, WainscotRail, left, 110, right - left, 2);
        Fill(dc, Rgb(0x16, 0x24, 0x1A), left, 112, right - left, 1);
        Fill(dc, Wainscot, left, 113, right - left, FloorY - 113);
        for (int x = left; x < right; x += 5) Fill(dc, WainscotLine, x, 114, 1, FloorY - 115);
        Fill(dc, BaseBoard, left, FloorY - 3, right - left, 3);
        Fill(dc, BaseBoardTop, left, FloorY - 3, right - left, 1);
        Fill(dc, Outline, left, FloorY - 1, right - left, 1);
    }

    private void DrawWallWash(DrawingContext dc)
    {
        // Two warm sources on the wall itself: the lamp bloom and a weaker window spill.
        // Painted before the furniture so objects can occlude them properly.
        var bloom = new RadialGradientBrush(
            Color.FromArgb(82, 0xFF, 0xB9, 0x66), Color.FromArgb(0, 0xFF, 0xB9, 0x66));
        bloom.Freeze();
        dc.DrawRectangle(bloom, null, new Rect(128 - 82, 52 - 78, 164, 156));

        // A much wider, weaker wash: the bulb does not just glow, it lifts the whole bay.
        var bay = new RadialGradientBrush(
            Color.FromArgb(24, 0xE8, 0x9E, 0x52), Color.FromArgb(0, 0xE8, 0x9E, 0x52));
        bay.Freeze();
        dc.DrawRectangle(bay, null, new Rect(128 - 140, 40 - 120, 280, 240));

        var inner = new RadialGradientBrush(
            Color.FromArgb(52, 0xFF, 0xD2, 0x8E), Color.FromArgb(0, 0xFF, 0xD2, 0x8E));
        inner.Freeze();
        dc.DrawRectangle(inner, null, new Rect(128 - 34, 50 - 30, 68, 60));

        var windowSpill = new RadialGradientBrush(
            Color.FromArgb(26, 0x86, 0xBA, 0xD6), Color.FromArgb(0, 0x86, 0xBA, 0xD6));
        windowSpill.Freeze();
        dc.DrawRectangle(windowSpill, null, new Rect(27 - 46, 46 - 44, 92, 88));
    }

    private static void DrawFloor(DrawingContext dc, int left, int right, int bottom)
    {
        var floor = new LinearGradientBrush(FloorFar.Color, FloorNear.Color, 90);
        floor.Freeze();
        dc.DrawRectangle(floor, null, new Rect(left, FloorY, right - left, bottom - FloorY));

        // Alternate boards get their own tone. Filling them as triangles that converge on
        // the vanishing point is what gives the floor a surface instead of a brown wash.
        var boards = new StreamGeometry();
        using (var g = boards.Open())
        {
            for (int k = -9; k <= 8; k++)
            {
                if (k % 2 != 0) continue;
                // A single eye-level vanishing point sits above the wall-floor seam.
                // The visible boards therefore meet the seam at different positions.
                const double horizon = 78;
                double atWall = (FloorY - horizon) / (bottom - horizon);
                g.BeginFigure(new Point(CenterX + k * 24 * atWall, FloorY), true, true);
                g.LineTo(new Point(CenterX + (k + 1) * 24 * atWall, FloorY), true, false);
                g.LineTo(new Point(CenterX + (k + 1) * 24, bottom), true, false);
                g.LineTo(new Point(CenterX + k * 24, bottom), true, false);
            }
        }
        boards.Freeze();
        dc.PushOpacity(.3);
        dc.DrawGeometry(Brush(Darken(FloorFar.Color, .35)), null, boards);
        dc.Pop();

        var planks = new StreamGeometry();
        using (var g = planks.Open())
        {
            // Boards run toward a vanishing point behind the rug.
            for (int k = -9; k <= 9; k++)
            {
                double atWall = (FloorY - 78d) / (bottom - 78d);
                g.BeginFigure(new Point(CenterX + k * 24 * atWall, FloorY), false, false);
                g.LineTo(new Point(CenterX + k * 24, bottom), true, false);
            }
            for (int i = 1; i <= 7; i++)
            {
                double t = i / 8d;
                double y = FloorY + (bottom - FloorY) * t * t;
                g.BeginFigure(new Point(left, Math.Round(y)), false, false);
                g.LineTo(new Point(right, Math.Round(y)), true, false);
            }
        }
        planks.Freeze();
        dc.PushOpacity(.43);
        dc.DrawGeometry(null, FloorPen, planks);
        dc.Pop();

        foreach (var (x, y, tone) in FloorGrain)
        {
            if (tone == 2) Fill(dc, FloorSheen, x, FloorY + y, 2, 1);
            else Fill(dc, FloorFar, x, FloorY + y, tone == 0 ? 3 : 1, 1);
        }
        Fill(dc, Outline, left, FloorY - 1, right - left, 1);
    }

    private void DrawFloorWash(DrawingContext dc, int left, int right, int bottom)
    {
        // Pool of lamp light on the boards, plus the vertical sheen it leaves behind.
        var pool = new RadialGradientBrush(
            Color.FromArgb(74, 0xF5, 0xBA, 0x6E), Color.FromArgb(0, 0xF5, 0xBA, 0x6E));
        pool.Freeze();
        dc.PushTransform(new ScaleTransform(1, .34));
        dc.DrawRectangle(pool, null, new Rect(128 - 74, (156 - 44) / .34, 148, 96));
        dc.Pop();

        // Sheen: the boards are polished, so the bulb leaves a soft vertical smear.
        var streak = new LinearGradientBrush(
            Color.FromArgb(52, 0xEE, 0xB0, 0x62), Color.FromArgb(0, 0xEE, 0xB0, 0x62), 90);
        streak.Freeze();
        dc.DrawRectangle(streak, null, new Rect(119, FloorY, 19, 32));

        var bounce = new RadialGradientBrush(
            Color.FromArgb(26, 0xFF, 0xD2, 0x94), Color.FromArgb(0, 0xFF, 0xD2, 0x94));
        bounce.Freeze();
        dc.DrawRectangle(bounce, null, new Rect(96, 132, 64, 40));

        var cool = new RadialGradientBrush(
            Color.FromArgb(20, 0x4C, 0x7A, 0x92), Color.FromArgb(0, 0x4C, 0x7A, 0x92));
        cool.Freeze();
        dc.DrawRectangle(cool, null, new Rect(left, FloorY, 110, 90));

        // The floor has to fall away toward the viewer or the empty boards compete with
        // the rug and the cat.
        var depth = new LinearGradientBrush(
            Color.FromArgb(0, 0x06, 0x0A, 0x07), Color.FromArgb(196, 0x06, 0x0A, 0x07), 90);
        depth.Freeze();
        dc.DrawRectangle(depth, null, new Rect(left, 168, right - left, bottom - 168));
    }

    private void DrawWindow(DrawingContext dc, double time)
    {
        const int x0 = 6, y0 = 8, x1 = 48, y1 = 84;
        Fill(dc, Outline, x0 - 1, y0 - 1, x1 - x0 + 2, y1 - y0 + 2);
        Fill(dc, Wood, x0, y0, x1 - x0, y1 - y0);
        Fill(dc, WoodLight, x0, y0, x1 - x0, 1);
        Fill(dc, WoodDeep, x0, y0, 1, y1 - y0);

        int sx0 = x0 + 3, sy0 = y0 + 3, sx1 = x1 - 3, sy1 = y1 - 3;
        double hour = CurrentLocalTime.TimeOfDay.TotalHours;
        SkyPeriod period = PeriodAt(hour);
        for (int y = sy0; y < sy1; y++)
        {
            double t = (y - sy0) / (double)(sy1 - sy0 - 1);
            Fill(dc, SkyAt(t, period), sx0, y, sx1 - sx0, 1);
        }

        if (period is SkyPeriod.Night or SkyPeriod.Dusk)
        {
            ReadOnlySpan<(int X, int Y, double Phase)> stars =
            [
                (11, 16, .0), (19, 24, 1.3), (27, 14, 2.6), (36, 22, 3.9),
                (14, 30, 5.2), (40, 33, 6.5), (31, 39, .8), (23, 36, 2.1)
            ];
            // Dusk only shows the first few stars coming out, so the window keeps changing
            // across the evening instead of snapping from full daylight to full night.
            int visible = period == SkyPeriod.Night ? stars.Length : 3;
            for (int i = 0; i < visible; i++)
            {
                var star = stars[i];
                double alpha = .3 + .7 * Math.Abs(Math.Sin(time * .7 + star.Phase));
                dc.PushOpacity(alpha);
                Fill(dc, Star, star.X, star.Y, 1, 1);
                dc.Pop();
            }
        }

        double progress = period switch
        {
            SkyPeriod.Dawn => (hour - 6) / 3,
            SkyPeriod.Day => (hour - 8) / 8,
            SkyPeriod.Dusk => (hour - 16) / 3,
            _ => ((hour + 6) % 24) / 12
        };
        progress = Math.Clamp(progress, 0, 1);
        double orbX = sx0 + 5 + (sx1 - sx0 - 10) * progress;
        // The sun climbs through the morning and sinks through dusk; the moon arcs high.
        double orbY = period switch
        {
            SkyPeriod.Dusk => sy0 + 20 + 46 * progress,
            SkyPeriod.Dawn => sy0 + 12 + 6 * (1 - progress),
            SkyPeriod.Day => sy0 + 13 - 6 * Math.Sin(Math.PI * progress),
            _ => sy0 + 13 - 6 * Math.Sin(Math.PI * progress)
        };
        if (period is SkyPeriod.Dusk)
        {
            // A low, red sun behind the muntins: the window and the lamp now agree that
            // the room is lit by something other than the sky.
            Fill(dc, Rgb(0x8E, 0x4A, 0x27), (int)orbX - 3, (int)orbY - 2, 7, 5);
            Fill(dc, Rgb(0xB9, 0x6E, 0x38), (int)orbX - 2, (int)orbY - 1, 5, 3);
            Fill(dc, Rgb(0xE3, 0xAC, 0x6A), (int)orbX - 1, (int)orbY - 1, 3, 2);
        }
        else if (period is SkyPeriod.Dawn or SkyPeriod.Day)
        {
            Fill(dc, WarmDeep, (int)orbX - 3, (int)orbY - 3, 7, 7);
            Fill(dc, Glow, (int)orbX - 2, (int)orbY - 2, 5, 5);
            Fill(dc, Cream, (int)orbX - 1, (int)orbY - 1, 3, 3);
        }
        else DrawPixelMoon(dc, orbX, orbY, CurrentMoonPhase);

        // Muntins last so the orb sits behind the window bars.
        Fill(dc, Wood, 25, sy0, 2, sy1 - sy0);
        Fill(dc, Wood, sx0, 33, sx1 - sx0, 2);
        Fill(dc, Wood, sx0, 57, sx1 - sx0, 2);
        Fill(dc, WoodDeep, 27, sy0, 1, sy1 - sy0);
        Fill(dc, WoodDeep, sx0, 35, sx1 - sx0, 1);
        Fill(dc, WoodDeep, sx0, 59, sx1 - sx0, 1);

        // Sill: two lit steps, so the window reads as an object in front of the wall.
        Fill(dc, WoodLight, x0 - 3, y1, (x1 - x0) + 6, 4);
        Fill(dc, Cream, x0 - 3, y1, (x1 - x0) + 6, 1);
        Fill(dc, Outline, x0 - 3, y1 + 4, (x1 - x0) + 6, 1);

        double sway = ReducedMotion ? 0 : Math.Round(Math.Sin(time * .45) * 1.4);
        DrawCurtain(dc, 1 + (int)sway, 5, 7, 82);
        DrawCurtain(dc, 46 - (int)sway, 5, 7, 82);
    }

    private enum SkyPeriod { Dawn, Day, Dusk, Night }

    private static SkyPeriod PeriodAt(double hour) => hour < 6 ? SkyPeriod.Night
        : hour < 8 ? SkyPeriod.Dawn
        : hour < 16 ? SkyPeriod.Day
        : hour < 19 ? SkyPeriod.Dusk
        : SkyPeriod.Night;

    private static SolidColorBrush SkyAt(double t, SkyPeriod period)
    {
        // Four bands per channel keeps the sky inside a small, deliberate palette.
        switch (period)
        {
            case SkyPeriod.Dawn:
                return t < .28 ? SkyDawnTop : t < .56 ? Blend(SkyDawnTop, SkyDawnMid, .6)
                    : t < .8 ? SkyDawnMid : SkyDawnLow;
            case SkyPeriod.Day:
                return t < .3 ? SkyDayTop : t < .58 ? Blend(SkyDayTop, SkyDayMid, .6)
                    : t < .82 ? SkyDayMid : SkyDayLow;
            case SkyPeriod.Dusk:
                // The horizon band stays in the bottom fifth. A dusk sky that runs hot over
                // half the window outshines the lamp, and then the lamp stops being the source.
                return t < .26 ? SkyDuskTop
                    : t < .50 ? Blend(SkyDuskTop, SkyDuskMid, .55)
                    : t < .68 ? SkyDuskMid
                    : t < .82 ? Blend(SkyDuskMid, SkyDuskLow, .5)
                    : t < .93 ? SkyDuskLow
                    : SkyDuskGlow;
            default:
                return t < .3 ? SkyTop : t < .62 ? SkyMid : t < .86 ? SkyLow : Blend(SkyLow, SkyDayLow, .28);
        }
    }

    private static void DrawCurtain(DrawingContext dc, int x, int y, int w, int h)
    {
        Fill(dc, Rgb(0x22, 0x31, 0x26), x, y, w, h);
        Fill(dc, Rgb(0x2E, 0x40, 0x31), x + 1, y + 1, 2, h - 2);
        Fill(dc, Rgb(0x11, 0x1C, 0x15), x, y, 1, h);
        Fill(dc, Outline, x + w - 1, y, 1, h);
        for (int i = 3; i < w - 1; i += 2) Fill(dc, Rgb(0x18, 0x24, 0x1B), x + i, y + 1, 1, h - 2);
        Fill(dc, Wood, x - 1, y - 2, w + 2, 2);
        Fill(dc, WoodLight, x - 1, y - 2, w + 2, 1);
    }

    private static void DrawPixelMoon(DrawingContext dc, double cx, double cy, double phase)
    {
        const int radius = 4;
        int ix = (int)Math.Round(cx), iy = (int)Math.Round(cy);
        dc.PushOpacity(.26);
        FillShape(dc, Moon, ix - radius - 3, iy - radius - 3, ix + radius + 3, iy + radius + 3,
            (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= (radius + 2.6) * (radius + 2.6));
        dc.Pop();
        FillShape(dc, Moon, ix - radius, iy - radius, ix + radius + 1, iy + radius + 1,
            (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius);
        double c = Math.Cos(phase * Math.PI * 2);
        if (Math.Abs(c) > .08)
        {
            FillShape(dc, MoonShade, ix - radius, iy - radius, ix + radius + 1, iy + radius + 1,
                (x, y) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius &&
                    (phase <= .5 ? x - cx < -c * 1.6 : x - cx > -c * 1.6));
        }
    }

    private static void DrawWallDecor(DrawingContext dc)
    {
        // Framed mountain print, echoing the room's own subject without adding a second focal point.
        Fill(dc, Outline, 55, 15, 22, 28);
        Fill(dc, WoodDeep, 56, 16, 20, 26);
        Fill(dc, Wood, 57, 17, 18, 24);
        Fill(dc, WoodLight, 57, 17, 18, 1);
        Fill(dc, CreamDeep, 59, 19, 14, 20);
        Fill(dc, SkyMid, 60, 20, 12, 18);
        Fill(dc, Blend(SkyLow, SkyDayLow, .35), 60, 28, 12, 10);
        // Two peaks rather than one parabola, so the print reads as a landscape.
        static double Ridge(double x) =>
            34 - 7 * Math.Max(0, 1 - Math.Abs(x - 63.5) / 4.0) - 5 * Math.Max(0, 1 - Math.Abs(x - 69) / 3.5);
        FillShape(dc, Rgb(0x2C, 0x47, 0x52), 60, 22, 72, 38, (x, y) => y > Ridge(x));
        FillShape(dc, Rgb(0x8E, 0xA7, 0xAC), 60, 22, 72, 38,
            (x, y) => y > Ridge(x) && y < Ridge(x) + 2);
        Fill(dc, WarmDeep, 68, 22, 2, 2);
        Fill(dc, Cream, 60, 37, 12, 1);
        Fill(dc, Outline, 56, 42, 20, 1);
    }

    private void DrawHangingPlant(DrawingContext dc, double time)
    {
        // A macrame hanger under the light: the one element that reaches the ceiling and
        // closes the top of the frame without hiding the wall.
        double sway = ReducedMotion ? 0 : Math.Sin(time * .5);
        const int cx = 158;
        Fill(dc, MetalDark, cx - 1, 0, 3, 4);
        Fill(dc, MetalLight, cx - 1, 0, 1, 4);
        // Three cords fanning from the hook to the rim.
        for (int i = -1; i <= 1; i++)
        {
            for (int y = 4; y < 14; y++)
            {
                int x = cx + i * (y - 4) / 3 + (int)Math.Round(sway * (y - 4) / 10.0);
                Fill(dc, Rgb(0x53, 0x44, 0x2A), x, y, 1, 1);
            }
        }

        // Small terracotta pot, one rim band, no crate stripes.
        Fill(dc, Outline, cx - 6, 13, 13, 8);
        Fill(dc, Pot, cx - 5, 14, 11, 6);
        Fill(dc, PotLit, cx - 6, 13, 13, 2);
        Fill(dc, Rgb(0x2E, 0x1D, 0x12), cx - 6, 15, 13, 1);
        FillShape(dc, Rgb(0x2E, 0x1D, 0x12), cx - 5, 14, cx + 6, 20, (x, y) =>
            x > cx + 2 && y > 15);
        Fill(dc, Rgb(0x18, 0x11, 0x0A), cx - 4, 19, 9, 1);

        ReadOnlySpan<(int X, int Len, double Phase)> vines =
            [(cx - 4, 26, .0), (cx, 34, 1.1), (cx + 4, 22, 2.2)];
        foreach (var vine in vines)
        {
            int drift = ReducedMotion ? 0 : (int)Math.Round(Math.Sin(time * .5 + vine.Phase) * 1.5);
            for (int i = 0; i < vine.Len; i++)
            {
                int x = vine.X + drift * i / vine.Len;
                Fill(dc, Stem, x, 20 + i, 1, 1);
            }
            for (int i = 2; i < vine.Len; i += 3)
            {
                int x = vine.X + drift * i / vine.Len;
                int y = 20 + i;
                bool left = (i / 3) % 2 == 0;
                double angle = left ? Math.PI * .78 : Math.PI * .22;
                FillLeaf(dc, LeafDeep, x + Math.Cos(angle) * 2.4, y + Math.Sin(angle) * 2.4, 3.4, 1.9, angle);
                FillLeaf(dc, Leaf, x + Math.Cos(angle) * 2.1, y + Math.Sin(angle) * 2.1, 2.7, 1.4, angle);
            }
        }
    }

    private void DrawRug(DrawingContext dc, double time)
    {
        const int top = 150, bottom = 198;
        // Half width grows toward the viewer, so the rug reads as lying on the floor.
        static double HalfAt(double y) => 33 * (y - 78) / (top - 78);

        FillShape(dc, RugOutline, 40, top, 170, bottom + 1, (x, y) =>
        {
            double half = HalfAt(y);
            return x >= CenterX - half && x <= CenterX + half;
        });
        FillShape(dc, RugBorder, 41, top + 1, 169, bottom, (x, y) =>
        {
            double half = HalfAt(y) - 1;
            return x >= CenterX - half && x <= CenterX + half;
        });
        FillShape(dc, RugField, 44, top + 3, 166, bottom - 1, (x, y) =>
        {
            double half = HalfAt(y) - 3;
            return x >= CenterX - half && x <= CenterX + half;
        });

        // Border motif: one lighter rule just inside the edge, nothing dotted.
        FillShape(dc, RugBorderLit, 41, top + 1, 169, bottom, (x, y) =>
        {
            double half = HalfAt(y) - 2.2;
            bool inBand = x >= CenterX - half && x <= CenterX + half &&
                (x < CenterX - half + 1.6 || x > CenterX + half - 1.6 || y < top + 4 || y > bottom - 3);
            return inBand;
        });

        // Broken woven motifs follow the perspective of the carpet instead of producing
        // a uniform diagonal screen-door pattern. Every mark is deterministic and static.
        var thread = Rgb(0x77, 0x69, 0x3B);
        var threadShadow = Rgb(0x19, 0x22, 0x16);
        for (int y = top + 5; y < bottom - 4; y++)
        {
            double half = HalfAt(y) - 5;
            for (int x = (int)(CenterX - half); x <= CenterX + half; x++)
            {
                uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663));
                hash ^= hash >> 13;
                double u = (x - CenterX) / half;
                double v = (y - top) / (double)(bottom - top);
                double lozenge = Math.Abs(u) * 1.8 + Math.Abs(v - .54) * 2.0;
                bool ornament = Math.Abs(lozenge - .53) < .065 ||
                    (Math.Abs(u) > .66 && ((y + (int)(u * 17)) % 9 < 2));
                if (ornament && hash % 5 != 0)
                {
                    dc.PushOpacity(.36);
                    Fill(dc, thread, x, y, 1, 1);
                    dc.Pop();
                }
                else if (hash % 23 == 0)
                {
                    dc.PushOpacity(.26);
                    Fill(dc, (hash & 1) == 0 ? thread : threadShadow, x, y, 1, 1);
                    dc.Pop();
                }
            }
        }

        // One woven diamond under the cat, plus two small ones further back.
        FillShape(dc, RugMotif, CenterX - 8, 168, CenterX + 9, 186, (x, y) =>
            Math.Abs(x - CenterX) + Math.Abs(y - 176) == 7 ||
            Math.Abs(x - CenterX) + Math.Abs(y - 176) == 4);
        FillShape(dc, RugMotif, CenterX - 18, 156, CenterX - 8, 166, (x, y) =>
            Math.Abs(x - CenterX + 13) + Math.Abs(y - 161) == 3);
        FillShape(dc, RugMotif, CenterX + 8, 156, CenterX + 19, 166, (x, y) =>
            Math.Abs(x - CenterX - 13) + Math.Abs(y - 161) == 3);

        // Fringe on the near edge.
        FillShape(dc, RugShade, 40, bottom, 170, bottom + 3, (x, y) =>
        {
            double half = HalfAt(bottom) - 1;
            return x >= CenterX - half && x <= CenterX + half && x % 2 == 0;
        });

        // The lamp lands on the rug, not just on the boards; without this the rug reads
        // as a pasted decal instead of a surface in the light.
        var pool = new RadialGradientBrush(
            Color.FromArgb(62, 0xF2, 0xB6, 0x6A), Color.FromArgb(0, 0xF2, 0xB6, 0x6A));
        pool.Freeze();
        dc.PushTransform(new ScaleTransform(1, .5));
        dc.DrawRectangle(pool, null, new Rect(CenterX - 78, (178 - 44) / .5, 156, 88));
        dc.Pop();

        // A subtle far-edge shadow keeps the weave visible instead of creating a black slab.
        dc.PushOpacity(.22);
        FillShape(dc, Brush(Darken(RugShade.Color, .3)), 40, top, 170, top + 12, (x, y) =>
        {
            double half = HalfAt(y) - 2;
            return x >= CenterX - half && x <= CenterX + half;
        });
        dc.Pop();

        // A shared highlight sends one ring across the weave; nothing else moves the rug.
        double age = time - _rugPulseStarted;
        if (age is >= 0 and < 1.6)
        {
            double radius = 8 + age * 44;
            dc.PushOpacity(Math.Max(0, .42 - age * .26));
            FillShape(dc, Warm, 40, top, 170, bottom, (x, y) =>
                Math.Abs(Math.Sqrt((x - CenterX) * (x - CenterX) * .45 + (y - 176) * (y - 176)) - radius) < 1.2);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------- furniture

    private void DrawShelf(DrawingContext dc, double time)
    {
        Color wood = Wood.Color;

        // The case sits on the floor to the right, so the shelf throws its shadow left,
        // away from the lamp, and drops a dark contact line on the boards.
        dc.PushOpacity(.45);
        Fill(dc, NearDark, 0, 126, 52, 5);
        Fill(dc, NearDark, 0, 86, 5, 42);
        dc.Pop();

        Fill(dc, Outline, 3, 86, 44, 43);
        Fill(dc, Form(wood, 4, 87, 42, 41, .2, .55), 4, 87, 42, 41);

        // Top face: the shelf is a box, so the top plane catches the shade.
        Fill(dc, Brush(Lighten(wood, .3)), 4, 86, 42, 3);
        Fill(dc, Brush(Lighten(wood, .1)), 4, 89, 42, 1);

        // Inner cavity, darker than the frame and darker at the bottom.
        Fill(dc, FormVertical(Darken(wood, .7), 91, 33, .06, .3), 8, 91, 34, 33);
        Fill(dc, Brush(Lighten(wood, .18)), 8, 104, 34, 2);
        Fill(dc, Brush(Darken(wood, .2)), 8, 106, 34, 1);

        SolidColorBrush[] records =
        [
            Rgb(0x7E, 0x43, 0x32), Rgb(0x36, 0x4E, 0x4F), Rgb(0x8B, 0x66, 0x35),
            Rgb(0xDC, 0xCF, 0xB3), Rgb(0x36, 0x4E, 0x4F), Rgb(0x8B, 0x66, 0x35),
            Rgb(0x6B, 0x4A, 0x2E), Rgb(0xDC, 0xCF, 0xB3)
        ];
        int x = 10;
        for (int i = 0; i < records.Length; i++)
        {
            int w = i % 3 == 0 ? 3 : 2;
            int top = 107 + (i % 2);
            // Each sleeve is a thin slab: lit edge on the right, shadow between records.
            Fill(dc, records[i], x, top, w, 19 - (i % 2));
            Fill(dc, Brush(Darken(records[i].Color, .45)), x + w - 1, top, 1, 19 - (i % 2));
            Fill(dc, Brush(Lighten(records[i].Color, .3)), x, top, 1, 19 - (i % 2));
            x += w + 1;
        }
        for (int i = 0; i < 5; i++)
        {
            SolidColorBrush spine = i % 2 == 0 ? Cream : RugBorder;
            Fill(dc, spine, 10 + i * 4, 93 + (i % 2) * 2, 3, 8 - (i % 2));
            Fill(dc, Brush(Darken(spine.Color, .4)), 12 + i * 4, 93 + (i % 2) * 2, 1, 8 - (i % 2));
        }
        Fill(dc, Brush(Darken(wood, .55)), 8, 124, 34, 3);
        Fill(dc, Brush(Lighten(wood, .12)), 8, 124, 34, 1);

        // The lamp sits to the right, so the right edge of the shelf catches the light.
        dc.PushOpacity(.5 + (ReducedMotion ? 0 : .06 * Math.Sin(time * 3.1)));
        Fill(dc, Brush(Lighten(WarmDeep.Color, .35)), 44, 89, 2, 38);
        dc.Pop();
    }

    private void DrawAcousticGuitar(DrawingContext dc, double time)
    {
        Color spruce = Rgb(0xB6, 0x79, 0x36).Color;
        Color rosewood = Rgb(0x2C, 0x1C, 0x11).Color;

        // The guitar leans back against the wall with its body on the boards, so the whole
        // silhouette is sheared rather than vertical. The shear is applied per row in whole
        // units, which keeps every edge on the pixel grid — a RotateTransform would not.
        dc.PushOpacity(.4);
        FillShape(dc, Brush(Darken(WallMid.Color, .5)), 20, 54, 96, FloorY, (x, y) =>
            GuitarSilhouette(x + 5, y));
        dc.Pop();

        // --- body: one silhouette with a real waist, at dreadnought proportions
        FillShape(dc, Outline, 40, BodyTop - 1, 82, BodyBottom + 1, (x, y) =>
            y >= BodyTop - 1 && y <= BodyBottom
            && Math.Abs(x - GuitarAxis(y)) <= GuitarHalfWidth(y) + .7);

        // Spruce soundboard: the lamp sits up and to the right, so the falloff runs from the
        // lower treble bout toward the shadowed bass side.
        var board = new RadialGradientBrush(Lighten(spruce, .38), Darken(spruce, .5))
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(GuitarAxis(114) + 5, 114),
            GradientOrigin = new Point(GuitarAxis(114) + 5, 114),
            RadiusX = 17,
            RadiusY = 18
        };
        board.Freeze();
        FillShape(dc, board, 40, BodyTop, 82, BodyBottom, GuitarBody);

        // Binding: cream keyline, bright where the lamp reaches it and dark on the far side.
        FillShape(dc, Brush(Lighten(CreamDeep.Color, .14)), 40, BodyTop - 1, 82, BodyBottom + 1,
            (x, y) => GuitarBody(x, y) && !GuitarBodyInset(x, y));
        FillShape(dc, Brush(Darken(CreamDeep.Color, .5)), 40, BodyTop - 1, 62, BodyBottom + 1,
            (x, y) => GuitarBody(x, y) && !GuitarBodyInset(x, y));

        // --- soundhole: a wide rosette ring around a real hole, a third of the way down
        FillShape(dc, Brush(Lighten(CreamDeep.Color, .14)), 40, 96, 82, 112, (x, y) =>
        {
            double dx = x - GuitarAxis(104), dy = y - 104;
            double d = dx * dx + dy * dy;
            return d <= 27 && d > 15;
        });
        FillShape(dc, Brush(Darken(rosewood, .72)), 40, 98, 82, 111, (x, y) =>
        {
            double dx = x - GuitarAxis(104), dy = y - 104;
            return dx * dx + dy * dy <= 15;
        });

        // --- bridge and saddle, clear of the rosette
        FillShape(dc, Brush(Lighten(rosewood, .24)), 40, 115, 82, 118, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= 4.6);
        FillShape(dc, Brush(Lighten(CreamDeep.Color, .26)), 40, 115, 82, 116, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= 4.6);

        // No pickguard: at this scale a teardrop next to the rosette just reads as a second
        // hole. The waist, rosette and bridge already say "acoustic" on their own.

        // --- neck: a slim shaft that widens toward the body joint
        FillShape(dc, Outline, 40, NutY - 1, 82, BodyTop + 1, (x, y) =>
            y >= NutY - 1 && y <= BodyTop && Math.Abs(x - GuitarAxis(y)) <= GuitarNeckHalf(y) + .7);
        FillShape(dc, Brush(Darken(rosewood, .14)), 40, NutY, 82, BodyTop, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= GuitarNeckHalf(y));

        // Two dim lines are all the scale can carry. Six strings would fuse into one bright
        // bar and bury the frets, the rosette and the bridge under it.
        foreach (double spread in StringSpreads)
        {
            FillShape(dc, Brush(Lighten(rosewood, .38)), 40, NutY, 82, 115, (x, y) =>
            {
                double t = (115 - y) / 45d;
                double x0 = GuitarAxis(y) + spread * (1.35 + (1.05 - 1.35) * t);
                return Math.Abs(x - x0) < .5;
            });
        }

        // Frets go on last so the strings break across them, the way a real board reads.
        // They stay dim on purpose: bright rungs turn the neck into a ladder.
        foreach (int fret in FretRows)
        {
            FillShape(dc, Brush(Lighten(rosewood, .19)), 40, fret, 82, fret + 1, (x, y) =>
                Math.Abs(x - GuitarAxis(y)) <= GuitarNeckHalf(y));
        }

        // --- headstock: a flared paddle, three tuners a side
        FillShape(dc, Outline, 34, HeadTop - 1, 82, NutY + 1, (x, y) =>
            y >= HeadTop - 1 && y <= NutY && Math.Abs(x - GuitarAxis(y)) <= GuitarHeadHalf(y) + .7);
        FillShape(dc, Brush(Darken(rosewood, .06)), 34, HeadTop, 82, NutY, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= GuitarHeadHalf(y));
        FillShape(dc, Brush(Lighten(rosewood, .3)), 34, HeadTop, 82, HeadTop + 1, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= GuitarHeadHalf(y));

        for (int i = 0; i < 3; i++)
        {
            int ty = HeadTop + 3 + i * 3;
            int axis = (int)Math.Round(GuitarAxis(ty));
            int reach = (int)Math.Round(GuitarHeadHalf(ty)) + 1;
            Fill(dc, Brush(Darken(MetalDark.Color, .35)), axis - reach - 1, ty, 2, 1);
            Fill(dc, MetalDark, axis - reach - 1, ty + 1, 1, 1);
            Fill(dc, Brush(Darken(MetalDark.Color, .35)), axis + reach - 1, ty, 2, 1);
            Fill(dc, MetalDark, axis + reach, ty + 1, 1, 1);
        }

        // Nut: a bone bar where the board meets the paddle.
        FillShape(dc, Brush(Lighten(Cream.Color, .04)), 34, NutY - 1, 82, NutY, (x, y) =>
            Math.Abs(x - GuitarAxis(y)) <= GuitarNeckHalf(y) + .4);

        // --- contact shadow so the guitar sits on the floor instead of hovering
        dc.PushOpacity(.6);
        FillShape(dc, NearDark, 40, 126, 84, 132, (x, y) =>
        {
            double ex = (x - GuitarCentre) / 14.0, ey = (y - 128.4) / 2.4;
            return ex * ex + ey * ey <= 1;
        });
        dc.Pop();

        // Real input makes the strings catch the lamp for a moment; nothing plays on its own.
        double age = time - _inputPulseStarted;
        if (age is >= 0 and < .55)
        {
            dc.PushOpacity(Math.Max(0, .85 - age * 1.5));
            foreach (double spread in StringSpreads)
            {
                FillShape(dc, Glow, 40, NutY, 82, 115, (x, y) =>
                {
                    double t = (115 - y) / 45d;
                    double x0 = GuitarAxis(y) + spread * (1.35 + (1.05 - 1.35) * t);
                    return Math.Abs(x - x0) < .5;
                });
            }
            dc.Pop();
        }
    }

    // ---------------------------------------------------------------- guitar geometry
    //
    // The instrument leans back against the wall, so its axis drifts left as it rises. Every
    // shape below is expressed against GuitarAxis(y) instead of a fixed x, which shears the
    // whole guitar in whole room units and keeps it on the pixel grid.

    private const double GuitarCentre = 62;
    private const int BodyTop = 94;
    private const int BodyBottom = 128;
    private const int NutY = 70;
    private const int HeadTop = 60;

    /// <summary>Rows that carry a fret line, thinned out so they read as frets, not a ladder.</summary>
    private static readonly int[] FretRows = [73, 77, 81, 85, 89, 92];

    /// <summary>Which strings get drawn, as a fraction of the saddle's half span.</summary>
    private static readonly double[] StringSpreads = [-.72, .72];

    /// <summary>How far left the guitar's axis has drifted at a given row, in room units.</summary>
    private static double GuitarAxis(double y) => GuitarCentre - Math.Max(0, (FloorY - y) * .26);

    /// <summary>Half width of a dreadnought body at a given row, in room units.</summary>
    private static double GuitarHalfWidth(double y)
    {
        ReadOnlySpan<(double Y, double Half)> keys =
        [
            (94, 5.6), (96, 8.2), (98, 9.5), (101, 10.0), (105, 9.6),
            (108, 9.0), (110, 8.7), (112, 9.2), (115, 10.8), (118, 12.2),
            (121, 12.9), (124, 12.4), (126, 10.8), (127, 7.6), (128, 3.6)
        ];
        if (y < keys[0].Y || y > keys[^1].Y) return 0;
        for (int i = 1; i < keys.Length; i++)
        {
            if (y > keys[i].Y) continue;
            double t = (y - keys[i - 1].Y) / (keys[i].Y - keys[i - 1].Y);
            return keys[i - 1].Half + (keys[i].Half - keys[i - 1].Half) * t;
        }
        return keys[^1].Half;
    }

    /// <summary>Nut is 1.7 half units; the board widens to the 14th fret at the body joint.</summary>
    private static double GuitarNeckHalf(double y) => 1.7 + (y - NutY) * .02;

    private static double GuitarHeadHalf(double y) => 2.4 + (NutY - y) * .03;

    private static bool GuitarBody(int x, int y) =>
        y >= BodyTop && y <= BodyBottom && Math.Abs(x - GuitarAxis(y)) <= GuitarHalfWidth(y);

    private static bool GuitarBodyInset(int x, int y) =>
        y >= BodyTop + 1 && y <= BodyBottom - 1
        && Math.Abs(x - GuitarAxis(y)) <= GuitarHalfWidth(y) - 1.1;

    /// <summary>The whole instrument as one silhouette, used for the cast shadow on the wall.</summary>
    private static bool GuitarSilhouette(double x, double y)
    {
        double axis = GuitarAxis(y);
        if (y >= BodyTop - 1 && y <= BodyBottom) return Math.Abs(x - axis) <= GuitarHalfWidth(y) + .6;
        if (y >= NutY && y < BodyTop) return Math.Abs(x - axis) <= GuitarNeckHalf(y) + .6;
        if (y >= HeadTop && y < NutY) return Math.Abs(x - axis) <= GuitarHeadHalf(y) + .6;
        return false;
    }

    private void DrawFloorLamp(DrawingContext dc, double time)
    {
        double flicker = ReducedMotion ? 1 : 1 + .05 * Math.Sin(time * 3.1) + .035 * Math.Sin(time * 7.7 + 1.3);
        double level = Math.Clamp(Level / 60.0, 0, 1);
        double strength = Math.Clamp(flicker * (.84 + .3 * level), .6, 1.3);
        const int cx = 128;

        // Contact pool under the base, then the base itself as a lit dome.
        dc.PushOpacity(.55);
        FillShape(dc, NearDark, cx - 12, 122, cx + 12, 130, (x, y) =>
        {
            double ex = (x - cx) / 11.0, ey = (y - 126) / 2.4;
            return ex * ex + ey * ey <= 1;
        });
        dc.Pop();

        // Pole: a cylinder, so it needs a bright side, a core and a dark side.
        Fill(dc, Outline, cx - 2, 48, 5, 78);
        Fill(dc, Brush(Darken(MetalDark.Color, .25)), cx - 1, 48, 3, 78);
        Fill(dc, Brush(Lighten(Metal.Color, .28)), cx - 1, 48, 1, 78);
        Fill(dc, Brush(Darken(MetalDark.Color, .45)), cx + 1, 48, 1, 78);

        var baseDome = new RadialGradientBrush(
            Lighten(Metal.Color, .34), Darken(MetalDark.Color, .45))
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new Point(cx - 3, 123),
            GradientOrigin = new Point(cx - 3, 123),
            RadiusX = 10,
            RadiusY = 5
        };
        baseDome.Freeze();
        FillShape(dc, Outline, cx - 9, 121, cx + 9, 128, (x, y) =>
        {
            double ex = (x - cx) / 8.0, ey = (y - 125) / 2.6;
            return ex * ex + ey * ey <= 1;
        });
        FillShape(dc, baseDome, cx - 8, 122, cx + 8, 127, (x, y) =>
        {
            double ex = (x - cx) / 7.0, ey = (y - 125) / 2.0;
            return ex * ex + ey * ey <= 1;
        });
        Fill(dc, Brush(Lighten(Metal.Color, .4)), cx - 6, 122, 9, 1);

        // Shade: dark outside because the bulb points down, so the form reads as a cone
        // with a lit mouth rather than a glowing lampshade decal.
        FillShape(dc, Outline, cx - 14, 31, cx + 15, 51, (x, y) =>
            Math.Abs(x - cx) <= 3 + (y - 33) * .78);
        FillShape(dc, Form(WarmDeep.Color, cx - 13, 33, 26, 17, .18, .5), cx - 12, 33, cx + 13, 50, (x, y) =>
            Math.Abs(x - cx) <= 2.6 + (y - 33) * .75);
        // Two shading bands down the cone keep it from reading as a flat triangle.
        FillShape(dc, Brush(Darken(WarmDeep.Color, .35)), cx - 12, 33, cx - 4, 50, (x, y) =>
            Math.Abs(x - cx) <= 2.6 + (y - 33) * .75 && x < cx - 5);
        FillShape(dc, Brush(Lighten(Amber.Color, .2)), cx + 3, 36, cx + 12, 50, (x, y) =>
            Math.Abs(x - cx) <= 2.6 + (y - 33) * .75 && x > cx + 4);
        // Mouth: the brightest thing in the room, and a 1px rim catching it.
        FillShape(dc, Brush(Lighten(Amber.Color, .35)), cx - 12, 47, cx + 13, 51, (x, y) =>
            Math.Abs(x - cx) <= 2.6 + (y - 33) * .75);
        Fill(dc, Brush(Lighten(Glow.Color, .25)), cx - 10, 49, 21, 2);
        Fill(dc, Metal, cx - 2, 30, 5, 3);
        Fill(dc, Brush(Lighten(Metal.Color, .35)), cx - 2, 30, 5, 1);

        dc.PushOpacity(Math.Clamp(strength, .6, 1));
        Fill(dc, Glow, cx - 9, 50, 19, 2);
        dc.Pop();

        // Volumetric cone: four stepped densities, never a solid wedge.
        for (int band = 0; band < 4; band++)
        {
            double spread = .5 + band * .19;
            dc.PushOpacity(Math.Clamp(.05 * strength - band * .009, .006, .11));
            FillShape(dc, Glow, cx - 52, 50, cx + 52, FloorY + 18, (x, y) =>
            {
                double t = Math.Min(1, (y - 50) / (FloorY - 32.0));
                double half = 5 + 44 * t * spread;
                return Math.Abs(x - cx) <= half;
            });
            dc.Pop();
        }
    }

    private void DrawAmp(DrawingContext dc, double time)
    {
        double energy = IsPlaying ? Math.Clamp(AccompanimentEnergy, 0, 1) : 0;
        // A 2x12 combo: wider than it is tall, so it reads as an amp and not as a fridge.
        const int x0 = 134, x1 = 172, top = 84, bottom = 128;
        int w = x1 - x0, h = bottom - top;
        Color shell = Rgb(0x2E, 0x2B, 0x21).Color;
        Color panel = Rgb(0x36, 0x33, 0x27).Color;

        // Cast shadow to the right, away from the lamp, plus a dark contact pool.
        dc.PushOpacity(.55);
        FillShape(dc, NearDark, x0 + 4, bottom - 3, x1 + 20, bottom + 5, (x, y) =>
        {
            double ex = (x - (x0 + 14)) / 22.0, ey = (y - bottom) / 3.6;
            return ex * ex + ey * ey <= 1;
        });
        dc.Pop();

        // Top face: two units of slightly lit tolex. Just enough to say "box".
        Fill(dc, Brush(Lighten(shell, .14)), x0 - 1, top - 2, w + 2, 2);
        Fill(dc, Brush(Lighten(shell, .05)), x0 - 1, top, w + 2, 1);

        Fill(dc, Outline, x0 - 1, top, w + 2, h);
        Fill(dc, Form(shell, x0, top, w, h, .2, .42), x0, top + 1, w, h - 1);

        // Control panel: a narrow brushed strip, knobs small enough to stay knobs.
        Fill(dc, Form(panel, x0, top + 1, w, 7, .26, .22), x0, top + 1, w, 7);
        Fill(dc, Brush(Lighten(panel, .26)), x0, top + 1, w, 1);
        Fill(dc, Brush(Darken(panel, .55)), x0, top + 8, w, 1);
        for (int i = 0; i < 5; i++)
        {
            int kx = x0 + 4 + i * 6;
            Fill(dc, Brush(Darken(panel, .5)), kx, top + 5, 2, 2);
            Fill(dc, Brush(Lighten(CreamDeep.Color, .1)), kx, top + 5, 2, 1);
            Fill(dc, Brush(Lighten(Cream.Color, .25)), kx, top + 5, 1, 1);
        }
        Fill(dc, Brush(Lighten(panel, .3)), x0 + 4, top + 3, 20, 1);

        // Silver piping between panel and cloth: one pixel of material change.
        Fill(dc, Brush(Lighten(Metal.Color, .35)), x0, top + 9, w, 1);

        // Grille cloth: a warm silver weave, deliberately lighter than the tolex so the
        // cabinet reads as a box with a face rather than one dark slab.
        Color cloth = Rgb(0x46, 0x40, 0x32).Color;
        int gx = x0 + 2, gy = top + 11, gw = w - 4, gh = h - 15;
        Fill(dc, FormVertical(cloth, gy, gh, .24, .34), gx, gy, gw, gh);
        // Cloth weave is noise at this scale: a faint horizontal thread every four units is
        // as much texture as the grid can hold before it turns into slats.
        for (int y = gy + 2; y < gy + gh; y += 4)
            Fill(dc, Brush(Lighten(cloth, .07)), gx, y, gw, 1);

        // Baffle strip between the two drivers: a shade darker than the cloth.
        Fill(dc, Brush(Darken(cloth, .3)), x0 + w / 2 - 1, gy, 2, gh);

        for (int i = 0; i < 2; i++)
        {
            int cx = x0 + 10 + i * 18;
            int cy = gy + gh / 2;

            // Frame gap, then the cone itself. Dark circles on a light cloth is the whole
            // read: without the cloth the speakers are just two smudges.
            FillShape(dc, Brush(Darken(cloth, .62)), cx - 9, cy - 9, cx + 9, cy + 9, (x, y) =>
                (x - cx) * (x - cx) + (y - cy) * (y - cy) <= 72);
            var cone = new RadialGradientBrush(
                Lighten(Rgb(0x24, 0x20, 0x18).Color, .3), Darken(Rgb(0x24, 0x20, 0x18).Color, .55))
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = new Point(cx - 3, cy - 3),
                GradientOrigin = new Point(cx - 3, cy - 3),
                RadiusX = 9,
                RadiusY = 9
            };
            cone.Freeze();
            FillShape(dc, cone, cx - 7, cy - 7, cx + 7, cy + 7, (x, y) =>
                (x - cx) * (x - cx) + (y - cy) * (y - cy) <= 45);
            FillShape(dc, Brush(Lighten(cloth, .04)), cx - 2, cy - 2, cx + 3, cy + 3, (x, y) =>
                (x - cx) * (x - cx) + (y - cy) * (y - cy) <= 4);
            FillShape(dc, Brush(Lighten(cloth, .22)), cx - 2, cy - 2, cx + 1, cy, (x, y) =>
                (x - cx) * (x - cx) + (y - cy) * (y - cy) <= 2);
            if (energy > .25)
            {
                dc.PushOpacity(Math.Clamp(energy * .34, 0, .34));
                FillShape(dc, Warm, cx - 8, cy - 9, cx + 8, cy + 9, (x, y) =>
                {
                    double d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                    double r = 5 + energy * 2;
                    return d <= r * r && d >= (r - 1.6) * (r - 1.6);
                });
                dc.Pop();
            }
        }


        // Feet, handle, and the lamp-side rim that keeps the left edge from going flat.
        Fill(dc, Brush(Darken(shell, .55)), x0 + 2, bottom - 1, 5, 2);
        Fill(dc, Brush(Darken(shell, .55)), x1 - 7, bottom - 1, 5, 2);
        Fill(dc, Brush(Darken(MetalDark.Color, .35)), x0 + 13, top - 4, 12, 2);
        Fill(dc, Brush(Lighten(Metal.Color, .3)), x0 + 13, top - 4, 12, 1);
        dc.PushOpacity(.45);
        Fill(dc, Brush(Lighten(WarmDeep.Color, .4)), x0, top + 1, 1, h - 1);
        dc.Pop();

        // The only lamp on the amp reports real playback, never a decorative idle glow.
        double pulse = HasAccompaniment ? .55 + .45 * Math.Abs(Math.Sin(time * 2.2)) : .16;
        dc.PushOpacity(pulse);
        Fill(dc, Warm, x0 + w - 8, top + 4, 2, 2);
        Fill(dc, Brush(Lighten(Glow.Color, .35)), x0 + w - 8, top + 4, 1, 1);
        dc.Pop();
    }

    private static void DrawGuitarCase(DrawingContext dc)
    {
        Color shell = Rgb(0x40, 0x3C, 0x2E).Color;

        // Contact pool: the case is heavy, so it presses a tight dark ellipse on the boards.
        dc.PushOpacity(.5);
        FillShape(dc, NearDark, 172, 125, 206, 133, (x, y) =>
        {
            double ex = (x - CaseCentre) / 16.0, ey = (y - 129) / 2.8;
            return ex * ex + ey * ey <= 1;
        });
        dc.Pop();

        FillShape(dc, Outline, 170, CaseTop - 1, 208, CaseBottom + 1, (x, y) =>
            CaseHalf(y) > 0 && Math.Abs(x - CaseAxis(y)) <= CaseHalf(y) + .7);

        // The shell is one volume lit from the lamp side, not a flat cut-out.
        FillShape(dc, Form(shell, 174, CaseTop, 30, CaseBottom - CaseTop, .30, .42),
            170, CaseTop, 208, CaseBottom, (x, y) =>
                CaseHalf(y) > 0 && Math.Abs(x - CaseAxis(y)) <= CaseHalf(y));

        // Rim: hardshell cases have a raised lip all the way round the lid. Two units of a
        // lighter vinyl is what stops the whole thing reading as a black door.
        FillShape(dc, Brush(Lighten(shell, .24)), 170, CaseTop, 208, CaseBottom, (x, y) =>
            CaseHalf(y) > 0 && Math.Abs(x - CaseAxis(y)) <= CaseHalf(y)
            && Math.Abs(x - CaseAxis(y)) > CaseHalf(y) - 2.2);

        // Inner panel sits a shade down so the rim reads as a lip, not a stripe.
        FillShape(dc, Brush(Darken(shell, .22)), 170, CaseTop + 2, 208, CaseBottom - 2, (x, y) =>
            CaseHalf(y) > 2.2 && Math.Abs(x - CaseAxis(y)) <= CaseHalf(y) - 2.2);

        // Lamp-side edge catches a hard line; the far side falls off into the wall.
        FillShape(dc, Brush(Lighten(shell, .42)), 170, CaseTop, 208, CaseBottom, (x, y) =>
            CaseHalf(y) > 0 && x >= CaseAxis(y) - CaseHalf(y)
            && x <= CaseAxis(y) - CaseHalf(y) + .9);
        FillShape(dc, Brush(Darken(shell, .58)), 170, CaseTop, 208, CaseBottom, (x, y) =>
            CaseHalf(y) > 0 && x >= CaseAxis(y) + CaseHalf(y) - 1.2
            && x <= CaseAxis(y) + CaseHalf(y));

        // Stickers: the small human marks that make a case a belonging rather than furniture.
        FillShape(dc, Cream, 180, 103, 208, 109, (x, y) =>
            x >= 182 && x < 188 && y >= 104 && y < 108 && CaseHalf(y) - 2.2 > Math.Abs(x - CaseAxis(y)));
        FillShape(dc, Rgb(0x8B, 0x46, 0x32), 180, 103, 208, 109, (x, y) =>
            x >= 182 && x < 188 && y == 104 && CaseHalf(y) - 2.2 > Math.Abs(x - CaseAxis(y)));

        FillShape(dc, Rgb(0xA8, 0x5A, 0x3C), 188, 110, 208, 116, (x, y) =>
            x >= 190 && x < 195 && y >= 111 && y < 114 && CaseHalf(y) - 2.2 > Math.Abs(x - CaseAxis(y)));

        FillShape(dc, Rgb(0x4E, 0x72, 0x66), 178, 118, 208, 124, (x, y) =>
            x >= 183 && x < 188 && y >= 119 && y < 122 && CaseHalf(y) - 2.2 > Math.Abs(x - CaseAxis(y)));

        // Latch plate at the waist, where the lid actually opens.
        int axis = (int)Math.Round(CaseAxis(97));
        Fill(dc, Brush(Darken(MetalDark.Color, .25)), axis - 3, 96, 6, 2);
        Fill(dc, Brush(Lighten(Metal.Color, .28)), axis - 3, 96, 6, 1);
    }

    // ---------------------------------------------------------------- case geometry

    private const double CaseCentre = 188;
    private const int CaseTop = 62;
    private const int CaseWaist = 96;
    private const int CaseBottom = 130;

    /// <summary>The case leans a touch, so it does not read as a door frame.</summary>
    private static double CaseAxis(double y) => CaseCentre + (y - CaseTop) * .03;

    /// <summary>
    /// A hardshell case is the guitar's own silhouette inflated by a lip of padding, so the
    /// body half widths come straight from <see cref="GuitarHalfWidth"/>.
    /// </summary>
    private static double CaseHalf(double y)
    {
        if (y < CaseTop || y > CaseBottom) return 0;
        if (y < CaseWaist)
        {
            double t = (y - CaseTop) / (CaseWaist - CaseTop);
            return 5.0 + t * 1.4;
        }
        return GuitarHalfWidth(y - 2) + 1.6;
    }

    private void DrawFloorPlant(DrawingContext dc, double time)
    {
        double sway = ReducedMotion ? 0 : Math.Sin(time * .8);
        const int cx = 88;

        // Tapered terracotta pot with one rim band.
        FillShape(dc, Outline, cx - 10, 108, cx + 11, 128, (x, y) =>
            x >= cx - 9 + (y - 114) * .14 && x <= cx + 9 - (y - 114) * .14 && y >= 114);
        FillShape(dc, Pot, cx - 9, 109, cx + 10, 127, (x, y) =>
            x >= cx - 8 + (y - 114) * .13 && x <= cx + 8 - (y - 114) * .13 && y >= 114);
        Fill(dc, PotLit, cx - 9, 109, 19, 3);
        Fill(dc, Rgb(0x2E, 0x1D, 0x12), cx - 9, 112, 19, 1);
        FillShape(dc, Rgb(0x2E, 0x1D, 0x12), cx - 9, 109, cx + 10, 128, (x, y) =>
            x > cx + 3 && x >= cx - 8 + (y - 114) * .13 && x <= cx + 8 - (y - 114) * .13 && y >= 114);
        Fill(dc, Rgb(0x1C, 0x13, 0x0C), cx - 7, 114, 15, 1);

        // Stems first, then rotated leaves attached along them, so nothing floats.
        // Four stems, each carrying a run of leaves. Density is what makes a plant read as a
        // plant at this size; a handful of leaves floating on stems reads as a diagram.
        ReadOnlySpan<(int X, int Top, double Phase, int Leaves)> stems =
            [(cx - 6, 100, .0, 4), (cx - 1, 92, 1.0, 5), (cx + 5, 97, .0, 4)];
        foreach (var stem in stems)
        {
            int drift = ReducedMotion ? 0 : (int)Math.Round(sway * 1.6);
            for (int y = stem.Top; y < 114; y++)
            {
                int t = y - stem.Top;
                int x = stem.X + drift * t / 26;
                Fill(dc, Stem, x, y, 1, 1);
            }
            for (int i = 0; i < stem.Leaves; i++)
            {
                int t = 3 + i * 6;
                int y = stem.Top + t;
                if (y > 112) break;
                int x = stem.X + drift * t / 26;
                bool left = (i + (int)stem.Phase) % 2 == 0;
                double angle = left ? Math.PI * .84 : Math.PI * .16;
                double bx = x + (left ? -1 : 1), by = y;
                double len = 3.8 + (i % 2) * .9;
                double tipX = bx + Math.Cos(angle) * (len * .45);
                double tipY = by + Math.Sin(angle) * (len * .45);

                // Shadowed underside, body, then a bright tip only on the lamp-facing leaves.
                FillLeaf(dc, LeafDeep, tipX, tipY, len + 1.4, 3.2, angle);
                FillLeaf(dc, left ? Leaf : LeafLit, tipX, tipY, len, 2.1, angle);
                if (!left)
                    FillLeaf(dc, LeafGlow, bx + Math.Cos(angle) * (len * .72), by + Math.Sin(angle) * (len * .72),
                        len * .42, 1.1, angle);
            }
        }
        // Crown leaves, hung at different heights so they never pair up into a heart.
        FillLeaf(dc, LeafDeep, cx - 3, 90, 5.2, 2.8, -Math.PI / 2 - .55);
        FillLeaf(dc, LeafLit, cx - 3, 90, 4.0, 1.9, -Math.PI / 2 - .55);
        FillLeaf(dc, LeafDeep, cx + 4, 93, 4.6, 2.5, -Math.PI / 2 + .42);
        FillLeaf(dc, LeafGlow, cx + 4, 93, 3.4, 1.7, -Math.PI / 2 + .42);
    }

    private void DrawCables(DrawingContext dc, double time)
    {
        var cable = new StreamGeometry();
        using (var g = cable.Open())
        {
            g.BeginFigure(new Point(158, 128), false, false);
            // A loose S-curve stays to the right of the rug and leads toward the floor.
            g.BezierTo(new Point(160, 143), new Point(140, 152), new Point(143, 168), true, false);
            g.BezierTo(new Point(146, 181), new Point(184, 180), new Point(187, 196), true, false);
        }
        cable.Freeze();
        dc.DrawGeometry(null, CablePen, cable);

        // A pulse travels the cable only when the independent input actually fires.
        double age = time - _inputPulseStarted;
        if (age is >= 0 and < .8)
        {
            double t = age / .8;
            double s = t < .5 ? t * 2 : (t - .5) * 2;
            static double B(double a, double b, double c, double d, double t) =>
                a * (1-t)*(1-t)*(1-t) + 3*b*t*(1-t)*(1-t) + 3*c*t*t*(1-t) + d*t*t*t;
            int px = (int)Math.Round(t < .5 ? B(158, 160, 140, 143, s) : B(143, 146, 184, 187, s));
            int py = (int)Math.Round(t < .5 ? B(128, 143, 152, 168, s) : B(168, 181, 180, 196, s));
            dc.PushOpacity(Math.Max(0, .9 - t));
            Fill(dc, Signal, px, py, 2, 2);
            dc.Pop();
        }
    }

    private static void DrawPedalboard(DrawingContext dc)
    {
        // The near edge of the board is lower and wider than its far edge. These are
        // room props, not UI controls: none claim to change an effect or audio setting.
        // Keep it below the in-scene status and quiet-mode controls.
        dc.PushTransform(new TranslateTransform(0, 22));
        var board = new StreamGeometry();
        using (var g = board.Open())
        {
            g.BeginFigure(new Point(7, 183), true, true);
            g.LineTo(new Point(48, 183), true, false);
            g.LineTo(new Point(53, 197), true, false);
            g.LineTo(new Point(3, 197), true, false);
        }
        board.Freeze();
        dc.PushOpacity(.48);
        Fill(dc, NearDark, 4, 196, 52, 4);
        dc.Pop();
        dc.DrawGeometry(Rgb(0x12, 0x16, 0x11), null, board);
        Fill(dc, Rgb(0x38, 0x39, 0x2B), 10, 186, 11, 7);
        Fill(dc, Rgb(0x63, 0x53, 0x37), 11, 186, 9, 1);
        Fill(dc, Rgb(0x84, 0x7A, 0x5E), 15, 190, 2, 2);
        Fill(dc, Rgb(0x34, 0x3D, 0x2B), 25, 186, 11, 7);
        Fill(dc, Rgb(0x6F, 0x78, 0x58), 26, 186, 9, 1);
        Fill(dc, Rgb(0x8A, 0x82, 0x67), 30, 190, 2, 2);
        Fill(dc, Rgb(0x39, 0x2D, 0x22), 40, 187, 7, 6);
        Fill(dc, Rgb(0x79, 0x62, 0x41), 41, 187, 5, 1);
        Fill(dc, Rgb(0x8A, 0x82, 0x67), 43, 190, 2, 2);
        dc.Pop();
    }

    private void DrawCatShadow(DrawingContext dc)
    {
        // The character has no shadow of its own; the floor supplies one so it is planted.
        // The row comes from the real layout, so it stays under the feet at any window size.
        if (_catFootY is not double foot || foot < 120 || foot > StageHeight + 12) return;
        double y = Math.Round(foot) + 1;
        dc.PushOpacity(.34);
        FillShape(dc, NearDark, CenterX - 20, (int)y - 4, CenterX + 20, (int)y + 6, (x, py) =>
        {
            double ex = (x - CenterX) / 17.0, ey = (py - y) / 2.8;
            return ex * ex + ey * ey <= 1;
        });
        dc.Pop();
    }

    // ----------------------------------------------------------------------- light

    private void DrawVolumetricLight(DrawingContext dc, double time, int scale, double ox, double oy)
    {
        double flicker = ReducedMotion ? 1 : 1 + .05 * Math.Sin(time * 3.1) + .035 * Math.Sin(time * 7.7 + 1.3);
        double level = Math.Clamp(Level / 60.0, 0, 1);
        double strength = Math.Clamp(flicker * (.85 + .25 * level), .7, 1.25);

        dc.PushTransform(new TranslateTransform(ox, oy));
        dc.PushTransform(new ScaleTransform(scale, scale));

        // A soft halo around the bulb is what makes the flat planes feel lit rather than painted.
        var halo = new RadialGradientBrush(
            Color.FromArgb((byte)Math.Clamp(74 * strength, 0, 255), 0xFF, 0xC8, 0x82),
            Color.FromArgb(0, 0xFF, 0xC8, 0x82));
        halo.Freeze();
        dc.DrawRectangle(halo, null, new Rect(128 - 44, 46 - 38, 88, 88));

        dc.Pop();
        dc.Pop();
    }

    private void DrawDust(DrawingContext dc, double time)
    {
        ReadOnlySpan<(int X, int Y, double Phase, double Speed)> motes =
        [
            (110, 118, .0, .16), (120, 106, 1.1, .12), (130, 124, 2.2, .2), (138, 110, 3.3, .14),
            (104, 130, 4.4, .18), (126, 94, 5.5, .1), (144, 118, 6.6, .16), (116, 98, 7.7, .13),
            (134, 132, 8.8, .19), (122, 130, 9.9, .11), (148, 104, 1.7, .15), (108, 110, 2.9, .17),
            (152, 128, 4.1, .12), (100, 120, 5.3, .15)
        ];
        double level = Math.Clamp(Level / 60.0, 0, 1);
        for (int i = 0; i < motes.Length; i++)
        {
            var mote = motes[i];
            double drift = ReducedMotion ? 0 : Math.Sin(time * .32 + mote.Phase) * 2.5;
            double rise = ReducedMotion ? 0 : (time * mote.Speed + mote.Phase) % 28;
            int x = (int)Math.Round(mote.X + drift);
            int y = (int)Math.Round(mote.Y - rise);
            dc.PushOpacity(Math.Clamp(.18 + .5 * level + .12 * Math.Sin(time + mote.Phase), .05, .75));
            Fill(dc, Glow, x, y, i % 3 == 0 ? 2 : 1, 1);
            dc.Pop();
        }
    }

    // ------------------------------------------------------------------ near plane

    private void DrawForeground(DrawingContext dc, double time)
    {
        // The near plane is drawn on double blocks: bigger pixels read as focus falloff
        // without a blur filter, and it never covers the cat or the panel.
        int drift = ReducedMotion ? 0 : (int)Math.Round(Math.Sin(time * .28) * 2) * 2;
        int drift2 = ReducedMotion ? 0 : (int)Math.Round(Math.Sin(time * .19 + 1.4) * 2) * 2;

        // The lamp is the only light source, so every near leaf keeps a lit edge on the
        // side that faces it. Without that rim the corner collapses into a black smear:
        // the silhouette alone carries no leaf shape on a floor this dark.
        const double LeftLight = -.88;
        const double RightLight = -2.06;

        // Two fans per corner — a broad one behind, a near-black one in front — so the
        // layers separate by their rims instead of by brightness.
        dc.PushOpacity(.55);
        DrawLeafFan(dc, NearMid, NearLit, -12 + drift, 216, 3, 32, 10, -1.22, .5, LeftLight, 2);

        DrawLeafFan(dc, NearMid, NearLit, 220 - drift, 216, 3, 32, 10, -2.1, .5, RightLight, 2);

        // A few leaves intrude from the side edges, so the near plane frames the shot
        // instead of only filling the bottom corners.
        DrawLeafFan(dc, NearMid, NearLit, -20, 190, 2, 26, 8, -.72, .3, LeftLight, 2);
        DrawLeafFan(dc, NearMid, NearLit, 228, 184, 2, 26, 8, -2.48, .3, RightLight, 2);

        // Two loose leaves drifting through the near plane, well away from the cat.
        FillBacklitLeaf(dc, NearMid, NearLit, 8 + drift2, 196, 6, 3, -.5, LeftLight, 2);
        FillBacklitLeaf(dc, NearMid, NearLit, 200 - drift2, 194, 6, 3, .6, RightLight, 2);
        dc.Pop();
    }

    private static void DrawLeafFan(DrawingContext dc, Brush body, Brush rim, double cx, double cy,
        int count, double length, double width, double startAngle, double spread,
        double lightAngle, int block = 1)
    {
        for (int i = 0; i < count; i++)
        {
            double t = count == 1 ? .5 : i / (double)(count - 1);
            double angle = startAngle + spread * t * Math.PI;
            double scale = .72 + .3 * Math.Sin(Math.PI * (t * .86 + .07));
            double half = length * scale * .5;
            // The leaf grows out of the base point, so the silhouette keeps separated tips
            // instead of collapsing into one dark mass.
            FillBacklitLeaf(dc, body, rim, cx + Math.Cos(angle) * half, cy + Math.Sin(angle) * half,
                half, width * scale * .5, angle, lightAngle, block);
        }
    }

    /// <summary>
    /// A near-plane leaf as two passes: a dark body and a lit band along the edge that
    /// faces the light. The band is the difference between the leaf and the same leaf
    /// pulled away from the light, which gives a clean crescent without any blur.
    /// </summary>
    private static void FillBacklitLeaf(DrawingContext dc, Brush body, Brush rim,
        double cx, double cy, double length, double width, double angle, double lightAngle, int block = 1)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        double lx = Math.Cos(lightAngle), ly = Math.Sin(lightAngle);
        double depth = Math.Max(1.5, width * .5);
        double innerX = cx - lx * depth, innerY = cy - ly * depth;
        double innerLength = Math.Max(1, length - depth * .5);
        double innerWidth = Math.Max(1, width - depth * .5);

        int reach = (int)Math.Ceiling(Math.Max(length, width)) + 2;
        int bx = (int)Math.Round(cx), by = (int)Math.Round(cy);
        int x0 = bx - reach, y0 = by - reach, x1 = bx + reach + 1, y1 = by + reach + 1;

        FillBlockShape(dc, body, x0, y0, x1, y1, block,
            (x, y) => InEllipse(x, y, cx, cy, length, width, cos, sin));
        FillBlockShape(dc, rim, x0, y0, x1, y1, block, (x, y) =>
            InEllipse(x, y, cx, cy, length, width, cos, sin) &&
            !InEllipse(x, y, innerX, innerY, innerLength, innerWidth, cos, sin));
    }

    private static bool InEllipse(double x, double y, double cx, double cy,
        double length, double width, double cos, double sin)
    {
        double u = (x - cx) * cos + (y - cy) * sin;
        double v = -(x - cx) * sin + (y - cy) * cos;
        return u * u / (length * length) + v * v / (width * width) <= 1;
    }

    private static void DrawVignette(DrawingContext dc, int left, int right, int top, int bottom)
    {
        // Stepped bands instead of a smooth gradient keep the picture inside a pixel palette.
        for (int i = 0; i < 6; i++)
        {
            dc.PushOpacity(.05);
            int inset = i * 4;
            Fill(dc, NearDark, left, top, right - left, 3);
            Fill(dc, NearDark, left, bottom - 3, right - left, 3);
            Fill(dc, NearDark, left + inset, top, 3, bottom - top);
            Fill(dc, NearDark, right - inset - 3, top, 3, bottom - top);
            dc.Pop();
        }
    }

    // ---------------------------------------------------------------------- helpers

    // The bulb, in room units. Every form gradient on every object points at this spot,
    // which is what stops a box from reading as a flat swatch of colour.
    private const double LampX = 128;
    private const double LampY = 60;

    private static Color Mix(Color a, Color b, double t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t),
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    private static Color Lighten(Color c, double amount) =>
        Mix(c, Color.FromRgb(0xFF, 0xE4, 0xBE), Math.Clamp(amount, 0, 1));

    private static Color Darken(Color c, double amount) =>
        Mix(c, Color.FromRgb(0x04, 0x08, 0x06), Math.Clamp(amount, 0, 1));

    /// <summary>
    /// A linear gradient that runs from the face turned toward the lamp to the face turned
    /// away from it. Mapped in absolute room units so the same brush keeps one continuous
    /// ramp across the many runs that make up a pixel shape, instead of restarting on each.
    /// </summary>
    private static LinearGradientBrush Form(Color baseColor, double x, double y, double w, double h,
        double litGain = .34, double shadeLoss = .52)
    {
        double cx = x + w * .5, cy = y + h * .5;
        double dx = LampX - cx, dy = LampY - cy;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1) { dx = 0; dy = -1; len = 1; }
        double ux = dx / len, uy = dy / len;
        // Span the gradient across the object's own extent along the light direction.
        // If the ramp reaches past the object the form is crushed into its shadow half.
        double reach = (Math.Abs(ux) * w + Math.Abs(uy) * h) * .5 + .5;
        var brush = new LinearGradientBrush(
            Lighten(baseColor, litGain), Darken(baseColor, shadeLoss),
            new Point(cx + ux * reach, cy + uy * reach),
            new Point(cx - ux * reach, cy - uy * reach))
        {
            MappingMode = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>Vertical form gradient: top catches the shade, bottom falls into shadow.</summary>
    private static LinearGradientBrush FormVertical(Color baseColor, double y, double h,
        double litGain = .3, double shadeLoss = .55)
    {
        var brush = new LinearGradientBrush(
            Lighten(baseColor, litGain), Darken(baseColor, shadeLoss),
            new Point(0, y), new Point(0, y + h))
        {
            MappingMode = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static (int X, int Y, byte Tone)[] BuildGrain(int width, int height, int count)
    {
        var result = new (int, int, byte)[count];
        uint state = 0x9E3779B9;
        for (int i = 0; i < count; i++)
        {
            state = state * 1664525u + 1013904223u;
            int x = (int)((state >> 9) % (uint)width);
            state = state * 1664525u + 1013904223u;
            int y = (int)((state >> 9) % (uint)height);
            result[i] = (x, y, (byte)(i % 4 == 0 ? 2 : i % 2));
        }
        return result;
    }

    private static void Fill(DrawingContext dc, Brush brush, int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return;
        dc.DrawRectangle(brush, null, new Rect(x, y, w, h));
    }

    /// <summary>
    /// Draws a rotated ellipse on whole pixels. Leaves and out-of-focus foliage need a
    /// direction, which a plain axis-aligned ellipse cannot express.
    /// </summary>
    /// <param name="block">
    /// Pixel block size. Near-plane foliage uses 2, so its pixels are visibly coarser than
    /// the room's — the classic pixel-art stand-in for depth of field.
    /// </param>
    private static void FillLeaf(DrawingContext dc, Brush brush, double cx, double cy,
        double length, double width, double angle, int block = 1)
    {
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        int reach = (int)Math.Ceiling(Math.Max(length, width)) + 2;
        int ix = (int)Math.Round(cx), iy = (int)Math.Round(cy);
        FillBlockShape(dc, brush, ix - reach, iy - reach, ix + reach + 1, iy + reach + 1, block, (x, y) =>
        {
            double u = (x - cx) * cos + (y - cy) * sin;
            double v = -(x - cx) * sin + (y - cy) * cos;
            return u * u / (length * length) + v * v / (width * width) <= 1;
        });
    }

    private static void FillBlockShape(DrawingContext dc, Brush brush, int x0, int y0, int x1, int y1,
        int block, Func<int, int, bool> inside)
    {
        if (block <= 1)
        {
            FillShape(dc, brush, x0, y0, x1, y1, inside);
            return;
        }
        int half = block / 2;
        x0 -= x0 % block;
        y0 -= y0 % block;
        for (int y = y0; y < y1; y += block)
        {
            int runStart = int.MinValue;
            for (int x = x0; x <= x1; x += block)
            {
                bool hit = x < x1 && inside(x + half, y + half);
                if (hit && runStart == int.MinValue) runStart = x;
                else if (!hit && runStart != int.MinValue)
                {
                    dc.DrawRectangle(brush, null, new Rect(runStart, y, x - runStart, block));
                    runStart = int.MinValue;
                }
            }
        }
    }

    private static void FillShape(DrawingContext dc, Brush brush, int x0, int y0, int x1, int y1,
        Func<int, int, bool> inside)
    {
        for (int y = y0; y < y1; y++)
        {
            int runStart = int.MinValue;
            for (int x = x0; x <= x1; x++)
            {
                bool hit = x < x1 && inside(x, y);
                if (hit && runStart == int.MinValue) runStart = x;
                else if (!hit && runStart != int.MinValue)
                {
                    dc.DrawRectangle(brush, null, new Rect(runStart, y, x - runStart, 1));
                    runStart = int.MinValue;
                }
            }
        }
    }

    private static SolidColorBrush Rgb(byte r, byte g, byte b, byte a = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static SolidColorBrush Blend(SolidColorBrush a, SolidColorBrush b, double t)
    {
        var ca = a.Color;
        var cb = b.Color;
        return Rgb((byte)(ca.R + (cb.R - ca.R) * t), (byte)(ca.G + (cb.G - ca.G) * t),
            (byte)(ca.B + (cb.B - ca.B) * t));
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
