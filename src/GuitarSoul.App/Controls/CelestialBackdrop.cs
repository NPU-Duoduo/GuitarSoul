using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;

namespace GuitarSoul.App.Controls;

/// <summary>
/// A quiet local-time ambience layer. It deliberately uses no location services:
/// the arc communicates time of day, while the lunar phase is an astronomical approximation.
/// </summary>
public sealed class CelestialBackdrop : FrameworkElement
{
    private static readonly DateTimeOffset KnownNewMoon =
        new(2000, 1, 6, 18, 14, 0, TimeSpan.Zero);
    private const double SynodicMonthDays = 29.53058867;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset? _previewLocalTime;
    private double? _previewMoonPhase;

    public DateTimeOffset? PreviewLocalTime
    {
        get => _previewLocalTime;
        set { _previewLocalTime = value; InvalidateVisual(); }
    }

    public double? PreviewMoonPhase
    {
        get => _previewMoonPhase;
        set { _previewMoonPhase = value; InvalidateVisual(); }
    }

    public string Description
    {
        get
        {
            var local = CurrentLocalTime;
            var hour = local.TimeOfDay.TotalHours;
            return hour is >= 6 and < 18
                ? $"本地时间 {local:HH:mm} · {DayPeriod(hour)}"
                : $"本地时间 {local:HH:mm} · {MoonPhaseName(CurrentMoonPhase)}";
        }
    }

    private DateTimeOffset CurrentLocalTime => PreviewLocalTime ?? DateTimeOffset.Now;
    private double CurrentMoonPhase => PreviewMoonPhase is double preview
        ? Normalize(preview)
        : MoonPhase(CurrentLocalTime.ToUniversalTime());

