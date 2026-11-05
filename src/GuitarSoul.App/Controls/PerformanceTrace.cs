using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using GuitarSoul.Core.Game;

namespace GuitarSoul.App.Controls;

/// <summary>
/// A layered spatial trace: measured player input enters the cat's resonance floor,
/// then re-emerges toward the distant accompaniment highlight. The renderer consumes
/// measured input level and deterministic game state; it does not invent a score.
/// </summary>
public sealed class PerformanceTrace : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(PerformanceTrace),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MomentProperty = DependencyProperty.Register(
        nameof(Moment), typeof(GameMomentState), typeof(PerformanceTrace),
        new FrameworkPropertyMetadata(GameMomentState.Ready, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PlayerActiveProperty = DependencyProperty.Register(
        nameof(PlayerActive), typeof(bool), typeof(PerformanceTrace),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasAccompanimentProperty = DependencyProperty.Register(
        nameof(HasAccompaniment), typeof(bool), typeof(PerformanceTrace),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public GameMomentState Moment
    {
        get => (GameMomentState)GetValue(MomentProperty);
        set => SetValue(MomentProperty, value);
    }

    public bool PlayerActive
    {
        get => (bool)GetValue(PlayerActiveProperty);
        set => SetValue(PlayerActiveProperty, value);
    }

    public bool HasAccompaniment
    {
        get => (bool)GetValue(HasAccompanimentProperty);
        set => SetValue(HasAccompanimentProperty, value);
    }

    public bool ReducedMotion { get; set; }

    public PerformanceTrace()
    {
        IsHitTestVisible = false;
        UseLayoutRounding = true;
        AutomationProperties.SetName(this, "玩家输入从猫脚下的共鸣区汇入伴奏高光的实时轨迹");
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 10 || ActualHeight < 10) return;

        double measured = Math.Clamp(Level / 60, 0, 1);
        bool active = Moment is GameMomentState.Following or GameMomentState.Anticipating or
            GameMomentState.Together or GameMomentState.SongHighlight or GameMomentState.Release;
        double strength = PlayerActive && active ? Math.Max(.24, measured) : 0;
        bool highlight = Moment is GameMomentState.Anticipating or GameMomentState.Together or GameMomentState.SongHighlight;

        var destination = new Point(ActualWidth * .86, ActualHeight * .27);
        if (HasAccompaniment) DrawAccompanimentHorizon(dc, strength);

        var playerColor = Moment == GameMomentState.Together
            ? Color.FromRgb(234, 221, 133)
            : Color.FromRgb(198, 216, 145);
        byte alpha = (byte)Math.Round(45 + 195 * strength);
        var approachStart = new Point(ActualWidth * .1, ActualHeight * .94);
        var approachControl1 = new Point(ActualWidth * .24, ActualHeight * .93);
        var approachControl2 = new Point(ActualWidth * .38, ActualHeight * .9);
        var mergeLeft = new Point(ActualWidth * .435, ActualHeight * .86);
        var mergeRight = new Point(ActualWidth * .58, ActualHeight * .86);
        var exitControl1 = new Point(ActualWidth * .7, ActualHeight * .8);
        var exitControl2 = new Point(ActualWidth * .82, ActualHeight * .55);
        var approachGeometry = Curve(approachStart, approachControl1, approachControl2, mergeLeft);
        var exitGeometry = Curve(mergeRight, exitControl1, exitControl2, destination);

        if (PlayerActive)
        {
            DrawMergeFloor(dc, mergeLeft, mergeRight, playerColor, strength, HasAccompaniment);
            DrawRibbon(dc, approachGeometry, playerColor, strength, alpha);
            DrawRibbonMarkers(dc, approachStart, approachControl1, approachControl2, mergeLeft,
                [.12, .34, .58, .82], playerColor, strength, alpha, 0, .44);
            if (HasAccompaniment)
            {
                DrawRibbon(dc, exitGeometry, playerColor, strength, alpha);
                DrawRibbonMarkers(dc, mergeRight, exitControl1, exitControl2, destination,
                    [.12, .3, .48, .66, .82, .93], playerColor, strength, alpha, .56, 1);
            }
        }

        if (HasAccompaniment) DrawBeaconSpectrum(dc, destination, strength, highlight);

        if (HasAccompaniment && highlight)
        {
            var amber = Color.FromRgb(225, 151, 74);
            for (int ring = 6; ring >= 1; ring--)
            {
                double radius = 8 + ring * 9;
                var glow = new SolidColorBrush(Color.FromArgb((byte)(7 + 6 * (7 - ring)), amber.R, amber.G, amber.B));
                glow.Freeze();
                dc.DrawEllipse(glow, null, destination, radius * 1.25, radius * .72);
            }
            var core = new SolidColorBrush(Color.FromRgb(247, 204, 109));
            core.Freeze();
            dc.DrawEllipse(core, null, destination, 4.2, 4.2);
        }
    }

    private static void DrawRibbon(DrawingContext dc, Geometry geometry, Color playerColor, double strength, byte alpha)
    {
        foreach (var (width, veilAlpha) in new[]
        {
            (22d + strength * 7, (byte)Math.Round(3 + 6 * strength)),
            (11d + strength * 5, (byte)Math.Round(7 + 12 * strength)),
            (5d + strength * 2, (byte)Math.Round(12 + 19 * strength))
        })
        {
            var veil = new Pen(new SolidColorBrush(Color.FromArgb(veilAlpha, playerColor.R, playerColor.G, playerColor.B)), width)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            veil.Freeze();
            dc.DrawGeometry(null, veil, geometry);
        }

        var playerPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, playerColor.R, playerColor.G, playerColor.B)),
            1.45 + 2.15 * strength)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        playerPen.Freeze();
        dc.DrawGeometry(null, playerPen, geometry);
    }

    private static void DrawRibbonMarkers(DrawingContext dc, Point start, Point control1, Point control2, Point end,
        ReadOnlySpan<double> positions, Color playerColor, double strength, byte alpha, double progressStart, double progressEnd)
    {
        var marker = new SolidColorBrush(Color.FromArgb((byte)Math.Min(230, alpha + 25), playerColor.R, playerColor.G, playerColor.B));
        marker.Freeze();
        foreach (double t in positions)
        {
            var point = Cubic(start, control1, control2, end, t);
            double progress = progressStart + (progressEnd - progressStart) * t;
            double size = 2 + progress * 4 + 1.8 * strength;
            dc.DrawRectangle(marker, null, new Rect(point.X - size / 2, point.Y - size / 2, size, size));
        }
    }

    private static void DrawMergeFloor(DrawingContext dc, Point left, Point right, Color color, double strength, bool connected)
    {
        var center = new Point((left.X + right.X) / 2, (left.Y + right.Y) / 2);
        double radiusX = (right.X - left.X) / 2 + 12;
        double radiusY = 10 + 6 * strength;
        for (int ring = 3; ring >= 1; ring--)
        {
            var glow = new SolidColorBrush(Color.FromArgb((byte)(4 + (4 - ring) * 5), color.R, color.G, color.B));
            glow.Freeze();
            dc.DrawEllipse(glow, null, center, radiusX + ring * 7, radiusY + ring * 3);
        }

        var ringPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(58 + strength * 64), color.R, color.G, color.B)), 1.05);
        ringPen.Freeze();
        dc.DrawEllipse(null, ringPen, center, radiusX, radiusY);
        var node = new SolidColorBrush(Color.FromArgb((byte)(125 + strength * 90), color.R, color.G, color.B));
        node.Freeze();
        dc.DrawRectangle(node, null, new Rect(left.X - 2.5, left.Y - 2.5, 5, 5));
        if (connected)
            dc.DrawRectangle(node, null, new Rect(right.X - 2.5, right.Y - 2.5, 5, 5));
    }

    private void DrawAccompanimentHorizon(DrawingContext dc, double strength)
    {
        const int count = 28;
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)(count - 1);
            double x = ActualWidth * (.055 + .7 * t);
            double y = ActualHeight * (.32 + .025 * Math.Sin(t * Math.PI * 1.35));
            double envelope = .35 + .65 * Math.Abs(Math.Sin(i * 1.19 + .6));
            double height = (2.5 + envelope * 7) * (.7 + strength * .25);
            byte alpha = (byte)Math.Round(18 + envelope * 30);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 137, 164, 148)), i % 5 == 0 ? 1.5 : 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(x, y - height / 2), new Point(x, y + height / 2));
        }
    }

    private static void DrawBeaconSpectrum(DrawingContext dc, Point destination, double strength, bool highlight)
    {
        const int count = 31;
        double baseline = destination.Y + 1;
        for (int i = 0; i < count; i++)
        {
            double normalized = (i - (count - 1) / 2d) / ((count - 1) / 2d);
            double envelope = Math.Exp(-normalized * normalized * 3.6);
            double texture = .38 + .62 * Math.Abs(Math.Sin(i * 1.73 + .4));
            double height = (5 + 73 * envelope * texture) * (.42 + strength * .58);
            double x = destination.X + normalized * 92;
            byte alpha = (byte)Math.Round((highlight ? 95 : 32) + envelope * (highlight ? 118 : 52));
            var warm = normalized < -.2 ? Color.FromRgb(218, 207, 123)
                : normalized > .55 ? Color.FromRgb(207, 93, 66)
                : Color.FromRgb(245, 167, 75);
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Min(70, alpha / 3), warm.R, warm.G, warm.B)), 6);
            glowPen.Freeze();
            dc.DrawLine(glowPen, new Point(x, baseline - height * .5), new Point(x, baseline + height * .5));
            var barPen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, warm.R, warm.G, warm.B)), 2.2);
            barPen.Freeze();
            dc.DrawLine(barPen, new Point(x, baseline - height * .5), new Point(x, baseline + height * .5));
        }

        var horizon = new Pen(new SolidColorBrush(Color.FromArgb((byte)(38 + strength * 90), 232, 153, 73)), 1.25);
        horizon.Freeze();
        dc.DrawLine(horizon, new Point(destination.X - 112, baseline), new Point(destination.X + 132, baseline));
    }

    private static StreamGeometry Curve(Point start, Point control1, Point control2, Point end)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.BezierTo(control1, control2, end, true, false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point Cubic(Point a, Point b, Point c, Point d, double t)
    {
        double u = 1 - t;
        double uu = u * u;
        double tt = t * t;
        return new Point(
            uu * u * a.X + 3 * uu * t * b.X + 3 * u * tt * c.X + tt * t * d.X,
            uu * u * a.Y + 3 * uu * t * b.Y + 3 * u * tt * c.Y + tt * t * d.Y);
    }
}
