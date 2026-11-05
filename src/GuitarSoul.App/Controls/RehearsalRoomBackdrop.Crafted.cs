using System.Windows;
using System.Windows.Media;

namespace GuitarSoul.App.Controls;

public sealed partial class RehearsalRoomBackdrop
{
    private static SolidColorBrush Rgb(int r, int g, int b) =>
        Rgb((byte)Math.Clamp(r,0,255),(byte)Math.Clamp(g,0,255),(byte)Math.Clamp(b,0,255));
    private DrawingGroup? _craftedRoom;
    private (int Left, int Right, int Top, int Bottom, long Minute, double Foot, double Moon) _craftedKey;
    private static readonly Brush[] Textile =
    [
        Rgb(0x1B, 0x25, 0x1B), Rgb(0x2E, 0x34, 0x21), Rgb(0x4D, 0x4B, 0x2E),
        Rgb(0x72, 0x64, 0x3B), Rgb(0x9B, 0x7A, 0x42), Rgb(0xB0, 0x8B, 0x4D)
    ];

    private void DrawCraftedRoom(DrawingContext dc, double width, double height, double time)
    {
        int scale = Math.Clamp(PixelScale, 2, 9);
        double ox = Math.Round((width - StageWidth * scale) / 2);
        double oy = StageTopFor(height, scale);
        int left = (int)Math.Floor(-ox / scale) - 2;
        int right = (int)Math.Ceiling((width - ox) / scale) + 2;
        int top = (int)Math.Floor(-oy / scale) - 2;
        int bottom = (int)Math.Ceiling((height - oy) / scale) + 2;
        var key = (left, right, top, bottom, CurrentLocalTime.Ticks / TimeSpan.TicksPerMinute,
            _catFootY ?? 162, Math.Round(CurrentMoonPhase,4));
        if (_craftedRoom is null || _craftedKey != key)
        {
            var room = new DrawingGroup();
            using (var c = room.Open())
            {
                CraftedWall(c, left, right, top);
                CraftedWindow(c);
                CraftedAcoustics(c);
                CraftedFloor(c, left, right, bottom);
                CraftedCarpet(c, left, right, bottom);
                CraftedShelf(c);
                CraftedLamp(c);
                // Large, overlapping volumes are seen from the cat's eye height.
                CraftedCase(c);
                CraftedCabinet(c);
                CraftedGuitar(c);
                CraftedPlant(c, 126, 77, 1.25, .0);
                CraftedCable(c);
                DrawCatShadow(c);
                // The room falls off at the edges; the centre stays open for the character.
                var vignette = new RadialGradientBrush();
                vignette.GradientStops.Add(new GradientStop(Color.FromArgb(0,3,10,7), .35));
                vignette.GradientStops.Add(new GradientStop(Color.FromArgb(130, 3, 10, 7), 1));
                vignette.Freeze();
                c.DrawRectangle(vignette, null, new Rect(left, top, right-left, bottom-top));
            }
            room.Freeze();
            _craftedRoom = room;
            _craftedKey = key;
        }
        dc.PushTransform(new TranslateTransform(ox, oy));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawDrawing(_craftedRoom);
        CraftedAtmosphere(dc, time);
        dc.Pop();
        dc.Pop();
        var view=new Rect(-ox/scale,-oy/scale,width/scale,height/scale);
        DrawBlurredPlane(dc,width,height,view,scale,1.65,ref _nearLayer,c=>
        {
            double sway=ReducedMotion?0:Math.Sin(time*.15)*.6;
            c.PushTransform(new TranslateTransform(0,view.Bottom-233));
            CraftedPedals(c);
            CraftedNearLeaves(c, sway);
            if(PlayerActive)
            {
                c.PushOpacity(.25+.5*Math.Clamp(Level/60,0,1));
                P(c,Signal,34,212,1,1);
                c.Pop();
            }
            c.Pop();
        });
    }

    private static uint SurfaceHash(int x, int y, int salt = 0)
    {
        uint h = unchecked((uint)(x * 374761393 + y * 668265263 + salt * 144269));
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }

    private static void P(DrawingContext c, Brush b, double x, double y, double w, double h) =>
        c.DrawRectangle(b, null, new Rect(x, y, w, h));

