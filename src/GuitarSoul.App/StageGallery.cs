using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

/// <summary>Exports actual WPF sprites at fixed clocks; never captures or plays audio.</summary>
internal static class StageGallery
{
    private static readonly StageStyle[] Styles = Enum.GetValues<StageStyle>().Where(x => x != StageStyle.Everyday).ToArray();
    private static readonly string[] Names = ["Slash", "Jimi Hendrix", "清音", "Jimmy Page", "David Gilmour", "B.B. King", "Angus Young", "Kurt Cobain", "Nile Rodgers"];
    private static readonly Brush Paper = B("#F4F1EA"), Ink = B("#25342C"), Green = B("#253A31"), Light = B("#F5EDD9");

    internal static void Render(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Stage render check failed: " + name);
            checks.Add(name);
        }
        foreach (var style in Styles)
        foreach (var skin in Enum.GetValues<CatSkin>())
        {
            foreach (var mood in Enum.GetValues<CompanionMood>())
                Check(Pixels(Cat(style, skin, mood, false, .12, .8, .7)).SequenceEqual(
                    Pixels(Cat(StageStyle.Everyday, skin, mood, false, .12, .8, .7))),
                    $"Unarmed {style}/{skin}/{mood} retains the everyday pixels");
            foreach (int size in new[] { 168, 224 })
            {
                var stillA = Cat(style, skin, CompanionMood.Excited, true, .12, 1, 0, size);
                var stillB = Cat(style, skin, CompanionMood.Excited, true, 1.3, 0, 0, size);
                Check(Pixels(stillA).SequenceEqual(Pixels(stillB)), $"Zero motion stays still: {style}/{skin}/{size}");
                Check(Pixels(Cat(style, skin, CompanionMood.Excited, true, .12, 1, 1, size, true)).SequenceEqual(
                    Pixels(Cat(style, skin, CompanionMood.Excited, true, 1.3, 0, 1, size, true))),
                    $"Reduced motion stays still: {style}/{skin}/{size}");
                foreach (double pulse in new[] { 0d, .5, 1d })
                {
                    var bitmap = Cat(style, skin, CompanionMood.Excited, true, .12, pulse, 1, size);
                    var bytes = Pixels(bitmap);
                    bool edge = false;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        if ((x == 0 || y == 0 || x == size - 1 || y == size - 1) && bytes[(y * size + x) * 4 + 3] != 0)
                            edge = true;
                    Check(!edge, $"No sprite clipping: {style}/{skin}/{size}/{pulse}");
                }
            }
        }
        foreach (var style in Styles)
        foreach (int size in new[] { 168, 224 })
        {
            using var cat = new PixelCat { Stage = style, Skin = CatSkin.Calico, Guitar = GuitarStyle.Stratocaster,
                PreviewSeconds = .12, PreviewPulse = 1, MotionAmount = 1 };
            cat.SetPose(new CompanionPose(CompanionAction.Jam, CompanionMood.Excited, 1)
                { HasMusicalEvidence = true, Engagement = 1 });
            cat.Measure(new Size(size, size)); cat.Arrange(new Rect(0, 0, size, size)); cat.UpdateLayout();
            var high = Pixels(Bitmap(cat, size, size));
            Check(!Enumerable.Range(0, size).Any(x => high[x * 4 + 3] != 0),
                $"High-engagement head has top clearance: {style}/{size}");
            cat.SetPose(new CompanionPose(CompanionAction.Settle, CompanionMood.Listening, .5));
            Check(cat.IsJamming, $"Stage remains through settling: {style}/{size}");
            _ = Bitmap(cat, size, size);
            cat.ReducedMotion = true;
            cat.PreviewSeconds = .1; cat.PreviewPulse = 1; cat.InvalidateVisual();
            var quiet = Pixels(Bitmap(cat, size, size));
            cat.PreviewSeconds = 1.1; cat.PreviewPulse = 0; cat.InvalidateVisual();
            Check(quiet.SequenceEqual(Pixels(Bitmap(cat, size, size))), $"Reduced motion stops an in-flight stage: {style}/{size}");
            cat.SetPose(new CompanionPose(CompanionAction.Listen, CompanionMood.Listening, 1));
            var completed = Pixels(Bitmap(cat, size, size));
            cat.Stage = StageStyle.Everyday;
            Check(!cat.IsJamming && completed.SequenceEqual(Pixels(Bitmap(cat, size, size))),
                $"Settling returns to the ordinary listening cat: {style}/{size}");
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Paper, null, new Rect(0, 0, 960, 1174));
            Text(dc, "九次相遇，各有一把好琴", 28, 20, 26, Ink);
            Text(dc, "实际 WPF 像素绘制 · 原创装束候选 · 仅在「一起弹」出现", 28, 61, 14, Ink);
            for (int i = 0; i < Styles.Length; i++)
            {
                int x = 20 + i % 3 * 312, y = 100 + i / 3 * 344;
                dc.DrawRectangle(Green, null, new Rect(x, y, 296, 324));
                Text(dc, Names[i], x + 15, y + 12, 19, Light);
                dc.DrawImage(Cat(Styles[i], CatSkin.Calico, CompanionMood.Excited, true, .12, 1, .7), new Rect(x + 36, y + 40, 224, 224));
                for (int skin = 0; skin < 4; skin++)
                    dc.DrawImage(Cat(Styles[i], (CatSkin)skin, CompanionMood.Excited, true, .12, 1, .7, 56), new Rect(x + 12 + skin * 68, y + 264, 56, 56));
            }
            Text(dc, "原创像素装束 · 实际 WPF 绘制 · 解锁前保持剪影 · 仅在一起弹与收势出现", 28, 1144, 14, Ink);
        }
        SavePng(Bitmap(visual, 960, 1174), path);
        var encoder = new GifBitmapEncoder();
        for (int frame = 0; frame < 72; frame++)
        {
            double time = frame / 12d;
            double pulse = Math.Exp(-(time % .5) / .16);
            var motion = new DrawingVisual();
            using (var dc = motion.RenderOpen())
            {
                dc.DrawRectangle(Green, null, new Rect(0, 0, 744, 840));
                for (int i = 0; i < Styles.Length; i++)
                {
                    int x = i % 3 * 248, y = i / 3 * 272;
                    Text(dc, Names[i], x + 15, y + 14, 16, Light);
                    dc.DrawImage(Cat(Styles[i], CatSkin.Calico, CompanionMood.Excited, true, time, pulse, .7),
                        new Rect(x + 12, y + 42, 224, 224));
                }
                Text(dc, "无声模拟 · 同一节奏下的不同表现", 15, 818, 12, Light);
            }
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)(frame % 3 == 2 ? 9 : 8));
            if (frame == 0)
            {
                metadata.SetQuery("/appext/application", System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                metadata.SetQuery("/appext/data", new byte[] { 3, 1, 0, 0, 0 });
            }
            encoder.Frames.Add(BitmapFrame.Create(Bitmap(motion, 744, 840), null, metadata, null));
        }
        using (var stream = File.Create(Path.ChangeExtension(path, ".gif"))) encoder.Save(stream);
        File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(new { passed = checks.Count, checks,
            note = "Actual WPF render; fixed preview clocks, no audio or user data. Not an aesthetic acceptance." },
            new JsonSerializerOptions { WriteIndented = true }));
    }
    private static RenderTargetBitmap Cat(StageStyle stage, CatSkin skin, CompanionMood mood, bool jam, double time,
        double pulse, double amount, int size = 224, bool reduced = false)
    {
        using var cat = new PixelCat { Stage = stage, Skin = skin, Mood = mood, IsJamming = jam,
            Guitar = GuitarStyle.Stratocaster, PreviewSeconds = time, PreviewPulse = pulse,
            MotionAmount = amount, ReducedMotion = reduced };
        cat.Measure(new Size(size, size)); cat.Arrange(new Rect(0, 0, size, size)); cat.UpdateLayout();
        return Bitmap(cat, size, size);
    }
    private static RenderTargetBitmap Bitmap(Visual visual, int width, int height)
    {
        if (visual is UIElement element) element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Pixels(BitmapSource bitmap)
    {
        var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0); return bytes;
    }
    private static void SavePng(BitmapSource bitmap, string path)
    {
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); png.Save(file);
    }
    private static void Text(DrawingContext dc, string value, double x, double y, double size, Brush color) =>
        dc.DrawText(new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), size, color, 1), new Point(x, y));
    private static Brush B(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze(); return brush;
    }
}