    public CelestialBackdrop()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        UseLayoutRounding = true;
        AutomationProperties.SetName(this, "按本地时间变化的日月声场；月相为近似值，不读取定位");
        _timer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background,
            (_, _) => InvalidateVisual(), Dispatcher);
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 1 || ActualHeight < 1) return;

        var local = CurrentLocalTime;
        double hour = local.TimeOfDay.TotalHours;
        bool day = hour is >= 6 and < 18;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(SkyBrush(hour), null, bounds);

        if (!day) DrawStars(dc);

        double progress = day ? (hour - 6) / 12 : hour >= 18 ? (hour - 18) / 12 : (hour + 6) / 12;
        progress = Math.Clamp(progress, 0, 1);
        var anchor = CelestialAnchor(ActualWidth, ActualHeight, progress);
        double x = anchor.X;
        double y = anchor.Y;
        if (day) DrawSun(dc, x, y, hour);
        else DrawMoon(dc, x, y, CurrentMoonPhase);

        DrawReflection(dc, x, day);
    }

    /// <summary>
    /// Keeps the local-time arc inside the left sky corridor so the orb and its halo
    /// never travel behind the central headline or the two right-hand status blocks.
    /// </summary>
    internal static Point CelestialAnchor(double width, double height, double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        return new Point(
            width * (.09 + .21 * progress),
            height * (.29 - .16 * Math.Sin(Math.PI * progress)));
    }

    private Brush SkyBrush(double hour)
    {
        Color top;
        Color horizon;
        if (hour is >= 6 and < 18)
        {
            double noon = 1 - Math.Abs(hour - 12) / 6;
            top = Lerp(Color.FromRgb(13, 42, 43), Color.FromRgb(31, 73, 72), noon);
            horizon = Lerp(Color.FromRgb(104, 70, 50), Color.FromRgb(62, 103, 92), noon);
        }
        else
        {
            double midnight = hour >= 18 ? Math.Min(1, (hour - 18) / 6) : Math.Min(1, (6 - hour) / 6);
            top = Lerp(Color.FromRgb(9, 31, 33), Color.FromRgb(4, 17, 23), midnight);
            horizon = Lerp(Color.FromRgb(35, 59, 54), Color.FromRgb(10, 37, 38), midnight);
        }
        var brush = new LinearGradientBrush(top, horizon, new Point(.5, 0), new Point(.5, 1));
        brush.Freeze();
        return brush;
    }

    private void DrawSun(DrawingContext dc, double x, double y, double hour)
    {
        double edge = Math.Min(Math.Abs(hour - 6), Math.Abs(18 - hour));
        var halo = Color.FromRgb(244, 154, 76);
        DrawGlow(dc, new Point(x, y), halo, edge < 1.5 ? 70 : 54, .12);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(235, 255, 218, 132)), null,
            new Point(x, y), 12, 12);
    }

    private void DrawMoon(DrawingContext dc, double x, double y, double phase)
    {
        var center = new Point(x, y);
        DrawGlow(dc, center, Color.FromRgb(210, 221, 190), 46, .08);
        const double radius = 13;
        dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(28, 49, 48)),
            new Pen(new SolidColorBrush(Color.FromArgb(110, 218, 224, 201)), 1), center, radius, radius);

        var lit = new Pen(new SolidColorBrush(Color.FromRgb(232, 229, 197)), 1.35);
        lit.Freeze();
        double c = Math.Cos(phase * Math.PI * 2);
        for (double dy = -radius + .7; dy <= radius - .7; dy += 1.1)
        {
            double edge = Math.Sqrt(Math.Max(0, radius * radius - dy * dy));
            double left;
            double right;
            if (phase <= .5)
            {
                left = c * edge;
                right = edge;
            }
            else
            {
                left = -edge;
                right = -c * edge;
            }
            if (right - left > .15)
                dc.DrawLine(lit, new Point(x + left, y + dy), new Point(x + right, y + dy));
        }
    }

    private void DrawStars(DrawingContext dc)
    {
        ReadOnlySpan<(double X, double Y, double Size)> stars =
        [
            (.08, .11, 1), (.15, .23, .7), (.23, .08, .8), (.31, .19, .6),
            (.38, .1, 1), (.47, .26, .7), (.55, .13, .6), (.63, .22, .9),
            (.71, .08, .7), (.79, .18, 1), (.87, .11, .6), (.93, .25, .8)
        ];
        var brush = new SolidColorBrush(Color.FromArgb(105, 225, 230, 205));
        brush.Freeze();
        foreach (var star in stars)
            dc.DrawEllipse(brush, null, new Point(ActualWidth * star.X, ActualHeight * star.Y), star.Size, star.Size);
    }

    private void DrawReflection(DrawingContext dc, double x, bool day)
    {
        double horizon = ActualHeight * .55;
        var color = day ? Color.FromRgb(245, 166, 86) : Color.FromRgb(201, 218, 191);
        for (int i = 0; i < 8; i++)
        {
            double y = horizon + 8 + i * 7;
            double width = (8 - i) * (day ? 5 : 3.5);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)Math.Max(5, 40 - i * 4), color.R, color.G, color.B)), 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(x - width, y), new Point(x + width, y));
        }
    }

    private static void DrawGlow(DrawingContext dc, Point center, Color color, double radius, double opacity)
    {
        for (int i = 3; i >= 1; i--)
        {
            double r = radius * i / 3;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * opacity / i), color.R, color.G, color.B));
            brush.Freeze();
            dc.DrawEllipse(brush, null, center, r, r);
        }
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }

    private static double MoonPhase(DateTimeOffset utc)
    {
        double days = (utc - KnownNewMoon).TotalDays;
        return Normalize(days / SynodicMonthDays);
    }

    private static double Normalize(double value) => value - Math.Floor(value);

    private static string DayPeriod(double hour) => hour < 8 ? "清晨"
        : hour < 16 ? "日光"
        : "黄昏";

    private static string MoonPhaseName(double phase) => phase switch
    {
        < .035 or >= .965 => "新月",
        < .215 => "娥眉月",
        < .285 => "上弦月",
        < .465 => "盈凸月",
        < .535 => "满月",
        < .715 => "亏凸月",
        < .785 => "下弦月",
        _ => "残月"
    };
}