    private static void Poly(DrawingContext c, Brush brush, params Point[] points)
    {
        var shape = new StreamGeometry();
        using (var g = shape.Open())
        {
            g.BeginFigure(points[0], true, true);
            for (int i=1; i<points.Length; i++) g.LineTo(points[i], true, false);
        }
        shape.Freeze();
        c.DrawGeometry(brush, null, shape);
    }

    private static void CraftedWall(DrawingContext c, int left, int right, int top)
    {
        var paint = new LinearGradientBrush(Rgb(10, 23, 19).Color, Rgb(19, 35, 27).Color, 90);
        paint.Freeze();
        P(c, paint, left, top, right-left, 131-top);
        // Broad plaster planes and recessed joinery; evenly spaced black stripes made
        // the room read as a flat grid. Grain precedes illumination so it belongs to it.
        Brush[] grain = [Rgb(15,29,22), Rgb(17,31,24), Rgb(20,34,25)];
        for (int y=top;y<128;y++)
            for (int x=left;x<right;x++)
            {
                uint h=SurfaceHash(x,y);
                if(h%4==0) P(c,grain[h%3],x,y,.3+h%3*.15,.4);
            }
        var light = new RadialGradientBrush(Color.FromArgb(63,208,151,66), Color.FromArgb(0,208,151,66))
        { MappingMode=BrushMappingMode.Absolute, Center=new Point(43,67), GradientOrigin=new Point(43,55), RadiusX=82, RadiusY=99 };
        light.Freeze();
        P(c, light, left, top, right-left, 130-top);
        // A wall return falls away behind the case, with a softened junction.
        var returnShadow=new LinearGradientBrush(Color.FromArgb(0,2,9,6),Color.FromArgb(150,2,9,6),0);
        P(c,returnShadow,188,top,Math.Max(1,right-188),130-top);
        P(c,Rgb(8,18,13),71,top,.7,126-top);
        P(c,Rgb(28,40,27),72,top,.4,126-top);
        SoftPool(c,Color.FromArgb(100,3,10,6),new Rect(62,53,29,77));
        P(c,Rgb(6,15,11),left,126,right-left,4);
        P(c,Rgb(48,51,32),left,127,right-left,.5);
        P(c,Rgb(5,10,7),left,130,right-left,1);
    }

    private void CraftedWindow(DrawingContext c)
    {
        // A small deep-set window stays clear of all overlaid labels.
        const int x=154,y=5,w=35,h=53;
        P(c,Rgb(5,12,10),x-3,y-3,w+6,h+6);
        P(c,Rgb(51,43,28),x-2,y-2,w+4,h+4);
        P(c,Rgb(82,63,38),x-1,y-1,w+2,1);
        var period=PeriodAt(CurrentLocalTime.TimeOfDay.TotalHours);
        for(int row=0;row<h;row++) P(c,SkyAt(row/(double)h,period),x,y+row,w,1);
        if(period==SkyPeriod.Night)
        {
            for(int i=0;i<11;i++)
            {
                int sx=x+2+(int)(SurfaceHash(i,18)%30), sy=y+3+(int)(SurfaceHash(i,29)%40);
                P(c,Star,sx,sy,.5,.5);
            }
            double progress=(CurrentLocalTime.Hour>=18?CurrentLocalTime.TimeOfDay.TotalHours-18:
                CurrentLocalTime.TimeOfDay.TotalHours+6)/12;
            DrawPixelMoon(c,x+6+23*Math.Clamp(progress,0,1),y+14-7*Math.Sin(progress*Math.PI),CurrentMoonPhase);
        }
        else
        {
            double progress=Math.Clamp((CurrentLocalTime.TimeOfDay.TotalHours-6)/12,0,1);
            P(c,WarmDeep,x+5+24*progress,y+15-9*Math.Sin(progress*Math.PI),4,4);
            P(c,Glow,x+6+24*progress,y+16-9*Math.Sin(progress*Math.PI),2,2);
        }
        P(c,Rgb(41,36,24),x+17,y,1.5,h);
        P(c,Rgb(56,45,29),x,y+26,w,1.5);
        P(c,Rgb(16,25,20),x-2,y-1,3,h+1);
        P(c,Rgb(27,39,29),x-1,y,1,h);
        P(c,Rgb(16,25,20),x+w-1,y-1,4,h+1);
        P(c,Rgb(63,53,33),x-3,y+h,w+7,2);
    }

