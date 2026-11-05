using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App.Controls;

/// <summary>Uses the actual sprite alpha for a solid silhouette; locked portraits contain no coloured detail.</summary>
public sealed class StagePortrait : FrameworkElement
{
    public static readonly DependencyProperty StageProperty = DependencyProperty.Register(nameof(Stage), typeof(StageStyle), typeof(StagePortrait),
        new FrameworkPropertyMetadata(StageStyle.Everyday, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(nameof(IsLocked), typeof(bool), typeof(StagePortrait),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public StageStyle Stage { get => (StageStyle)GetValue(StageProperty); set => SetValue(StageProperty, value); }
    public bool IsLocked { get => (bool)GetValue(IsLockedProperty); set => SetValue(IsLockedProperty, value); }
    private static readonly Dictionary<(StageStyle, bool), BitmapSource> Cache = new();
    internal static BitmapSource Portrait(StageStyle stage, bool locked)
    {
        if (Cache.TryGetValue((stage, locked), out var found)) return found;
        using var cat = new PixelCat { Stage = stage, Skin = CatSkin.Snow, Guitar = StageLook.Guitar(stage, GuitarStyle.Stratocaster),
            IsJamming = true, Mood = CompanionMood.Excited, MotionAmount = 0, ReducedMotion = true, PreviewSeconds = .12, PreviewPulse = 0 };
        cat.Measure(new Size(168, 168)); cat.Arrange(new Rect(0, 0, 168, 168)); cat.UpdateLayout();
        var bitmap = new RenderTargetBitmap(168, 168, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(cat);
        BitmapSource result = bitmap;
        if (locked)
        {
            var pixels = new byte[168 * 168 * 4]; bitmap.CopyPixels(pixels, 168 * 4, 0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte grey = (byte)(106 * pixels[i + 3] / 255);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = grey;
            }
            result = BitmapSource.Create(168, 168, 96, 96, PixelFormats.Pbgra32, null, pixels, 168 * 4);
        }
        result.Freeze(); Cache[(stage, locked)] = result; return result;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
        double size = Math.Min(ActualWidth, ActualHeight);
        dc.DrawImage(Portrait(Stage, IsLocked), new Rect((ActualWidth - size) / 2, (ActualHeight - size) / 2, size, size));
    }
}
