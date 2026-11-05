using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.Core.Scenery;

namespace GuitarSoul.App.Controls;

public sealed partial class RehearsalRoomBackdrop
{
    // All response plates are transforms of ONE bitmap: there is no geometry crossfade.
    private static readonly Lazy<BitmapSource> AmbientPlate = new(() => LightResponse(1.95, 2.20, 2.35, true));
    private static readonly Lazy<BitmapSource> WarmPlate = new(() => LightResponse(2.05, 1.70, 1.25, true));
    private static readonly Lazy<BitmapSource> SunlitPlate = new(() => LightResponse(3.25, 2.65, 1.85, false));
    private static readonly Lazy<BitmapSource> MoonlitPlate = new(() => LightResponse(1.45, 1.80, 2.55, false));
    private WriteableBitmap _litRoom = null!;
    private byte[] _basePixels = [], _ambientPixels = [], _warmPixels = [], _sunPixels = [], _moonPixels = [], _litPixels = [];
    internal double LastLightRenderMilliseconds { get; private set; }
    private double _lastLightHour = double.NaN;

    private ImageBrush CreateRoomLightTexture()
    {
        byte[] Pixels(BitmapSource bitmap)
        {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            converted.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        _basePixels = Pixels(ReferenceRoom);
        _ambientPixels = Pixels(AmbientPlate.Value); _warmPixels = Pixels(WarmPlate.Value);
        _sunPixels = Pixels(SunlitPlate.Value); _moonPixels = Pixels(MoonlitPlate.Value);
        _litPixels = new byte[_basePixels.Length];
        _litRoom = new WriteableBitmap(ReferenceRoom.PixelWidth, ReferenceRoom.PixelHeight, 96, 96, PixelFormats.Pbgra32, null);
        RenderLightTexture(RoomDayCycle.At(CurrentLocalTime.TimeOfDay.TotalHours));
        return new ImageBrush(_litRoom) { Stretch = Stretch.Fill };
    }

    private static BitmapSource LightResponse(double redGain, double greenGain, double blueGain, bool spatial)
    {
        // Tone changes preserve alpha, edge positions and texture. Work only once, not per frame.
        var source = new FormatConvertedBitmap(ReferenceRoom, PixelFormats.Bgra32, null, 0);
        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int p = y * stride + x * 4;
            double u = x / (double)width, v = y / (double)height;
            double influence = spatial ? .35 + .65 * Math.Exp(-1.8 * (Math.Pow(u - .72, 2) + Math.Pow(v - .28, 2))) : 1;
            // Keep the electric lamp's warm local pool separate from window light.
            if (spatial) influence *= 1 - .74 * Math.Exp(-30 * Math.Pow(u - .20, 2) - 65 * Math.Pow(v - .25, 2));
            // Near leaves and pedals retain their dark, defocused depth frame.
            if (spatial) influence *= 1 - .65 * RoomDayCycle.Smooth(.74, 1, v);
            pixels[p] = Lift(pixels[p], 1 + (blueGain - 1) * influence);
            pixels[p + 1] = Lift(pixels[p + 1], 1 + (greenGain - 1) * influence);
            pixels[p + 2] = Lift(pixels[p + 2], 1 + (redGain - 1) * influence);
        }
        var result = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    private static byte Lift(byte channel, double gain) =>
        (byte)Math.Clamp(Math.Round(255 * (1 - Math.Pow(1 - channel / 255d, gain))), 0, 255);

    private void UpdateDayCycle()
    {
        var state = RoomDayCycle.At(CurrentLocalTime.TimeOfDay.TotalHours);
        if (double.IsNaN(_lastLightHour) || Math.Abs(state.Hour - _lastLightHour) > 1d / 3600)
        {
            _lastLightHour = state.Hour;
            RenderLightTexture(state);
        }
        using var background = _windowSky.RenderOpen();
        background.DrawRectangle(Rgb(5, 10, 8), null, new Rect(0, 0, ActualWidth, ActualHeight));
    }

    private void RenderLightTexture(RoomDayCycle state)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        byte[] sky = RenderSkyPixels(state);
        const int precision = 65536;
        int ambient = (int)(state.Daylight * .85 * precision), warm = (int)(state.Warmth * .56 * precision);
        double sun = state.SunVisibility * (.48 + .25 * state.Warmth);
        double moon = state.MoonVisibility * .16 * (1 - Math.Cos(CurrentMoonPhase * Math.Tau)) / 2;
        int width = ReferenceRoom.PixelWidth, height = ReferenceRoom.PixelHeight;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int p = (y * width + x) * 4;
            int sunlight = sun > .001 ? (int)(sun * FloorLight(x, y, state.SunX, Math.Max(0, state.SunHeight)) * precision) : 0;
            int moonlight = moon > .001 ? (int)(moon * FloorLight(x, y, state.MoonX, Math.Max(0, -state.SunHeight)) * precision) : 0;
            // Generated cutout can contain near-opaque alpha noise on room surfaces.
            // Transparency belongs exclusively to the glass opening and its tree edges.
            int alpha = x is >= 720 and <= 943 && y is <= 390 ? _basePixels[p + 3] : 255;
            for (int channel = 0; channel < 3; channel++)
            {
                int value = _basePixels[p + channel];
                value += ((_ambientPixels[p + channel] - value) * ambient) / precision;
                value += ((_warmPixels[p + channel] - value) * warm) / precision;
                value += ((_sunPixels[p + channel] - value) * sunlight) / precision;
                value += ((_moonPixels[p + channel] - value) * moonlight) / precision;
                int behind = alpha < 255 ? sky[(y * 255 + x - 705) * 4 + channel] : 0;
                _litPixels[p + channel] = (byte)((value * alpha + behind * (255 - alpha)) / 255);
            }
            _litPixels[p + 3] = 255;
        }
        _litRoom.WritePixels(new Int32Rect(0, 0, width, height), _litPixels, width * 4, 0);
        LastLightRenderMilliseconds = timer.Elapsed.TotalMilliseconds;
    }

    private static double FloorLight(double x, double y, double travel, double elevation)
    {
        // Texture-preserving projection only on clear rug, below amp/case and behind pedals.
        if (y < 849 || y > 1121 || x < 205 || x > 1035) return 0;
        double v = (y - 849) / (122 + 150 * (1 - elevation));
        if (v <= 0 || v >= 1) return 0;
        double shift = (travel - .5) * 105;
        double left = 690 + shift + (355 + shift * 1.7 - 690 - shift) * v;
        double right = 916 + shift + (772 + shift * 1.7 - 916 - shift) * v;
        double u = (x - left) / (right - left);
        double edges = RoomDayCycle.Smooth(0, .08, u) * (1 - RoomDayCycle.Smooth(.92, 1, u)) *
            RoomDayCycle.Smooth(0, .08, v) * (1 - RoomDayCycle.Smooth(.92, 1, v));
        double crossbar = 1 - RoomDayCycle.Smooth(.43, .49, v) * (1 - RoomDayCycle.Smooth(.51, .57, v));
        return edges * crossbar;
    }

    private byte[] RenderSkyPixels(RoomDayCycle state)
    {
        // Composite sky through the cutout BEFORE giving it to WPF's emissive material.
        // Emissive 3D blending otherwise adds sky to opaque branches and the wooden frame.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
        dc.PushTransform(new TranslateTransform(-705, 0));
        // A single fixed sky plane; the alpha plate in front supplies the actual organic contours.
        var window = new Rect(705, 0, 255, 410);
        dc.PushClip(new RectangleGeometry(window));
        Color top = SkyColor(state.Hour, false), bottom = SkyColor(state.Hour, true);
        dc.DrawRectangle(new LinearGradientBrush(top, bottom, 90), null, window);
        double stars = Math.Pow(1 - state.Daylight, 3) * .65;
        for (int i = 0; i < 22; i++)
        {
            double x = 718 + SurfaceHash(i, 59) % 229, y = 14 + SurfaceHash(i, 71) % 320;
            dc.DrawEllipse(Rgb(194, 210, 224, (byte)(stars * (85 + i % 4 * 35))), null, new Point(x, y), .65, .65);
        }
        Point sun = new(window.Left + state.SunX * window.Width, 70 + state.SunY * 320);
        dc.PushOpacity(state.SunVisibility);
        var halo = new RadialGradientBrush(Color.FromArgb(45, 255, 203, 133), Colors.Transparent);
        dc.DrawEllipse(halo, null, sun, 37, 37);
        dc.DrawEllipse(Rgb(255, 227, 169), null, sun, 8, 8);
        dc.DrawEllipse(Rgb(255, 241, 199), null, sun, 6.2, 6.2);
        dc.Pop();
        Point moon = new(window.Left + state.MoonX * window.Width, 70 + state.MoonY * 320);
        dc.PushOpacity(state.MoonVisibility);
        DrawWindowMoon(dc, moon, CurrentMoonPhase);
        dc.Pop();
        dc.Pop(); dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(255, 410, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[255 * 410 * 4];
        bitmap.CopyPixels(pixels, 255 * 4, 0);
        return pixels;
    }

    private static void DrawWindowMoon(DrawingContext dc, Point center, double phase)
    {
        const double radius = 13;
        dc.DrawEllipse(Rgb(30, 43, 57), null, center, radius, radius);
        double terminator = Math.Cos(phase * Math.Tau);
        var lit = new DrawingGroup();
        using (var moon = lit.Open())
        {
            for (int y = -12; y <= 12; y++)
            {
                double extent = Math.Sqrt(radius * radius - y * y);
                double left = phase <= .5 ? extent * terminator : -extent;
                double right = phase <= .5 ? extent : -extent * terminator;
                if (right > left) moon.DrawRectangle(Rgb(191, 208, 216), null,
                    new Rect(center.X + left, center.Y + y, right - left, 1));
            }
        }
        dc.DrawDrawing(lit);
    }

    private static Color SkyColor(double hour, bool horizon)
    {
        (double Hour, Color Top, Color Bottom)[] stops =
        [
            (0, Color.FromRgb(9, 22, 39), Color.FromRgb(23, 42, 57)),
            (4.5, Color.FromRgb(12, 25, 45), Color.FromRgb(31, 45, 64)),
            (5.7, Color.FromRgb(65, 73, 101), Color.FromRgb(176, 132, 121)),
            (7.2, Color.FromRgb(133, 168, 183), Color.FromRgb(224, 204, 169)),
            (12, Color.FromRgb(137, 181, 204), Color.FromRgb(199, 215, 208)),
            (15.5, Color.FromRgb(141, 173, 185), Color.FromRgb(210, 199, 161)),
            (17.5, Color.FromRgb(126, 102, 111), Color.FromRgb(229, 152, 93)),
            (19, Color.FromRgb(33, 45, 72), Color.FromRgb(105, 87, 102)),
            (20.5, Color.FromRgb(9, 22, 39), Color.FromRgb(23, 42, 57)),
            (24, Color.FromRgb(9, 22, 39), Color.FromRgb(23, 42, 57))
        ];
        for (int i = 1; i < stops.Length; i++)
        {
            if (hour > stops[i].Hour) continue;
            var a = stops[i - 1]; var b = stops[i];
            double t = RoomDayCycle.Smooth(a.Hour, b.Hour, hour);
            Color ca = horizon ? a.Bottom : a.Top, cb = horizon ? b.Bottom : b.Top;
            // scRGB retains fractional channel values: no 8-bit colour stepping in the live brush.
            return Color.FromScRgb(1, (float)(ca.ScR + (cb.ScR - ca.ScR) * t),
                (float)(ca.ScG + (cb.ScG - ca.ScG) * t), (float)(ca.ScB + (cb.ScB - ca.ScB) * t));
        }
        return stops[0].Top;
    }
}