    private static void CraftedAcoustics(DrawingContext c)
    {
        SoftPool(c,Color.FromArgb(140,2,9,6),new Rect(104,-7,52,83));
        P(c,Rgb(6,15,11),105,-13,42,75);
        P(c,new LinearGradientBrush(Rgb(48,48,31).Color,Rgb(22,34,25).Color,0),106,-12,40,72);
        P(c,Rgb(67,57,34),106,-12,.6,72);
        P(c,Rgb(48,46,31),106,59,40,1);
        for(int i=0;i<8;i++)
        {
            int x=109+i*4;
            P(c,Rgb(9,20,16),x,-4+i%3*6,1.5,53-i%3*6);
            P(c,Rgb(56,55,35),x+1.5,-4+i%3*6,.4,53-i%3*6);
            for(int j=0;j<36;j++)
                if(SurfaceHash(i,j)%3==0) P(c,Rgb(40,46,29),x+2,j*1.7-9,.35,1.4);
        }
        SoftPool(c,Color.FromArgb(140,3,9,5),new Rect(73,24,32,45));
        P(c,Rgb(5,12,10),73,24,25,37);
        P(c,Rgb(48,44,31),74,25,23,35);
        P(c,Rgb(93,77,47),75,26,21,.5);
        P(c,Rgb(147,136,102),77,28,17,29);
        P(c,Rgb(20,39,32),78,29,15,27);
        for(int x=78;x<93;x++)
        {
            int ridge=42-(int)(8*Math.Max(0,1-Math.Abs(x-84)/7.0));
            P(c,Rgb(42,61,43),x,ridge,1,56-ridge);
            P(c,Rgb(72,79,47),x,ridge,1,1);
        }
        // Small layered fir silhouettes, flecked paper and an inset glass highlight.
        for(int tree=0;tree<7;tree++)
        {
            double x=78+tree*2.1,tip=42+SurfaceHash(tree,4)%7;
            Poly(c,Rgb(18+tree%3*5,34+tree%3*5,27),new Point(x,tip),new Point(x-1.7,54),new Point(x+2,54));
        }
        P(c,Rgb(105,104,73),77,28,.4,29); P(c,Rgb(111,106,69),77,56.5,17,.4);
    }

    private static void CraftedFloor(DrawingContext c,int left,int right,int bottom)
    {
        P(c,new LinearGradientBrush(Rgb(47,36,22).Color,Rgb(13,19,14).Color,90),left,131,right-left,bottom-131);
        // A low camera: the eye line is just above the floor, not high on the wall.
        // World coordinates share the same vanishing point as the carpet and cabinet.
        const double horizon=RoomEyeY;
        for(int k=-10;k<11;k++)
        {
            double back=104+k*15*(131-horizon)/24;
            double front=104+k*15*(bottom-horizon)/24;
            var seam=new Pen(Rgb(24,24,15),.35); seam.Freeze();
            c.DrawLine(seam,new Point(back,131),new Point(front,bottom));
            for(int j=0;j<4;j++)
            {
                double y=horizon+2400/(110-j*24+(k%3)*7);
                if(y<131||y>bottom) continue;
                double start=104+k*15*(y-horizon)/24;
                double end=104+(k+1)*15*(y-horizon)/24;
                c.DrawLine(seam,new Point(start,y),new Point(end,y));
            }
        }
        Brush[] scratches=[Rgb(57,45,27),Rgb(70,52,29),Rgb(31,29,19)];
        for(int y=132;y<bottom;y++) for(int x=left;x<right;x++)
        {
            uint h=SurfaceHash(x,y,3);
            if(h%43==0) P(c,scratches[h%3],x,y,1+h%3,.5);
        }
    }

    private const double RoomEyeY=110;

