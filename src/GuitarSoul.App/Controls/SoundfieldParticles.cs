using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using GuitarSoul.Core.Game;

namespace GuitarSoul.App.Controls;

/// <summary>
/// A state-driven atmospheric particle veil. The field stays restrained at rest and
/// gathers around the measured player trace and distant highlight when the performance is active.
/// </summary>
public sealed class SoundfieldParticles : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level), typeof(double), typeof(SoundfieldParticles),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MomentProperty = DependencyProperty.Register(
        nameof(Moment), typeof(GameMomentState), typeof(SoundfieldParticles),
        new FrameworkPropertyMetadata(GameMomentState.Ready, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PlayerActiveProperty = DependencyProperty.Register(
        nameof(PlayerActive), typeof(bool), typeof(SoundfieldParticles),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty HasAccompanimentProperty = DependencyProperty.Register(
        nameof(HasAccompaniment), typeof(bool), typeof(SoundfieldParticles),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _timer;
    private bool _reducedMotion;

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

    public bool ReducedMotion
    {
        get => _reducedMotion;
        set
        {
            if (_reducedMotion == value) return;
            _reducedMotion = value;
            InvalidateVisual();
        }
    }

    public SoundfieldParticles()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        UseLayoutRounding = true;
        AutomationProperties.SetName(this, "缓慢漂浮的环境微粒；实际输入与共同高光会增强对应区域");
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Render,
            (_, _) =>
            {
                if (!_reducedMotion && IsVisible)
                    InvalidateVisual();
            }, Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private bool IsPerformanceActive => PlayerActive || (HasAccompaniment &&
        (Moment is GameMomentState.Anticipating or GameMomentState.Together or GameMomentState.SongHighlight));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 10 || ActualHeight < 10) return;

        double measured = Math.Clamp(Level / 60d, 0, 1);
        bool highlight = Moment is GameMomentState.Anticipating or GameMomentState.Together or GameMomentState.SongHighlight;
        double energy = Math.Clamp((IsPerformanceActive ? .28 : .12) + measured * .72 + (highlight ? .18 : 0), .1, 1);
        double seconds = _reducedMotion ? 0 : Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        DrawGeneralDust(dc, seconds, energy);
        if (PlayerActive) DrawTraceMotes(dc, seconds, energy);
        if (HasAccompaniment) DrawHighlightCloud(dc, seconds, energy, highlight);
    }

    private void DrawGeneralDust(DrawingContext dc, double seconds, double energy)
    {
        for (int i = 0; i < 42; i++)
        {
            double seedX = Hash(i * 17 + 11);
            double seedY = Hash(i * 31 + 7);
            double depth = Hash(i * 47 + 3);
            double driftX = _reducedMotion ? 0
                : Math.Sin(seconds * (.06 + depth * .11) + i * 1.7) * (1.5 + depth * 6);
            double driftY = _reducedMotion ? 0
                : Math.Cos(seconds * (.045 + depth * .085) + i * .93) * (.7 + depth * 2.8);
            double breath = _reducedMotion ? 1
                : .86 + .14 * Math.Sin(seconds * (.12 + depth * .08) + i * .7);
            double x = ActualWidth * seedX + driftX;
            double y = ActualHeight * (.08 + seedY * .78) + driftY;
            double size = .55 + depth * 2.8;
            byte alpha = (byte)Math.Round((8 + depth * 24) * (.45 + energy * .55) * breath);
            var color = i % 7 == 0 ? Color.FromArgb(alpha, 226, 202, 129) : Color.FromArgb(alpha, 191, 216, 192);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            if (i % 3 == 0)
                dc.DrawRectangle(brush, null, new Rect(x, y, size, size));
            else
                dc.DrawEllipse(brush, null, new Point(x, y), size * .55, size * .55);
        }
    }

    private void DrawTraceMotes(DrawingContext dc, double seconds, double energy)
    {
        var approachStart = new Point(ActualWidth * .1, ActualHeight * .79);
        var approachC1 = new Point(ActualWidth * .24, ActualHeight * .79);
        var approachC2 = new Point(ActualWidth * .38, ActualHeight * .76);
        var mergeLeft = new Point(ActualWidth * .435, ActualHeight * .73);
        var mergeRight = new Point(ActualWidth * .58, ActualHeight * .73);
        var exitC1 = new Point(ActualWidth * .7, ActualHeight * .67);
        var exitC2 = new Point(ActualWidth * .82, ActualHeight * .44);
        var destination = new Point(ActualWidth * .86, ActualHeight * .255);

        DrawSegmentMotes(dc, seconds, energy, approachStart, approachC1, approachC2, mergeLeft,
            12, 17, 0, .44);
        if (HasAccompaniment)
            DrawSegmentMotes(dc, seconds, energy, mergeRight, exitC1, exitC2, destination,
                18, 79, .56, 1);
    }

    private void DrawSegmentMotes(DrawingContext dc, double seconds, double energy,
        Point start, Point c1, Point c2, Point end, int count, int seedBase, double progressStart, double progressEnd)
    {
        for (int i = 0; i < count; i++)
        {
            double seed = Hash(i * 59 + seedBase);
            double phase = _reducedMotion ? 0 : (seconds * (.018 + .05 * energy) + seed) % 1;
            double t = (i / (double)count + phase) % 1;
            var point = Cubic(start, c1, c2, end, t);
            var tangent = CubicTangent(start, c1, c2, end, t);
            var normal = new Vector(-tangent.Y, tangent.X);
            if (normal.LengthSquared > .001) normal.Normalize();
            double progress = progressStart + (progressEnd - progressStart) * t;
            double offset = (Hash(i * 73 + seedBase + 5) - .5) * (8 + 28 * progress);
            point += normal * offset;
            double size = .75 + progress * 3.2 + energy;
            byte alpha = (byte)Math.Round((20 + progress * 86) * energy);
            var color = progress > .72
                ? Color.FromArgb(alpha, 242, 190, 97)
                : Color.FromArgb(alpha, 215, 226, 150);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            dc.DrawRectangle(brush, null, new Rect(point.X - size / 2, point.Y - size / 2, size, size));
        }
    }

    private void DrawHighlightCloud(DrawingContext dc, double seconds, double energy, bool highlight)
    {
        var center = new Point(ActualWidth * .86, ActualHeight * .27);
        double strength = highlight ? energy : energy * .3;
        double motionSeconds = IsPerformanceActive ? seconds : 0;
        for (int i = 0; i < 18; i++)
        {
            double angle = Hash(i * 97 + 13) * Math.PI * 2 + (_reducedMotion ? 0 : motionSeconds * .035);
            double radius = 8 + Hash(i * 43 + 29) * 76;
            double x = center.X + Math.Cos(angle) * radius * 1.45;
            double y = center.Y + Math.Sin(angle) * radius * .52;
            double size = .8 + Hash(i * 37 + 17) * 3.8;
            byte alpha = (byte)Math.Round((12 + Hash(i * 19 + 31) * 84) * strength);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, 244, 173, 78));
            brush.Freeze();
            dc.DrawRectangle(brush, null, new Rect(x - size / 2, y - size / 2, size, size));
        }
    }

    private static double Hash(int value)
    {
        uint x = (uint)value;
        x ^= x >> 16;
        x *= 0x7feb352d;
        x ^= x >> 15;
        x *= 0x846ca68b;
        x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215d;
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

    private static Vector CubicTangent(Point a, Point b, Point c, Point d, double t)
    {
        double u = 1 - t;
        return new Vector(
            3 * u * u * (b.X - a.X) + 6 * u * t * (c.X - b.X) + 3 * t * t * (d.X - c.X),
            3 * u * u * (b.Y - a.Y) + 6 * u * t * (c.Y - b.Y) + 3 * t * t * (d.Y - c.Y));
    }
}
