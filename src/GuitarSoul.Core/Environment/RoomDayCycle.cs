namespace GuitarSoul.Core.Scenery;

/// <summary>A deliberately authored local-clock day, not a geographic ephemeris.</summary>
public readonly record struct RoomDayCycle(double Hour, double SunHeight, double Daylight,
    double Warmth, double SunVisibility, double MoonVisibility, double SunX, double SunY,
    double MoonX, double MoonY)
{
    public static RoomDayCycle At(double hour)
    {
        hour = double.IsFinite(hour) ? (hour % 24 + 24) % 24 : 0;
        double angle = (hour - 6) * Math.PI / 12;
        double height = Math.Sin(angle);
        double daylight = Smooth(-.19, .42, height);
        double warm = Math.Exp(-Math.Pow((height - .05) / .25, 2)) * Smooth(-.22, .02, height);
        return new(hour, height, daylight, warm,
            Smooth(-.025, .10, height), Smooth(-.025, .10, -height),
            .5 - .52 * Math.Cos(angle), .96 - .87 * height,
            .5 + .52 * Math.Cos(angle), .96 + .87 * height);
    }

    public static double Smooth(double from, double to, double value)
    {
        double t = Math.Clamp((value - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }
}

/// <summary>Follows wall time normally; eases clock corrections/resume without a visible jump.</summary>
public sealed class RoomCycleClock
{
    private DateTime? _display;
    private double _lastMonotonic;

    public DateTimeOffset Advance(DateTimeOffset wallTime, double monotonicSeconds)
    {
        if (_display is null)
        {
            _display = wallTime.DateTime;
            _lastMonotonic = monotonicSeconds;
        }
        else
        {
            double elapsed = Math.Max(0, monotonicSeconds - _lastMonotonic);
            _lastMonotonic = monotonicSeconds;
            // A hidden window or sleeping computer resumes from its last drawn state.
            double step = elapsed > 2 ? 0 : elapsed;
            var next = _display.Value.AddSeconds(step);
            double error = (wallTime.DateTime - next).TotalSeconds;
            _display = next.AddSeconds(error * (1 - Math.Exp(-step / 8)));
        }
        return new DateTimeOffset(DateTime.SpecifyKind(_display.Value, DateTimeKind.Unspecified), wallTime.Offset);
    }
}
