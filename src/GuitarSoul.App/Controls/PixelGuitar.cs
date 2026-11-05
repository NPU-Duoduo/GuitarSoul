using System.Windows;
using System.Windows.Media;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App.Controls;

/// <summary>Jam-only instruments, sampled directly onto the cat's 40 × 42 grid.</summary>
internal static class PixelGuitar
{
    private static readonly Brush Red = Color("#AB3443"), RedLight = Color("#D65A59"),
        RedDark = Color("#6F2638"), Cream = Color("#F3E2B6"), Gold = Color("#CA9343"),
        Amber = Color("#E9A340"), AmberLight = Color("#F8C45D"), Burst = Color("#AC482F"),
        Wood = Color("#CDA370"), Board = Color("#594237"), Steel = Color("#B6B5AC"),
        Pickup = Color("#645F56"), Strap = Color("#51404A"), Scarf = Color("#BA5855");

    // Reference landmarks in instrument coordinates: tail at x=0, neck towards +x.
    // Keep the two open cutaways and the pinched waist in the contour itself.
    // Sampling after the held-angle transform avoids rotating an already tiny bitmap.
    private static readonly Geometry StratBody = Shape(
        "M 0,0 C 0,-3.8 1.5,-6.2 4.5,-6.2 C 7,-6.2 9,-3.5 11.4,-3.5 " +
        "C 13.7,-3.5 16,-6 18.5,-5.5 Q 20,-5.2 20,-4.4 " +
        "Q 20,-3.6 18.6,-3.7 C 16.7,-3.8 15.6,-2.7 15.6,-1.1 " +
        "L 19,-1.1 19,1.1 14,1.1 Q 12.8,2.9 14.9,3.6 " +
        "Q 16,3.8 17.5,3.1 Q 18.2,3.2 17.3,4.5 " +
        "C 16,7 13.8,5.4 11.4,4.1 C 9.6,3.4 7.4,5.5 4.7,6.2 " +
        "C 1.8,7 0,4 0,0 Z");
    private static readonly Geometry LesPaulBody = Shape(
        "M 0,0 C 0,-3.8 2,-6.4 5,-6.4 C 8,-6.4 9.5,-3.6 11.1,-3.5 " +
        "C 12.8,-3.5 13.2,-5.3 15.4,-5.1 Q 17.8,-5 18.6,-2.3 " +
        "L 19,-1.1 19,1.1 17.6,1.1 Q 15.3,1.7 15.5,2.9 " +
        "Q 15.6,3.7 16.7,3.4 Q 17.4,3.3 17.1,4.5 " +
        "Q 16.5,6.8 14.2,6 Q 12.7,5 11.2,3.9 " +
        "C 9.6,3.2 8.4,6.6 5,6.6 C 1.8,6.6 0,4.4 0,0 Z");
    private static readonly Geometry StratGuard = Shape(
        "M 7,-2.3 L 13.7,-2.6 15.5,-1 17.5,-1 17.5,1 13.4,1.2 " +
        "Q 12.1,3 14.4,4.3 L 11.6,3.7 9.2,3.8 6.6,2.3 Z");
    private static readonly Geometry LesPaulGuard = Shape("M 14.6,1.2 L 14.4,4.1 8.6,3.2 10,1.3 Z");
    private static readonly Geometry StratHead = Shape(
        "M 30,-1.1 L 33,-2.3 35.4,-2.3 Q 37,-2 37,-.5 Q 37,1.4 35.2,1.8 " +
        "L 32,1.1 30,1.1 Z");
    private static readonly Geometry LesPaulHead = Shape(
        "M 30,-1 L 32,-1.7 36,-1.7 36.8,-1 36.1,0 36.8,1 36,1.7 32,1.7 30,1 Z");
    private static readonly Geometry SgBody = Shape(
        "M 0,0 C 0,-5 3,-6 6,-5.5 L 11,-3.4 18,-7.3 20,-7.5 18,-4 15,-2 " +
        "L 19,-1 19,1 15,2 18,4 19,6.3 17,6 11,3.4 6,5.5 C 2,6 0,4 0,0 Z");
    private static readonly Geometry DoubleBody = Shape(
        "M 0,0 C 0,-6 2,-8 6,-8 L 12,-6 18,-10 20,-10 18,-6 16,-4 " +
        "L 19,-4 19,4 16,4 18,6 19,9 16,8 11,6 6,8 C 2,8 0,6 0,0 Z");
    private static readonly Geometry LucilleBody = Shape(
        "M 0,0 C 0,-5 2,-8 6,-8 C 10,-8 10,-4 13,-4 " +
        "C 13,-8 19,-8 19,-4 L 17,-1 19,-1 19,1 17,1 " +
        "C 22,8 14,9 13,4 C 10,4 10,8 6,8 C 2,8 0,5 0,0 Z");
    private static readonly Geometry MustangBody = Shape(
        "M 0,1 C 0,-3 2,-5 5,-5 C 8,-5 10,-3 13,-3 " +
        "L 18,-5 19,-4 16,-1 19,-1 19,1 15,1 Q 14,3 17,4 " +
        "Q 16,6 12,4 C 9,5 6,8 3,7 C 0,6 0,3 0,1 Z");
    private static readonly Drawing Stratocaster = Build(true, false);
    private static readonly Drawing LesPaul = Build(false, false);
    private static readonly Drawing StratSilhouette = Build(true, true);
    private static readonly Drawing LesPaulSilhouette = Build(false, true);
    private static readonly Drawing PsychStrat = Build(true, false, StageStyle.Psychedelic);
    private static readonly Drawing MellowStrat = Build(true, false, StageStyle.Mellow);
    private static readonly Dictionary<StageStyle, Drawing> ArtistGuitars = new()
    {
        [StageStyle.JimmyPage] = Build(false, false, StageStyle.JimmyPage),
        [StageStyle.DavidGilmour] = Build(true, false, StageStyle.DavidGilmour),
        [StageStyle.BBKing] = Build(false, false, StageStyle.BBKing),
        [StageStyle.AngusYoung] = Build(false, false, StageStyle.AngusYoung),
        [StageStyle.KurtCobain] = Build(true, false, StageStyle.KurtCobain),
        [StageStyle.NileRodgers] = Build(true, false, StageStyle.NileRodgers)
    };