    private static void CraftedCarpet(DrawingContext c, int left, int right, int bottom)
    {
        // Sample a woven rug in world space, then project it onto the floor. Its near
        // edge is outside the frame; the motif compresses toward the rear of the room.
        const int top=134;
        Brush[] wool=new Brush[48];
        for(int i=0;i<wool.Length;i++) wool[i]=Rgb(16+i*2,26+(int)(i*1.45),18+(int)(i*.65));
        for(double y=top;y<bottom;y+=.5)
        {
            double projection=(y-RoomEyeY)/24;
            double half=78*projection;
            double z=2400/(y-RoomEyeY);
            P(c,Rgb(20,29,20),Math.Max(left,104-half),y,Math.Min(right,104+half)-Math.Max(left,104-half),.5);
            for(double x=Math.Max(left,104-half);x<Math.Min(right,104+half);x+=.5)
            {
                uint hash=SurfaceHash((int)(x*2),(int)(y*2),11);
                double u=(x-104)/projection;
                double edge=Math.Min(78-Math.Abs(u),100-z);
                double mx=(u+180+(Math.Floor(z/10)%2)*6)%12-6, mz=(z+10)%10-5;
                double radius=Math.Sqrt(mx*mx+mz*mz*1.4);
                double petal=2.2+.7*Math.Cos(Math.Atan2(mz,mx)*6);
                bool floral=Math.Abs(radius-petal)<.5||radius<.65||Math.Abs(mx-Math.Sin(z*.6)*2)<.3;
                bool border=edge<9;
                bool thread=border?(edge<1||Math.Abs(edge-3)<.7||edge>5&&floral):floral;
                double illumination=Math.Exp(-Math.Pow((x-47)/90,2)-Math.Pow((y-145)/36,2));
                double shade=Math.Clamp(1-(y-158)/135,.26,1);
                int tone=(int)(((thread?12:3)+illumination*(thread?24:10)+(hash%7)-3)*shade);
                if(hash%7==0) tone-=9; // interrupted old wool, not a dotted outline
                if(hash%17==0&&illumination>.3) tone+=10;
                P(c,wool[Math.Clamp(tone,0,wool.Length-1)],x,y,.5,.5);
            }
        }
        var light=new RadialGradientBrush(Color.FromArgb(32,222,163,74),Color.FromArgb(0,222,163,74));
        light.Freeze();
        P(c,light,-25,124,195,65);
    }

    private static void CraftedShelf(DrawingContext c)
    {
        // The front face is nearly head-on. The right return recedes toward (104,110);
        // because the top is above the eye line, there is no visible tabletop trapezoid.
        SoftPool(c,Color.FromArgb(140,2,7,4),new Rect(-19,124,102,17));
        Poly(c,Rgb(27,26,17),new Point(-14,60),new Point(57,60),new Point(57,134),new Point(-14,134));
        Poly(c,Rgb(38,32,19),new Point(57,60),new Point(63,66),new Point(63,131),new Point(57,134));
        P(c,new LinearGradientBrush(Rgb(45,32,17).Color,Rgb(16,22,15).Color,0),-13,63,69,69);
        for(int row=0;row<3;row++)
        {
            int y=63+row*23;
            P(c,Rgb(12,19,13),-10,y,64,21);
            P(c,new LinearGradientBrush(Rgb(49-row*6,37-row*4,21).Color,Rgb(17,22,14).Color,90),-10,y+5,63,16);
            for(int i=0;i<19;i++)
            {
                double x=-9+i*3.3;
                uint hash=SurfaceHash(i,row,6);
                int height=9+(int)(hash%10);
                int tone=(int)(hash%26);
                P(c,Rgb(40+tone,40+tone/2,25+tone/4),x,y+21-height,2.2,height);
                P(c,Rgb(87+tone,70+tone/2,38+tone/3),x+.2,y+21-height,.35,height);
                P(c,Rgb(17,22,14),x+1.7,y+21-height,.5,height);
                if(i%3!=0) P(c,Rgb(113,99,62),x+.4,y+24-height,1.1,.5);
            }
            P(c,new LinearGradientBrush(Rgb(91-row*9,61-row*7,31).Color,Rgb(36,32,19).Color,90),-14,y+21,72,2);
            P(c,Rgb(119-row*16,84-row*11,41),-13,y+21,70,.45);
            for(int i=0;i<32;i++) P(c,Rgb(39,32,18),-12+i*2.1,y+21.6,1.2,.25);
        }
        P(c,Rgb(83,55,27),54,61,3,73);
        P(c,Rgb(121,82,38),54,62,.5,71);
        P(c,Rgb(65,45,24),-14,58,72,4);
        P(c,Rgb(147,103,48),-12,58,69,.5);
        P(c,Rgb(32,27,17),-13,62,68,2);
        // A stack of records, a mug and a small folded cloth sit behind the front lip.
        for(int i=0;i<4;i++)
        {
            P(c,i%2==0?Rgb(110,85,48):Rgb(48,45,30),22-i%2,56-i*1.5,17+i%3,1.2);
            P(c,Rgb(149,115,61),23,56-i*1.5,8,.25);
        }
        P(c,new LinearGradientBrush(Rgb(102,88,51).Color,Rgb(42,43,27).Color,0),44,51,5,7);
        c.DrawEllipse(null,FrozenPen(Rgb(88,77,44),.7),new Point(50,54),1.7,2);
        P(c,Rgb(27,29,18),44.5,51,4,.7);
        for(int i=0;i<5;i++) P(c,Rgb(80-i*6,75-i*5,45-i*3),4,54+i*.6,12,.5);
    }

