using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.App;

internal static class CatGallery
{
    private static readonly string[] Skins = ["白猫", "三花", "橘猫", "狸花"];
    private static readonly string[] States = ["等待 · 呼吸眨眼", "倾听 · 抬耳点头", "兴奋 · 踏爪摇摆", "休息 · 蜷睡"];

    public static void Render(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Beige, null, new Rect(0, 0, 1024, 1080));
            Label(dc, "Guitar Soul / 四位练琴伙伴", 30, 22, 26, Brushes.DarkSlateGray);
            for (int row = 0; row < 4; row++)
            for (int skin = 0; skin < 4; skin++)
            {
                int x = skin * 256, y = 76 + row * 248;
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(37, 58, 49)), null, new Rect(x + 8, y, 240, 238));
                dc.DrawImage(Cat((CatSkin)skin, (CompanionMood)row, .5, false), new Rect(x + 16, y + 2, 224, 210));
                Label(dc, Skins[skin] + " / " + States[row], x + 20, y + 205, 14, Brushes.Beige);
            }
        }
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(RenderVisual(sheet, 1024, 1080)));
        using (var stream = File.Create(fullPath)) png.Save(stream);

        var gif = new GifBitmapEncoder();
        for (int frame = 0; frame < 96; frame++)
        {
            double time = frame / 12d;
            var mood = (CompanionMood)Math.Min(3, (int)(time / 2));
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(37, 58, 49)), null, new Rect(0, 0, 896, 260));
                for (int skin = 0; skin < 4; skin++)
                {
                    dc.DrawImage(Cat((CatSkin)skin, mood, time, false), new Rect(skin * 224, 0, 224, 224));
                    Label(dc, Skins[skin] + " · " + States[(int)mood], skin * 224 + 15, 232, 13, Brushes.Beige);
                }
            }
            gif.Frames.Add(BitmapFrame.Create(RenderVisual(visual, 896, 260)));
        }
        SaveTimedAnimation(gif, Path.ChangeExtension(fullPath, ".gif"));
        // Reduced motion must preserve appearance but stop cyclic displacement.
        foreach (var skin in Enum.GetValues<CatSkin>())
        foreach (var mood in Enum.GetValues<CompanionMood>())
        {
            var first = Cat(skin, mood, 0, true);
            var second = Cat(skin, mood, .7, true);
            var a = new byte[224 * 224 * 4]; var b = new byte[a.Length];
            first.CopyPixels(a, 224 * 4, 0); second.CopyPixels(b, 224 * 4, 0);
            if (!a.AsSpan().SequenceEqual(b)) throw new InvalidOperationException("Reduced motion moved: " + skin + mood);
        }
        File.WriteAllText(Path.ChangeExtension(fullPath, ".txt"), "Rendered 4 skins × 4 states; reduced-motion equality checks: 16/16. GIF: 96 frames, 8 seconds, 12 fps, infinite loop; decoded frame delays verified. Previews simulated attacks at 120/min; no audio captured.");
        RenderDirected(fullPath);
    }

    private static void RenderDirected(string fullPath)
    {
        using var reserved = new PixelCat { Width = 224, Height = 224, Skin = CatSkin.Snow };
        using var curious = new PixelCat { Width = 224, Height = 224, Skin = CatSkin.Snow };
        PixelCat[] cats = [reserved, curious];
        CompanionDirector[] directors = [new(24), new(24)];
        string[] labels = ["克制", "好奇"];
        string[] actions = ["等待", "注意到起音", "安静倾听", "轻微跟拍", "投入", "慢慢收住", "整理自己", "休息", "一起弹"];
        foreach (var cat in cats) { cat.Measure(new Size(224, 224)); cat.Arrange(new Rect(0, 0, 224, 224)); }
        var gif = new GifBitmapEncoder();
        var transitions = new List<string>();
        var previous = new CompanionAction?[2];
        for (int frame = 0; frame < 240; frame++)
        {
            var time = frame / 12d;
            bool active = time >= 1 && time < 10;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(37, 58, 49)), null, new Rect(0, 0, 448, 300));
                Label(dc, active ? "模拟输入 · 规律起音" : "模拟输入 · 安静", 15, 10, 15, Brushes.Beige);
                for (int i = 0; i < 2; i++)
                {
                    var pose = directors[i].Step(active, active && time >= 3, active && frame % 6 == 0,
                        1 / 12d, false, (ExpressionStyle)i);
                    cats[i].PreviewSeconds = time;
                    cats[i].PreviewPulse = active ? Math.Exp(-(time % .5) / .16) : 0;
                    cats[i].SetPose(pose);
                    cats[i].UpdateLayout();
                    dc.DrawImage(RenderVisual(cats[i], 224, 224), new Rect(i * 224, 35, 224, 224));
                    Label(dc, labels[i] + " · " + actions[(int)pose.Action], i * 224 + 24, 265, 14, Brushes.Beige);
                    if (previous[i] != pose.Action) transitions.Add($"{time:F2}s {labels[i]} {pose.Action}");
                    previous[i] = pose.Action;
                }
            }
            gif.Frames.Add(BitmapFrame.Create(RenderVisual(visual, 448, 300)));
        }
        var basename = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath) + "-behavior");
        SaveTimedAnimation(gif, basename + ".gif");
        // Check the same rendered gesture at two different phases under reduced motion.
        foreach (var action in Enum.GetValues<CompanionAction>())
        {
            using var cat = new PixelCat { Width = 224, Height = 224, ReducedMotion = true };
            cat.Measure(new Size(224, 224)); cat.Arrange(new Rect(0, 0, 224, 224));
            var pose = new CompanionPose(action, action == CompanionAction.Rest ? CompanionMood.Resting :
                action == CompanionAction.Engage ? CompanionMood.Excited : CompanionMood.Listening, .8);
            cat.SetPose(pose); cat.PreviewSeconds = 0; cat.PreviewPulse = 1; cat.UpdateLayout();
            var first = RenderVisual(cat, 224, 224);
            cat.SetPose(pose with { Weight = .3 }); cat.PreviewSeconds = .7; cat.PreviewPulse = 0; cat.UpdateLayout();
            var second = RenderVisual(cat, 224, 224);
            var a = new byte[224 * 224 * 4]; var b = new byte[a.Length];
            first.CopyPixels(a, 224 * 4, 0); second.CopyPixels(b, 224 * 4, 0);
            if (!a.AsSpan().SequenceEqual(b)) throw new InvalidOperationException("Directed reduced motion moved: " + action);
        }
        File.WriteAllLines(basename + ".txt", new[] { "20-second deterministic simulation; 240 frames, decoded timing verified. Reduced-motion gesture checks: 8/8. No audio captured." }.Concat(transitions));
    }

    private static void SaveTimedAnimation(GifBitmapEncoder encoder, string path)
    {
        using var encoded = new MemoryStream();
        encoder.Save(encoded);
        var bytes = encoded.ToArray();
        int headerLength = 13 + ((bytes[10] & 0x80) != 0 ? 3 * (1 << ((bytes[10] & 7) + 1)) : 0);
        int cursor = headerLength, controls = 0, images = 0;
        // WPF's GIF encoder discards frame timing metadata. Patch its graphic controls
        // without re-encoding the pixel data; cumulative rounding keeps 12 fps at 8 s.
        while (cursor < bytes.Length)
        {
            byte marker = bytes[cursor++];
            if (marker == 0x3b) break;
            if (marker == 0x21)
            {
                byte label = bytes[cursor++];
                if (label == 0xf9)
                {
                    if (bytes[cursor] != 4) throw new InvalidDataException("Invalid GIF graphic control.");
                    int delay = (int)Math.Round((controls + 1) * 100d / 12) - (int)Math.Round(controls * 100d / 12);
                    bytes[cursor + 2] = (byte)delay;
                    bytes[cursor + 3] = 0;
                    controls++;
                }
            }
            else if (marker == 0x2c)
            {
                byte flags = bytes[cursor + 8];
                cursor += 9;
                if ((flags & 0x80) != 0) cursor += 3 * (1 << ((flags & 7) + 1));
                cursor++; // LZW minimum code size.
                images++;
            }
            else throw new InvalidDataException("Unexpected GIF block.");
            int blockSize;
            do { blockSize = bytes[cursor++]; cursor += blockSize; } while (blockSize != 0);
        }
        if (controls != encoder.Frames.Count || images != controls)
            throw new InvalidDataException("GIF frame timing is incomplete.");

        using var timed = new MemoryStream();
        timed.Write(bytes, 0, headerLength);
        // NETSCAPE application extension, repeat count 0 means continuous looping.
        timed.Write(new byte[] { 0x21, 0xff, 11, 78, 69, 84, 83, 67, 65, 80, 69, 50, 46, 48, 3, 1, 0, 0, 0 });
        timed.Write(bytes, headerLength, bytes.Length - headerLength);
        timed.Position = 0;
        var decoder = new GifBitmapDecoder(timed, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count != controls) throw new InvalidDataException("GIF lost animation frames.");
        for (int frame = 0; frame < controls; frame++)
        {
            var expected = (ushort)((int)Math.Round((frame + 1) * 100d / 12) - (int)Math.Round(frame * 100d / 12));
            if (decoder.Frames[frame].Metadata is not BitmapMetadata metadata ||
                !Equals(metadata.GetQuery("/grctlext/Delay"), expected))
                throw new InvalidDataException("GIF frame delay did not survive decoding.");
        }
        File.WriteAllBytes(path, timed.ToArray());
    }

    private static RenderTargetBitmap Cat(CatSkin skin, CompanionMood mood, double time, bool reduced)
    {
        using var cat = new PixelCat { Width = 224, Height = 224, Skin = skin, Mood = mood, PreviewSeconds = time,
            PreviewPulse = Math.Exp(-(time % .5) / .16), ReducedMotion = reduced };
        cat.Measure(new Size(224, 224)); cat.Arrange(new Rect(0, 0, 224, 224)); cat.UpdateLayout();
        return RenderVisual(cat, 224, 224);
    }
    private static RenderTargetBitmap RenderVisual(Visual visual, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }
    private static void Label(DrawingContext dc, string text, double x, double y, double size, Brush brush) =>
        dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
            new Typeface("Microsoft YaHei UI"), size, brush, 1), new Point(x, y));
}
