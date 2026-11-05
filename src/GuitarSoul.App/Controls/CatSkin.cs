using System.Windows.Media;
namespace GuitarSoul.App.Controls;

public enum CatSkin { Snow, Calico, Ginger, Tabby }
internal sealed record CatPalette(Brush Fur, Brush Shade, Brush Bib, Brush Patch, Brush Stripe, Brush Eye)
{
    public static readonly Brush Ink = Color("#32313B"), Ear = Color("#E6B6AD"),
        Nose = Color("#B17E7F"), Glint = Color("#FFFDF6"), Shadow = Color("#20372D");
    public static readonly CatPalette[] All =
    [
        new(Color("#FFFDF6"), Color("#D8D6D0"), Color("#FFFDF6"), Color("#D8D6D0"), Color("#D8D6D0"), Ink),
        new(Color("#FFF9ED"), Color("#DAD0C2"), Color("#FFFDF6"), Color("#DEA56D"), Color("#726C66"), Ink),
        new(Color("#E9B67B"), Color("#CB9460"), Color("#FFF0D9"), Color("#F0C58D"), Color("#BB8153"), Ink),
        new(Color("#B5ADA0"), Color("#958B7E"), Color("#F0E9D9"), Color("#C6BCAE"), Color("#776F64"), Ink)
    ];
    private static Brush Color(string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        brush.Freeze();
        return brush;
    }
}
