using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.App;

internal static class CatCandidates
{
    public static void Render(string path)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        foreach (bool dark in new[] { false, true })
        foreach (int dpi in new[] { 96, 144 })
        {
            var foreground = dark ? Brushes.Beige : Brushes.DarkSlateGray;
            var background = new SolidColorBrush(dark ? Color.FromRgb(37, 58, 49) : Color.FromRgb(244, 241, 234));
            var sheet = new DrawingVisual();
            VisualTreeHelper.SetRootDpi(sheet, new DpiScale(dpi / 96d, dpi / 96d));
            using (var dc = sheet.RenderOpen())
            {
                dc.DrawRectangle(background, null, new Rect(0, 0, 976, 956));
                void Label(string text, double x, double y, double size) => dc.DrawText(new FormattedText(text,
                    CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"),
                    size, foreground, dpi / 96d), new Point(x, y));
                Label("角色比例对照 · 等待你的选择", 24, 18, 24);
                Label($"每只猫占 224 × 224 DIP · {dpi} DPI · 不更改当前外形与花色", 24, 54, 14);
                string[] labels = ["现有 0.3.1 · 保留", "候选 A · 较短的耳朵", "候选 B · 额头增加一行"];
                string[] moods = ["等待", "倾听", "投入", "休息"];
                for (int variant = 0; variant < 3; variant++)
                {
                    int y = 94 + variant * 282;
                    Label(labels[variant], 24, y, 17);
                    for (int mood = 0; mood < 4; mood++)
                    {
                        using var cat = new PixelCat { Width = 224, Height = 224, Skin = CatSkin.Snow,
                            Mood = (CompanionMood)mood, PreviewSeconds = 0, PreviewPulse = 0, ReducedMotion = true,
                            PreviewFaceVariant = variant };
                        VisualTreeHelper.SetRootDpi(cat, new DpiScale(dpi / 96d, dpi / 96d));
                        cat.Measure(new Size(224, 224)); cat.Arrange(new Rect(0, 0, 224, 224)); cat.UpdateLayout();
                        var bitmap = new RenderTargetBitmap((int)(224 * dpi / 96d), (int)(224 * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32);
                        bitmap.Render(cat);
                        dc.DrawImage(bitmap, new Rect(16 + mood * 240, y + 26, 224, 224));
                        Label(moods[mood], 106 + mood * 240, y + 249, 14);
                    }
                }
            }
            var result = new RenderTargetBitmap((int)(976 * dpi / 96d), (int)(956 * dpi / 96d), dpi, dpi, PixelFormats.Pbgra32);
            result.Render(sheet);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(result));
            var output = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath) + $"-{(dark ? "dark" : "light")}-{dpi}.png");
            using var stream = File.Create(output); encoder.Save(stream);
        }
    }
}
