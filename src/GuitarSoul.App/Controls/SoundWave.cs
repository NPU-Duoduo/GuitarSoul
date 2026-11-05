using System.Windows;
using System.Windows.Media;

namespace GuitarSoul.App.Controls;

/// <summary>Recent measured amplitude envelope, without a synthetic oscillator.</summary>
public sealed class SoundWave : FrameworkElement
{
    private readonly double[] _levels = new double[120];
    private int _index;
    private int _bucketSamples;
    private double _bucketPeak;
    private static readonly Brush WaveInk = new SolidColorBrush(Color.FromRgb(157, 185, 138));
    private static readonly Brush BaselineInk = new SolidColorBrush(Color.FromRgb(221, 220, 210));
    private static readonly Brush PixelInk = new SolidColorBrush(Color.FromRgb(146, 178, 132));
    private static readonly Brush PixelDim = new SolidColorBrush(Color.FromRgb(78, 102, 74));
    public bool ReducedMotion { get; set; }
    /// <summary>Draws whole pixel blocks on the same grid as the room, instead of a fine envelope.</summary>
    public bool PixelStyle { get; set; }
    /// <summary>Fraction of the width kept empty in the middle, so a character can sit between two bands.</summary>
    public double CenterGap { get; set; }
    /// <summary>Device-independent size of one pixel block in <see cref="PixelStyle"/> mode.</summary>
    public double PixelUnit { get; set; } = 4;
    /// <summary>Width of each pixel band in device units; 0 fills whatever the gap leaves.</summary>
    public double BandWidth { get; set; }
    internal bool HasMeasuredSignal => _levels.Any(level => level > 0);
    public void Push(ReadOnlySpan<float> samples, int sampleRate = 8000)
    {
        if (samples.Length == 0) return;
        // 10 ms sample-time buckets preserve scrolling speed across driver callbacks.
        int bucketSize = Math.Max(1, sampleRate / 100);
        foreach (var sample in samples)
        {
            if (float.IsFinite(sample)) _bucketPeak = Math.Max(_bucketPeak, Math.Abs(sample));
            if (++_bucketSamples < bucketSize) continue;
            _levels[_index] = Math.Clamp(Math.Pow(_bucketPeak, .65), 0, 1);
            _index = (_index + 1) % _levels.Length;
            _bucketSamples = 0; _bucketPeak = 0;
        }
        InvalidateVisual();
    }
    public void Clear()
    {
        Array.Clear(_levels);
        _index = 0; _bucketSamples = 0; _bucketPeak = 0;
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        if (PixelStyle) { RenderPixelBands(dc); return; }
        double center = ActualHeight / 2, height = ActualHeight * .42;
        double average = ReducedMotion ? _levels.Average() : 0;
        var pen = new Pen(WaveInk, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        // Each fine stroke retains one sample-time bucket. Quiet input stays quiet;
        // the edge fade changes presentation only, without an animated idle signal.
        for (int i = 0; i < _levels.Length; i++)
        {
            double x = 1 + (ActualWidth - 2) * i / (_levels.Length - 1);
            double envelope = ReducedMotion ? average :
                (_levels[(_index + i) % 120] * 2 + _levels[(_index + Math.Max(0, i - 1)) % 120] +
                    _levels[(_index + Math.Min(119, i + 1)) % 120]) / 4;
            double edge = Math.Sin(Math.PI * i / 119);
            double extent = envelope * height * edge;
            dc.PushOpacity(.25 + .75 * edge);
            if (extent < .3)
                dc.DrawEllipse(BaselineInk, null, new Point(x, center), .7, .7);
            else
                dc.DrawLine(pen, new Point(x, center - extent), new Point(x, center + extent));
            dc.Pop();
        }
    }

    /// <summary>
    /// Two symmetric bands of whole pixel blocks. The gap in the middle is where the
    /// character stands, so the wave never draws over it.
    /// </summary>
    private void RenderPixelBands(DrawingContext dc)
    {
        double unit = Math.Max(2, PixelUnit);
        double center = Math.Round(ActualHeight / 2);
        double gap = Math.Clamp(CenterGap, 0, .9);
        double bandWidth = ActualWidth * (1 - gap) / 2;
        // A bounded band keeps the wave a compact cluster around the character instead of
        // a dotted rule running the full width of the scene.
        if (BandWidth > unit * 2) bandWidth = Math.Min(bandWidth, BandWidth);
        int bars = Math.Max(1, (int)Math.Floor(bandWidth / (unit * 2)));
        // Both bands grow outward from the gap, so the two sides mirror each other.
        double gapLeft = Math.Round(ActualWidth * (1 - gap) / 2);
        double gapRight = Math.Round(ActualWidth * (1 + gap) / 2);
        double maxHalf = Math.Floor(ActualHeight * .42 / unit) * unit;
        double average = ReducedMotion ? _levels.Average() : 0;

        for (int side = 0; side < 2; side++)
        {
            for (int i = 0; i < bars; i++)
            {
                // The band is read from the outside in, so the newest sound is nearest the cat.
                int sample = (int)Math.Round(i * 119.0 / Math.Max(1, bars - 1));
                double envelope = ReducedMotion ? average :
                    (_levels[(_index + sample) % 120] * 2 + _levels[(_index + Math.Max(0, sample - 1)) % 120] +
                        _levels[(_index + Math.Min(119, sample + 1)) % 120]) / 4;
                double edge = .35 + .65 * Math.Sin(Math.PI * (i + 1) / (bars + 1));
                double half = Math.Round(envelope * maxHalf * edge / unit) * unit;
                double x = side == 0
                    ? gapLeft - (i + 1) * unit * 2 + unit
                    : gapRight + i * unit * 2;
                dc.PushOpacity(.28 + .72 * edge);
                if (half < unit)
                {
                    dc.DrawRectangle(PixelDim, null, new Rect(x, center, unit, unit));
                }
                else
                {
                    dc.DrawRectangle(PixelInk, null, new Rect(x, center - half, unit, half));
                    dc.DrawRectangle(PixelInk, null, new Rect(x, center, unit, half));
                }
                dc.Pop();
            }
        }
    }
}