    internal static void DrawInstrumentPreview(DrawingContext dc, GuitarStyle style, bool silhouette = false)
    {
        if (style == GuitarStyle.Stratocaster) dc.DrawDrawing(silhouette ? StratSilhouette : Stratocaster);
        else if (style == GuitarStyle.LesPaul) dc.DrawDrawing(silhouette ? LesPaulSilhouette : LesPaul);
    }

    internal static void DrawHeld(DrawingContext dc, CatPalette cat, GuitarStyle style, double stroke, double lift,
        StageStyle stage = StageStyle.Everyday)
    {
        if (style is not (GuitarStyle.Stratocaster or GuitarStyle.LesPaul)) return;
        if (stage is StageStyle.Psychedelic or StageStyle.KurtCobain)
        {
            dc.PushTransform(new TranslateTransform(40, 0));
            dc.PushTransform(new ScaleTransform(-1, 1));
        }
        if (stage == StageStyle.Everyday)
        {
            R(dc, Scarf, 15, 25, 10, 1);
            R(dc, Scarf, 23, 26, 2, 2);
        }
        R(dc, Strap, 13, 25, 2, 2);
        R(dc, Strap, 14, 27, 2, 2);

        int rise = lift >= .5 ? 1 : 0;
        int strum = stroke > .55 ? 1 : stroke < -.55 ? -1 : 0;
        dc.PushTransform(new TranslateTransform(0, -rise));
        // The fretting forearm sits behind the instrument, leaving both cutaways visible.
        R(dc, CatPalette.Ink, 25, 29, 3, 5);
        R(dc, cat.Fur, 26, 30, 1, 3);
        if (stage == StageStyle.Psychedelic) dc.DrawDrawing(PsychStrat);
        else if (stage == StageStyle.Mellow) dc.DrawDrawing(MellowStrat);
        else if (ArtistGuitars.TryGetValue(stage, out var artistGuitar)) dc.DrawDrawing(artistGuitar);
        else DrawInstrumentPreview(dc, style);
        R(dc, CatPalette.Ink, 27, 27, 3, 3);
        R(dc, cat.Bib, 27, 27, 2, 2);
        R(dc, cat.Shade, 28, 28, 1, 1);
        // Compact strumming arm descends from the near shoulder onto the bridge.
        R(dc, CatPalette.Ink, 12, 27, 3, 4);
        R(dc, cat.Fur, 13, 27, 1, 3);
        R(dc, CatPalette.Ink, 11, 30 + strum, 4, 3);
        R(dc, cat.Bib, 12, 30 + strum, 2, 2);
        dc.Pop();
        if (stage is StageStyle.Psychedelic or StageStyle.KurtCobain) { dc.Pop(); dc.Pop(); }
    }