    private static void CraftedLamp(DrawingContext c)
    {
        // A gooseneck lamp reaches over the shelf. The head and pool agree on direction.
        var stem=new Pen(Rgb(48,45,28),1.4); stem.Freeze();
        var neck=new StreamGeometry();
        using(var g=neck.Open())
        {
            g.BeginFigure(new Point(27,57),false,false);
            g.LineTo(new Point(27,21),true,false);
            g.BezierTo(new Point(27,13),new Point(38,12),new Point(43,20),true,false);
        }
        neck.Freeze(); c.DrawGeometry(null,stem,neck);
        Poly(c,Rgb(12,24,18),new Point(37,21),new Point(45,18),new Point(51,28),new Point(34,33));
        Poly(c,Rgb(57,53,31),new Point(38,21),new Point(44,19),new Point(49,27),new Point(35,31));
        Poly(c,Rgb(229,175,85),new Point(34,31),new Point(51,27),new Point(50,30),new Point(35,34));
        Poly(c,Rgb(255,229,161),new Point(36,32),new Point(49,28.5),new Point(48,30.5),new Point(36,33.5));
        var pool=new RadialGradientBrush(Color.FromArgb(80,238,164,67),Color.FromArgb(0,238,164,67))
        { MappingMode=BrushMappingMode.Absolute, Center=new Point(43,65),GradientOrigin=new Point(43,36),RadiusX=48,RadiusY=86 };
        pool.Freeze();
        P(c,pool,-12,-12,140,180);
        P(c,Rgb(75,57,30),23,57,10,1);
    }

    private void CraftedCabinet(DrawingContext c)
    {
        const int x=128,y=79,w=56,h=55;
        SoftPool(c,Color.FromArgb(190,3,8,5),new Rect(x-9,y+h-3,w+18,13));
        // The amp is taller than the camera. Show its left return, never its top panel.
        Poly(c,Rgb(13,22,16),new Point(x-5,y+5),new Point(x,y),new Point(x,y+h),new Point(x-5,y+h-3));
        P(c,new LinearGradientBrush(Rgb(53,47,29).Color,Rgb(18,26,18).Color,0),x,y,w,h);
        P(c,Rgb(116,91,46),x+.5,y+.5,w-1,.5);
        P(c,Rgb(83,70,38),x+.5,y+1,.6,h-2);
        P(c,Rgb(10,16,12),x+w-2,y+2,2,h-3);
        P(c,new LinearGradientBrush(Rgb(47,42,27).Color,Rgb(8,16,12).Color,90),x+2,y+3,w-5,7);
        for(int i=0;i<7;i++)
        {
            double k=x+5+i*5;
            c.DrawEllipse(Rgb(9,15,11),null,new Point(k+.6,y+6),1.4,1.5);
            c.DrawEllipse(Rgb(122,109,71),null,new Point(k,y+5.6),.8,1);
            P(c,Rgb(205,177,104),k-.35,y+4.7,.35,.8);
            P(c,Rgb(94,87,56),k-1,y+8,2,.25);
        }
        P(c,Rgb(111,86,43),x+2,y+11,w-5,.6);
        P(c,Rgb(13,23,17),x+3,y+12,w-7,h-16);
        // Woven speaker cloth follows one plane; the driver is a subdued volume behind it.
        for(double row=y+12;row<y+h-4;row+=.6)
            for(double col=x+3;col<x+w-4;col+=.6)
            {
                uint hash=SurfaceHash((int)(col*4),(int)(row*4),13);
                double radius=Math.Sqrt(Math.Pow((col-x-w*.5)/22,2)+Math.Pow((row-y-32)/20,2));
                int shade=(int)(12*Math.Exp(-Math.Pow((radius-.78)/.18,2)))+(int)(hash%15);
                P(c,Rgb(36+shade,39+shade,25+shade/2),col,row,.3+(hash%3)*.06,.45);
            }
        P(c,Rgb(96,80,46),x+2,y+h-3,w-5,.5);
        P(c,Rgb(6,11,8),x+2,y+h,4,1.5); P(c,Rgb(6,11,8),x+w-6,y+h,4,1.5);
        for(int corner=0;corner<4;corner++)
        {
            int cx=x+(corner%2==0?0:w-4), cy=y+(corner<2?0:h-4);
            P(c,Rgb(51,54,37),cx,cy,4,1); P(c,Rgb(67,66,41),cx,cy,1,4);
            P(c,Rgb(126,110,65),cx+.8,cy+.7,.5,.5);
        }
        P(c,Rgb(8,17,12),x+17,y-3,17,2); P(c,Rgb(63,59,36),x+18,y-3,15,.6);
        P(c,Rgb(34,40,27),x+34,y-4,12,4); P(c,Rgb(87,75,42),x+34,y-4,12,.5);
        P(c,Rgb(7,15,10),x+38,y-5,2,1); P(c,Rgb(7,15,10),x+43,y-5,2,1);
    }

