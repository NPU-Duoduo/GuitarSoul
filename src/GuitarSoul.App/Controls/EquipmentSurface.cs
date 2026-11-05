using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GuitarSoul.App.Controls;

public static class EquipmentMaterials
{
    public static Brush Graphite { get; }=Finish(Color.FromRgb(26,33,29),0);
    public static Brush Tolex { get; }=Finish(Color.FromRgb(16,22,18),1);
    public static Brush Bronze { get; }=Finish(Color.FromRgb(68,64,48),2);
    public static Brush GreenMetal { get; }=Finish(Color.FromRgb(39,51,41),2);
    public static Brush RoomGrain { get; }=Finish(Colors.Transparent,3);
    public static Brush Grille { get; }=Finish(Color.FromRgb(19,23,19),4);
    public static Brush AmpGold { get; }=Finish(Color.FromRgb(176,151,93),2);
    private static Brush Finish(Color color,int kind)
    {
        const int size=128;
        var drawing=new DrawingGroup();
        using(var c=drawing.Open())
        {
            c.DrawRectangle(new SolidColorBrush(color),null,new Rect(0,0,size,size));
            var random=new Random(701+kind);
            for(int i=0;i<2800;i++)
            {
                int x=random.Next(size),y=random.Next(size);
                byte alpha=(byte)random.Next(3,kind==1?10:kind==3?11:15);
                var ink=new SolidColorBrush(i%2==0?Color.FromArgb(alpha,218,213,186):Color.FromArgb(alpha,0,5,2));
                c.DrawRectangle(ink,null,new Rect(x,y,kind==1?1.6:.7,kind==1?.7:.55));
            }
            if(kind==2)
                for(int y=0;y<size;y+=3)
                    c.DrawRectangle(new SolidColorBrush(Color.FromArgb(7,225,213,177)),null,new Rect(0,y,size,.35));
            if(kind==4)
                for(int y=0;y<size;y+=4)
                    for(int x=0;x<size;x+=4)
                    {
                        c.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(50,53,39)),.7),new Point(x,y+3),new Point(x+3,y));
                        c.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(7,12,8)),1),new Point(x,y),new Point(x+3,y+3));
                    }
        }
        drawing.Freeze();
        var brush=new DrawingBrush(drawing) { TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,
            Viewport=new Rect(0,0,size,size),ViewboxUnits=BrushMappingMode.Absolute,
            Viewbox=new Rect(0,0,size,size),Stretch=Stretch.Fill };
        brush.Freeze();return brush;
    }
}

/// <summary>Passive enclosure detail: fasteners and bevels never masquerade as controls.</summary>
public sealed class EquipmentSurface : Border
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if(ActualWidth<30||ActualHeight<30) return;
        var light=new Pen(new SolidColorBrush(Color.FromArgb(43,211,209,179)),.7);
        var shade=new Pen(new SolidColorBrush(Color.FromArgb(150,2,7,4)),1);
        dc.DrawLine(light,new Point(5,1.5),new Point(ActualWidth-5,1.5));
        dc.DrawLine(shade,new Point(5,ActualHeight-2),new Point(ActualWidth-5,ActualHeight-2));
        foreach(var p in new[]{new Point(6,6),new Point(ActualWidth-6,6),new Point(6,ActualHeight-6),new Point(ActualWidth-6,ActualHeight-6)}) DrawScrew(dc,p);
    }
    internal static void DrawScrew(DrawingContext c,Point p)
    {
        c.DrawEllipse(new SolidColorBrush(Color.FromArgb(140,0,0,0)),null,new Point(p.X,p.Y+1),2.7,2.7);
        c.DrawEllipse(new LinearGradientBrush(Color.FromRgb(108,110,91),Color.FromRgb(36,44,35),110),
            new Pen(new SolidColorBrush(Color.FromRgb(18,25,19)),.65),p,2.2,2.2);
        c.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(13,21,15)),.8),new Point(p.X-1.1,p.Y+.6),new Point(p.X+1.1,p.Y-.6));
    }
}