    private static Drawing Build(bool strat, bool silhouette, StageStyle stage = StageStyle.Everyday)
    {
        var body = stage switch { StageStyle.JimmyPage => DoubleBody, StageStyle.AngusYoung => SgBody,
            StageStyle.BBKing => LucilleBody, StageStyle.KurtCobain => MustangBody, _ => strat ? StratBody : LesPaulBody };
        var guard = strat ? StratGuard : LesPaulGuard;
        var head = strat ? StratHead : LesPaulHead;
        const double angle = -14 * Math.PI / 180;
        double cosine = Math.Cos(angle), sine = Math.Sin(angle);
        double originY = stage == StageStyle.JimmyPage ? 31 : stage == StageStyle.BBKing ? 33 : 35;
        Point Local(double x, double y) => new(
            cosine * (x - 3) + sine * (y - originY),
            -sine * (x - 3) + cosine * (y - originY));
        bool In(Geometry geometry, Point p) => geometry.FillContains(p);
        bool Neck(Point p) => p.X is >= 16 and <= 31 && Math.Abs(p.Y) <= 1.2;
        bool Tuner(Point p)
        {
            if (strat)
                return p.Y is >= -3.2 and <= -1.7 && p.X >= 31 && p.X <= 36.8 &&
                    (p.X - 31) % 1.15 < .65;
            return Math.Abs(p.Y) is >= 1.5 and <= 2.8 && p.X >= 32 && p.X <= 36.8 &&
                (p.X - 32) % 1.8 < .85;
        }
        var cells = new Brush?[40, 42];
        Brush stageBody = stage == StageStyle.Psychedelic ? Color("#EEE8D2") : Color("#87B5A4");
        Brush stageHighlight = stage == StageStyle.Psychedelic ? Color("#FFFAE8") : Color("#B2D3BD");
        Brush blackBody = Color("#292D35"), blackLight = Color("#51565C"), lakeBlue = Color("#6EA7B5"), silverGuard = Color("#CCD1CF"),
            whiteHighlight = Color("#FFF9E7"), whiteBody = Color("#EDE8D8");
        var bodyCells = new bool[40, 42];
        for (int y = 0; y < 42; y++)
        for (int x = 0; x < 40; x++)
        {
            var p = Local(x + .5, y + .5);
            bodyCells[x, y] = In(body, p);
            // The double-neck body is shared; both fretboards, heads and pickup pairs remain separate.
            var n = stage == StageStyle.JimmyPage ? new Point(p.X, p.Y - (p.Y < 0 ? -3.4 : 3.4)) : p;
            Brush? fill = null;
            if (bodyCells[x, y])
            {
                // Discrete sunburst bands keep the reference's red rim and amber centre.
                double radius = Math.Sqrt(Math.Pow((p.X - 6) / 8, 2) + Math.Pow(p.Y / 6.5, 2));
                fill = strat ? (p.Y < -3 ? RedLight : Red) :
                    radius > .83 ? RedDark : radius > .65 ? Burst : radius > .4 ? Amber : AmberLight;
                if (stage is StageStyle.Psychedelic or StageStyle.Mellow) fill = p.Y < -3 ? stageHighlight : stageBody;
                if (stage is StageStyle.DavidGilmour or StageStyle.BBKing) fill = p.Y < -4 ? blackLight : blackBody;
                if (stage == StageStyle.NileRodgers) fill = p.Y < -3 ? whiteHighlight : whiteBody;
                if (stage == StageStyle.KurtCobain) fill = p.Y < -3 ? silverGuard : lakeBlue;
                if (stage is StageStyle.JimmyPage or StageStyle.AngusYoung) fill = p.Y < -3 ? RedLight : RedDark;
                if (In(guard, n)) fill = stage is StageStyle.DavidGilmour or StageStyle.JimmyPage or StageStyle.AngusYoung ? blackBody
                    : stage == StageStyle.NileRodgers ? silverGuard : Cream;
                if (strat)
                {
                    if (Math.Abs(p.Y) < 1.6 &&
                        (Math.Abs(p.X - 14) < .55 || Math.Abs(p.X - 11) < .55 ||
                         Math.Abs(p.X - (8 + p.Y * .3)) < .55)) fill = Pickup;
                    if (p.X is > 5.8 and < 7 && Math.Abs(p.Y) < 1.7) fill = Steel;
                    if (p.Y is > 2.2 and < 3.1 && p.X is > 8.8 and < 9.8) fill = Gold;
                    if (stage == StageStyle.KurtCobain)
                    {
                        if (Math.Abs(p.Y) < 1.6 && Math.Abs(p.X - 11) < .7) fill = Cream;
                        if (Math.Abs(p.Y) < 1.7 && (Math.Abs(p.X - 8) < 1 || Math.Abs(p.X - 14) < .65)) fill = Pickup;
                        if (p.X is > 4 and < 6 && Math.Abs(p.Y) < 2) fill = Steel;
                    }
                }
                else
                {
                    // Cream mounting rings around two dark double-coil blocks.
                    foreach (double pickupX in new[] { 9d, 14d })
                        if (Math.Abs(n.X - pickupX) < 1.25 && Math.Abs(n.Y) < 1.9)
                            fill = Math.Abs(n.X - pickupX) < .8 && Math.Abs(n.Y) < 1.4 ? (stage == StageStyle.BBKing ? Gold : Pickup) : Cream;
                    if ((Math.Abs(n.X - 6.5) < .45 || Math.Abs(n.X - 4.8) < .45) && Math.Abs(n.Y) < 1.8)
                        fill = stage == StageStyle.BBKing ? Gold : Steel;
                    if ((Math.Abs(p.X - 6) < .6 || Math.Abs(p.X - 8.7) < .6) &&
                        (Math.Abs(p.Y - 3.2) < .6 || Math.Abs(p.Y - 5) < .6)) fill = Gold;
                    if (Math.Abs(p.X - 15.3) < .65 && Math.Abs(p.Y + 3.4) < .65) fill = Cream;
                }
            }
            if (Neck(n))
            {
                fill = Math.Abs(n.Y) > .7 ? CatPalette.Ink : stage is StageStyle.DavidGilmour or StageStyle.NileRodgers ? Wood : Board;
                if (Math.Abs(n.Y) <= .7 && n.X > 18 && (n.X - 18) % 3.1 < .6) fill = Steel;
                if (Math.Abs(n.Y) < .4 && n.X > 20 && (n.X - 20) % 6.2 < 1) fill = Cream;
            }
            if (In(head, n))
                fill = strat ? Wood : Board;
            if (Tuner(n)) fill = stage == StageStyle.BBKing ? Gold : strat ? Steel : Cream;
            cells[x, y] = fill;
        }

        bool Occupied(int x, int y) => x is >= 0 and < 40 && y is >= 0 and < 42 && cells[x, y] != null;
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        for (int y = 0; y < 42; y++)
        for (int x = 0; x < 40; x++)
        {
            if (cells[x, y] is not Brush fill) continue;
            bool edge = !Occupied(x - 1, y) || !Occupied(x + 1, y) || !Occupied(x, y - 1) || !Occupied(x, y + 1);
            // Outline the body on the destination grid, so it has the cat's one-cell weight.
            R(dc, silhouette ? CatPalette.Ink : edge && bodyCells[x, y] ? CatPalette.Ink : fill, x, y, 1, 1);
        }
        drawing.Freeze();
        return drawing;
    }

    private static void R(DrawingContext dc, Brush brush, int x, int y, int width, int height) =>
        dc.DrawRectangle(brush, null, new Rect(x, y, width, height));
    private static Geometry Shape(string data) { var shape = Geometry.Parse(data); shape.Freeze(); return shape; }
    private static Brush Color(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze(); return brush;
    }
}