    private static void CraftedPlant(DrawingContext c,double x,double y,double scale,double sway)
    {
        c.PushTransform(new TranslateTransform(x,y));
        c.PushTransform(new ScaleTransform(scale,scale));
        Poly(c,Rgb(53,38,23),new Point(-4,0),new Point(4,0),new Point(3,8),new Point(-3,8));
        P(c,Rgb(94,61,31),-4,0,8,1);
        for(int branch=0;branch<7;branch++)
        {
            double angle=-2.9+branch*.43;
            for(int j=0;j<4;j++)
            {
                double px=Math.Cos(angle)*(j+1)*2.2+sway;
                double py=-2-Math.Sin(.5+branch*.13)*(j+1)*2.7;
                FillLeaf(c,branch%2==0?Leaf:LeafDeep,px,py,2.5,1.1,angle);
                P(c,Rgb(83,96,47),px,py,.5,.5);
            }
        }
        c.Pop(); c.Pop();
    }

    private static void CraftedCable(DrawingContext c)
    {
        var geometry=new StreamGeometry();
        using(var g=geometry.Open())
        {
            g.BeginFigure(new Point(173,134),false,false);
            g.BezierTo(new Point(194,154),new Point(152,162),new Point(172,178),true,false);
            g.BezierTo(new Point(185,187),new Point(207,188),new Point(201,210),true,false);
        }
        geometry.Freeze();
        c.DrawGeometry(null,FrozenPen(Rgb(7,12,8),1.5),geometry);
        c.DrawGeometry(null,FrozenPen(Rgb(62,53,30),.4),geometry);
    }

