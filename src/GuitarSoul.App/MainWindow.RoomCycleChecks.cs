using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Scenery;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private static void RunRoomCycleChecks(string reportPath, Action<bool, string> check)
    {
        var midnight = new DateTimeOffset(2026, 10, 31, 0, 0, 0, TimeSpan.FromHours(8));
        var noon = RoomDayCycle.At(12);
        var night = RoomDayCycle.At(0);
        check(noon.Daylight > .95 && night.Daylight < .01 && noon.SunY < .2 && night.MoonY < .2,
            "Local-clock cycle puts daylight and high sun at noon, high moon at midnight");
        bool continuous = true;
        foreach (double hour in new[] {0d, 4.5, 5.7, 6, 7.2, 12, 15.5, 17.5, 18, 19, 20.5, 24})
        {
            var a = RoomDayCycle.At(hour - 1d / 3600);
            var b = RoomDayCycle.At(hour + 1d / 3600);
            continuous &= Math.Abs(a.Daylight - b.Daylight) < .001 && Math.Abs(a.SunX - b.SunX) < .001 &&
                Math.Abs(a.MoonY - b.MoonY) < .001 && Math.Abs(a.Warmth - b.Warmth) < .001;
        }
        check(continuous, "Lighting and celestial trajectories remain continuous across dawn, dusk and midnight");
        var clock = new RoomCycleClock();
        clock.Advance(midnight, 0);
        check(Math.Abs((clock.Advance(midnight.AddSeconds(1), 1) - midnight.AddSeconds(1)).TotalMilliseconds) < 1,
            "Scene clock follows ordinary wall-clock progression");
        var resume = clock.Advance(midnight.AddHours(8), 8 * 3600);
        check((resume - midnight).TotalSeconds < 2, "Resuming after sleep keeps the previous first frame instead of jumping eight hours");
        DateTimeOffset after = resume;
        for (int i = 1; i <= 2000; i++) after = clock.Advance(midnight.AddHours(8).AddSeconds(i * .04), 8 * 3600 + i * .04);
        check(Math.Abs((midnight.AddHours(8).AddSeconds(80) - after).TotalSeconds) < 2,
            "Clock correction converges smoothly to local wall time after resume");
        var midnightClock = new RoomCycleClock();
        var beforeMidnight = midnight.AddDays(1).AddMilliseconds(-500);
        midnightClock.Advance(beforeMidnight, 0);
        check(midnightClock.Advance(beforeMidnight.AddSeconds(1), 1).Date == midnight.AddDays(1).Date,
            "Midnight advances the calendar without resetting the scene clock");

        var room = new RehearsalRoomBackdrop { ReducedMotion = true, PreviewMoonPhase = .36 };
        room.Measure(new Size(700, 800)); room.Arrange(new Rect(0, 0, 700, 800));
        byte[] Render(double hour)
        {
            room.PreviewLocalTime = midnight.AddHours(hour);
            room.UpdateLayout();
            var target = new RenderTargetBitmap(700, 800, 96, 96, PixelFormats.Pbgra32);
            target.Render(room);
            byte[] pixels = new byte[700 * 800 * 4]; target.CopyPixels(pixels, 700 * 4, 0);
            return pixels;
        }
        double Difference(byte[] a, byte[] b)
        {
            long sum = 0;
            for (int i = 0; i < a.Length; i++) if (i % 4 != 3) sum += Math.Abs(a[i] - b[i]);
            return sum / (700d * 800 * 3);
        }
        var boundaryDifferences = new List<object>();
        foreach (double boundary in new[] {0d, 5.7, 6, 7.2, 12, 17.5, 18, 19, 24})
        {
            double difference = Difference(Render(boundary - 1d / 3600), Render(boundary + 1d / 3600));
            boundaryDifferences.Add(new {hour = boundary, meanRgbDifferenceAcrossTwoSeconds = difference});
            check(difference < .4, $"Actual room pixels cross {boundary:0.0}h continuously (two-second mean RGB delta {difference:0.0000})");
        }
        var nightPixels = Render(0); var dayPixels = Render(12);
        check(Enumerable.Range(0, 700 * 800).All(i => nightPixels[i * 4 + 3] == 255),
            "Standalone scene has an opaque substrate, preventing transparent-export overexposure");
        double Luminance(byte[] pixels, int x0, int y0, int x1, int y1)
        {
            double sum = 0;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
            {
                int p = (y * 700 + x) * 4;
                sum += .0722 * pixels[p] + .7152 * pixels[p + 1] + .2126 * pixels[p + 2];
            }
            return sum / ((x1 - x0) * (y1 - y0));
        }
        check(Luminance(dayPixels, 450, 50, 560, 175) > Luminance(nightPixels, 450, 50, 560, 175) + 15,
            "Actual transparent window admits a substantially brighter daytime sky");
        check(Luminance(dayPixels, 340, 520, 530, 620) > Luminance(nightPixels, 340, 520, 530, 620) + 5,
            "Rug receives daylight independently of decorative motion");
        check(Difference(nightPixels, dayPixels) > 3, "Reduced motion preserves time-driven lighting while freezing decorative movement");

        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "cycle");
        Directory.CreateDirectory(folder);
        void Save(BitmapSource image, string file)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var output = File.Create(Path.Combine(folder, file)); encoder.Save(output);
        }
        foreach (var (hour, name) in new[] {(0d, "midnight"), (5.7, "dawn"), (8d, "morning"), (12d, "day"), (17.5, "sunset"), (19d, "blue-hour"), (21d, "night")})
            Save(BitmapSource.Create(700, 800, 96, 96, PixelFormats.Pbgra32, null, Render(hour), 700 * 4), name + ".png");
        // Reduced-size deterministic frames support a local 24-hour scrubber without audio capture.
        room.Measure(new Size(350, 400)); room.Arrange(new Rect(0, 0, 350, 400));
        var stopwatch = Stopwatch.StartNew();
        var lightCosts = new List<double>();
        for (int frame = 0; frame < 96; frame++)
        {
            room.PreviewLocalTime = midnight.AddMinutes(frame * 15);
            room.UpdateLayout();
            var bitmap = new RenderTargetBitmap(350, 400, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(room); Save(bitmap, $"frame-{frame:000}.png");
            lightCosts.Add(room.LastLightRenderMilliseconds);
        }
        File.WriteAllText(Path.Combine(folder, "validation.json"), JsonSerializer.Serialize(new
        {
            boundaries = boundaryDifferences, nativeFrames = 96, intervalMinutes = 15,
            renderAndEncodeSeconds = stopwatch.Elapsed.TotalSeconds,
            lightUpdateMillisecondsMedian = lightCosts.OrderBy(x => x).ElementAt(48),
            lightUpdateMillisecondsP95 = lightCosts.OrderBy(x => x).ElementAt(91),
            note = "Rendered from the production WPF control; offline accelerated review, no audio/device capture."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
