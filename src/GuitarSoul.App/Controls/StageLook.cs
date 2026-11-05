using System.Windows;
using System.Windows.Media;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App.Controls;

/// <summary>Original stage accessories on the existing pixel grid. Hair overlays the unchanged base face.</summary>
internal static class StageLook
{
    private static readonly Brush Coal = B("#40333F"), Velvet = B("#695068"), Gold = B("#DAAF62"),
        Purple = B("#735882"), Lilac = B("#B89BBA"), Teal = B("#79B9AF"), Sage = B("#7D9E9C"),
        Linen = B("#E6DDC4"), Blue = B("#4F6C79"), Hair = B("#29252B"), HairLight = B("#4A3B43");
    internal static GuitarStyle Guitar(StageStyle stage, GuitarStyle fallback) => stage switch
    {
        StageStyle.HardRock or StageStyle.JimmyPage or StageStyle.BBKing or StageStyle.AngusYoung => GuitarStyle.LesPaul,
        StageStyle.Psychedelic or StageStyle.Mellow or StageStyle.DavidGilmour or StageStyle.KurtCobain or StageStyle.NileRodgers => GuitarStyle.Stratocaster,
        _ => fallback
    };
    internal static void Body(DrawingContext dc, StageStyle stage)
    {
        if (stage == StageStyle.HardRock)
        {
            R(dc, Coal, 12, 25, 5, 8); R(dc, Coal, 24, 25, 4, 7);
            R(dc, Velvet, 13, 25, 2, 5); R(dc, Gold, 15, 27, 1, 1);
        }
        else if (stage == StageStyle.Psychedelic)
        {
            R(dc, Purple, 12, 25, 5, 8); R(dc, Purple, 23, 25, 5, 7);
            R(dc, Lilac, 13, 27, 1, 3); R(dc, Gold, 24, 26, 2, 1);
            R(dc, Teal, 25, 28, 2, 2);
        }
        else if (stage == StageStyle.Mellow)
        {
            R(dc, Blue, 12, 26, 5, 7); R(dc, Sage, 13, 26, 3, 6);
            R(dc, Blue, 24, 26, 4, 7); R(dc, Sage, 24, 26, 2, 5);
        }
        else if (stage == StageStyle.JimmyPage)
        {
            R(dc, Coal, 12, 25, 5, 9); R(dc, Coal, 24, 25, 4, 8);
            R(dc, Gold, 13, 26, 2, 2); R(dc, Gold, 25, 28, 2, 3);
            R(dc, Purple, 14, 29, 2, 3);
        }
        else if (stage == StageStyle.DavidGilmour)
        {
            R(dc, Hair, 12, 26, 5, 7); R(dc, Hair, 24, 26, 4, 7);
            R(dc, Coal, 13, 27, 2, 5); R(dc, Blue, 24, 32, 3, 2);
        }
        else if (stage == StageStyle.BBKing)
        {
            R(dc, Blue, 12, 25, 5, 8); R(dc, Blue, 24, 25, 4, 8);
            R(dc, Linen, 16, 25, 8, 3); R(dc, Coal, 18, 26, 4, 1);
            R(dc, Linen, 14, 26, 1, 4); R(dc, Gold, 25, 28, 1, 1);
        }
        else if (stage == StageStyle.AngusYoung)
        {
            R(dc, Blue, 12, 25, 5, 8); R(dc, Blue, 24, 25, 4, 8);
            R(dc, Linen, 16, 25, 7, 2); R(dc, Gold, 19, 27, 2, 3);
            R(dc, Blue, 13, 34, 5, 2); R(dc, Blue, 23, 34, 5, 2);
        }
        else if (stage == StageStyle.KurtCobain)
        {
            R(dc, Sage, 11, 25, 6, 9); R(dc, Sage, 24, 25, 5, 9);
            R(dc, Linen, 17, 26, 7, 3); R(dc, Blue, 17, 28, 7, 1);
            R(dc, Blue, 13, 32, 2, 1); R(dc, Blue, 26, 30, 2, 1);
        }
        else if (stage == StageStyle.NileRodgers)
        {
            R(dc, Linen, 12, 25, 5, 9); R(dc, Linen, 24, 25, 4, 9);
            R(dc, Coal, 17, 26, 7, 2); R(dc, Gold, 19, 28, 2, 1);
        }
    }
    internal static void Head(DrawingContext dc, StageStyle stage)
    {
        if (stage == StageStyle.HardRock)
        {
            // Broad curls fall in front of the forehead and partly across the left eye.
            // Their uneven stepped ends stay above the guitar's identifying cutaway/neck.
            R(dc, Hair, 11, 11, 18, 4);
            R(dc, Hair, 9, 13, 7, 6); R(dc, Hair, 8, 16, 5, 8);
            R(dc, Hair, 7, 20, 5, 6); R(dc, Hair, 8, 25, 3, 3);
            R(dc, Hair, 6, 22, 2, 3);
            R(dc, Hair, 14, 14, 4, 4); R(dc, Hair, 14, 17, 3, 4);
            R(dc, Hair, 12, 20, 3, 3); R(dc, Hair, 17, 13, 2, 3);
            R(dc, Hair, 24, 14, 6, 5); R(dc, Hair, 25, 18, 6, 5);
            R(dc, Hair, 28, 21, 5, 5); R(dc, Hair, 29, 25, 3, 2);
            R(dc, Hair, 30, 15, 2, 6); R(dc, Hair, 32, 20, 2, 3);
            // Sparse highlights describe curled locks without adding a second face.
            R(dc, HairLight, 11, 14, 1, 3); R(dc, HairLight, 10, 17, 1, 2);
            R(dc, HairLight, 11, 19, 1, 2); R(dc, HairLight, 9, 22, 1, 3);
            R(dc, HairLight, 16, 14, 1, 2); R(dc, HairLight, 15, 16, 1, 2);
            R(dc, HairLight, 27, 14, 1, 3); R(dc, HairLight, 28, 17, 1, 2);
            R(dc, HairLight, 27, 20, 1, 2); R(dc, HairLight, 30, 22, 1, 3);
            // The hat and visible ear tips keep a readable silhouette at desktop scale.
            R(dc, CatPalette.Ink, 16, 3, 9, 7); R(dc, Coal, 17, 4, 7, 5);
            R(dc, Velvet, 18, 4, 2, 3); R(dc, Gold, 17, 8, 7, 1);
            R(dc, CatPalette.Ink, 14, 10, 13, 2); R(dc, Velvet, 15, 10, 10, 1);
        }
        else if (stage == StageStyle.Psychedelic)
        {
            R(dc, Purple, 10, 12, 19, 2); R(dc, Teal, 11, 12, 6, 1);
            R(dc, Gold, 19, 12, 2, 2); R(dc, Lilac, 23, 12, 5, 1);
            R(dc, Purple, 29, 13, 3, 2); R(dc, Lilac, 31, 15, 2, 4);
            R(dc, Teal, 30, 19, 2, 2);
        }
        else if (stage == StageStyle.Mellow)
        {
            R(dc, Blue, 14, 25, 12, 2); R(dc, Linen, 15, 25, 10, 1);
            R(dc, Sage, 23, 27, 3, 4); R(dc, Linen, 23, 30, 3, 1);
        }
        else if (stage == StageStyle.JimmyPage)
        {
            R(dc, Hair, 10, 11, 19, 4); R(dc, Hair, 8, 14, 6, 11);
            R(dc, Hair, 27, 14, 5, 12); R(dc, Hair, 9, 25, 4, 3);
            R(dc, Hair, 29, 24, 4, 4); R(dc, HairLight, 11, 15, 1, 6);
            R(dc, HairLight, 29, 17, 1, 7); R(dc, Hair, 14, 13, 4, 3);
        }
        else if (stage == StageStyle.DavidGilmour)
        {
            R(dc, HairLight, 11, 11, 18, 3); R(dc, HairLight, 9, 14, 4, 8);
            R(dc, HairLight, 28, 14, 3, 9); R(dc, Gold, 12, 12, 4, 1);
            R(dc, HairLight, 12, 13, 5, 2);
        }
        else if (stage == StageStyle.BBKing)
        {
            R(dc, Hair, 12, 11, 15, 2); R(dc, Coal, 10, 13, 2, 4);
            R(dc, Coal, 28, 13, 2, 4); R(dc, Linen, 11, 13, 1, 2);
            R(dc, Linen, 28, 14, 1, 2);
        }
        else if (stage == StageStyle.AngusYoung)
        {
            R(dc, HairLight, 10, 13, 3, 8); R(dc, HairLight, 28, 13, 3, 8);
            R(dc, Coal, 12, 8, 16, 6); R(dc, Blue, 13, 9, 14, 4);
            R(dc, Coal, 11, 14, 19, 2); R(dc, Gold, 19, 10, 2, 2);
        }
        else if (stage == StageStyle.KurtCobain)
        {
            R(dc, Gold, 11, 11, 18, 4); R(dc, Gold, 9, 14, 6, 11);
            R(dc, Gold, 27, 14, 4, 12); R(dc, Gold, 14, 13, 4, 7);
            R(dc, Linen, 11, 16, 1, 6); R(dc, Linen, 28, 16, 1, 7);
            R(dc, Gold, 10, 24, 3, 3);
        }
        else if (stage == StageStyle.NileRodgers)
        {
            R(dc, Hair, 9, 13, 4, 14); R(dc, Hair, 28, 13, 4, 14);
            R(dc, HairLight, 10, 17, 1, 8); R(dc, HairLight, 30, 15, 1, 10);
            R(dc, Linen, 13, 6, 14, 7); R(dc, Coal, 14, 11, 12, 2);
            R(dc, Linen, 10, 13, 21, 2);
            R(dc, Coal, 14, 17, 5, 3); R(dc, Coal, 21, 17, 5, 3);
            R(dc, Coal, 19, 18, 2, 1); R(dc, Lilac, 15, 17, 2, 1);
        }
    }
    private static void R(DrawingContext dc, Brush brush, int x, int y, int w, int h) =>
        dc.DrawRectangle(brush, null, new Rect(x, y, w, h));
    private static Brush B(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze(); return brush;
    }
}