    private static void CraftedPedals(DrawingContext c)
    {
        SoftPool(c,Color.FromArgb(235,2,6,4),new Rect(-35,191,112,49));
        Poly(c,Rgb(13,20,14),new Point(-26,193),new Point(46,204),new Point(64,231),new Point(-34,220));
        Poly(c,Rgb(6,13,9),new Point(-34,220),new Point(64,231),new Point(64,239),new Point(-34,227));
        c.DrawLine(FrozenPen(Rgb(55,51,29),.8),new Point(-34,220),new Point(64,231));
        for(int i=0;i<3;i++)
        {
            int x=-18+i*24,y=193+i*4;
            Poly(c,Rgb(13,20,14),new Point(x-4,y+16),new Point(x+19,y+20),new Point(x+19,y+26),new Point(x-4,y+22));
            Poly(c,i==1?Rgb(53,62,39):Rgb(74,58,34),new Point(x,y),new Point(x+15,y+2),new Point(x+19,y+20),new Point(x-4,y+16));
            c.DrawLine(FrozenPen(Rgb(128,111,68),.6),new Point(x,y),new Point(x+15,y+2));
            for(int knob=0;knob<3;knob++)
            {
                double kx=x+2+knob*4.5,ky=y+3+knob*.6;
                c.DrawEllipse(Rgb(9,15,11),null,new Point(kx,ky),1.6,1.9);
                c.DrawEllipse(Rgb(71,72,48),null,new Point(kx-.3,ky-.6),1.3,.8);
                P(c,Rgb(150,133,83),kx-.5,ky-1,.4,.7);
            }
            c.DrawEllipse(Rgb(17,23,16),null,new Point(x+7,y+13),3,2.2);
            c.DrawEllipse(Rgb(135,129,87),null,new Point(x+7,y+12),2.2,1.4);
            c.DrawEllipse(Rgb(195,178,118),null,new Point(x+6.7,y+11.5),1.2,.6);
            P(c,Rgb(144,129,81),x+3,y+8,7,.5);
            c.DrawLine(FrozenPen(Rgb(8,15,10),1.3),new Point(x+17,y+7),new Point(x+26,y+10));
        }
    }

    private void CraftedAtmosphere(DrawingContext c,double time)
    {
        var breath=new RadialGradientBrush(
            Color.FromArgb((byte)(10+5*Math.Sin(time*.37)),238,164,67),Color.FromArgb(0,238,164,67))
        { MappingMode=BrushMappingMode.Absolute, Center=new Point(43,65),GradientOrigin=new Point(43,36),RadiusX=48,RadiusY=86 };
        P(c,breath,-12,-12,140,180);
        // Fixed roots and independently phased stems produce a slow sway, not camera drift.
        for(int vine=0;vine<5;vine++)
        {
            double root=vine<3?5+vine*9:178+(vine-3)*10;
            int length=vine<3?29+vine*8:34+(vine-3)*10;
            for(int row=-17;row<length;row+=3)
            {
                double phase=time*.19+vine*1.8;
                double bend=ReducedMotion?0:Math.Sin(phase+row*.04)*Math.Max(0,row+17)/48;
                double x=root+bend;
                P(c,Rgb(33,47,27),x,row,.5,3);
                double direction=(row/3%2==0)?-1:1;
                FillLeaf(c,Rgb(31+vine*2,48+vine*3,28),x+direction*1.5,row+1,2,1, direction*.55);
                P(c,Rgb(62,72,37),x+direction*2,row+1,.5,.5);
            }
        }
        // Dust remains inside the lamp's air volume, with varied speeds and no fake audio.
        for(int i=0;i<18;i++)
        {
            double phase=(SurfaceHash(i,6)%1000)/1000d;
            double y=35+((phase*84+time*(.28+i%4*.09))%84);
            double x=26+SurfaceHash(i,8)%40+Math.Sin(time*.13+i)*1.1;
            c.PushOpacity(.12+.2*(.5+.5*Math.Sin(time*.37+i)));
            P(c,Glow,Math.Round(x*2)/2,Math.Round(y*2)/2,.5,.5);
            c.Pop();
        }
        // The speaker's pilot and small meter segments are sourced from real playback.
        if(IsPlaying)
        {
            SoftPool(c,Color.FromArgb(100,245,175,67),new Rect(173,81,8,8));
            P(c,Glow,177,85,1,1);
            int segments=(int)Math.Round(Math.Clamp(AccompanimentEnergy,0,1)*6);
            for(int i=0;i<segments;i++) P(c,Warm,132+i*2,88,1,.5);
        }
        if(HasAccompaniment)
        {
            int lit=(int)Math.Round(Math.Clamp(PlaybackProgress,0,1)*33);
            for(int i=0;i<33;i++) P(c,i<lit?Signal:MetalDark,132+i,89,.6,.5);
            if(NextHighlightProgress is double next)
                P(c,Warm,132+Math.Clamp(next,0,1)*32,88,1,1);
        }
        // A short, signal-driven illumination catches the carpet around the paws.
        double age=time-_rugPulseStarted;
        if(age>=0&&age<1.6)
        {
            var shared=new RadialGradientBrush(Color.FromArgb((byte)(45*(1-age/1.6)),232,179,92),Color.FromArgb(0,232,179,92));
            P(c,shared,70,146,69,34);
        }
    }
}
