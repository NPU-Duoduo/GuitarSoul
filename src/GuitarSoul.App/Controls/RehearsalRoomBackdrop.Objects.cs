using System.Windows;
using System.Windows.Media;

namespace GuitarSoul.App.Controls;

public sealed partial class RehearsalRoomBackdrop
{
    private static void SoftPool(DrawingContext c, Color color, Rect bounds)
    {
        var wash=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B));
        wash.Freeze();
        c.DrawRectangle(wash,null,bounds);
    }

    private static double InstrumentAxis(double y)=>28-(142-y)*.28;

    private static double InstrumentWidth(double y)
    {
        ReadOnlySpan<(double Y,double W)> contour=
        [(87,7),(89,12),(93,15),(98,16),(103,14),(108,13),
         (112,15),(117,19),(124,21),(130,20),(136,17),(140,11),(142,3)];
        for(int i=1;i<contour.Length;i++)
            if(y<=contour[i].Y)
            {
                double t=(y-contour[i-1].Y)/(contour[i].Y-contour[i-1].Y);
                return contour[i-1].W+(contour[i].W-contour[i-1].W)*t;
            }
        return 0;
    }

    private static void CraftedGuitar(DrawingContext c)
    {
        SoftPool(c,Color.FromArgb(215,3,8,5),new Rect(-1,137,62,12));
        // A substantial lower bout, narrow waist and angled neck. The side rib is
        // visible on the shaded edge; all contours use the same leaning instrument axis.
        Brush[] spruce=new Brush[64];
        for(int i=0;i<spruce.Length;i++) spruce[i]=Rgb(51+i*2,35+(int)(i*1.45),18+(int)(i*.67));
        for(double y=87;y<142;y+=.5)
        {
            double axis=InstrumentAxis(y),half=InstrumentWidth(y);
            P(c,Rgb(23,20,12),axis-half-1,y,half*2+6,.5);
            P(c,Rgb(53,34,18),axis+half,y+1,4,.5);
            for(double x=axis-half;x<axis+half;x+=.5)
            {
                double u=(x-axis)/half;
                uint grain=SurfaceHash((int)((x-axis)*6),0,42);
                double light=32+15*u+7*Math.Sin((y-87)/55*Math.PI);
                int tone=(int)(light+(grain%9)*.35);
                P(c,spruce[Math.Clamp(tone,0,63)],x,y,.5,.5);
            }
            P(c,Rgb(81,64,35),axis-half,y,.65,.5);
            P(c,Rgb(184,143,78),axis+half-.6,y,.6,.5);
            if((int)(y*2)%7==0) P(c,Rgb(103,66,31),axis+half+2,y+1,.5,.5);
        }
        double holeX=InstrumentAxis(102);
        c.DrawEllipse(Rgb(55,37,19),null,new Point(holeX,102),7.7,7.4);
        c.DrawEllipse(null,FrozenPen(Rgb(180,145,83),.7),new Point(holeX,102),7.1,6.8);
        c.DrawEllipse(null,FrozenPen(Rgb(63,44,23),.55),new Point(holeX,102),6.4,6.2);
        c.DrawEllipse(Rgb(15,19,12),null,new Point(holeX,102),5.8,5.7);
        c.DrawEllipse(Rgb(8,13,9),null,new Point(holeX+.6,101.4),4.8,4.6);
        // An understated pickguard sits beside the hole, not another circular mark.
        Poly(c,Rgb(55,35,19),new Point(holeX+7,101),new Point(holeX+10,108),
            new Point(holeX+9,116),new Point(holeX+4,115),new Point(holeX+4,110));
        for(double y=39;y<96;y+=.5)
        {
            double x=InstrumentAxis(y),half=1.7+(y-39)*.017;
            P(c,Rgb(10,16,11),x-half-.7,y,half*2+1.4,.5);
            P(c,Rgb(42,31,17),x-half,y,half*2,.5);
            P(c,Rgb(104,78,41),x+half-.35,y,.35,.5);
        }
        // Tempered fret spacing tightens toward the body joint.
        for(int fret=1;fret<=19;fret++)
        {
            double y=39+87*(1-Math.Pow(2,-fret/12d));
            double x=InstrumentAxis(y),half=1.7+(y-39)*.017;
            c.DrawLine(FrozenPen(Rgb(115,105,73),.24),new Point(x-half,y),new Point(x+half,y));
            if(fret is 3 or 5 or 7 or 9 or 15) P(c,Rgb(159,147,105),x,y-1.5,.55,.55);
            if(fret==12) { P(c,Rgb(159,147,105),x-.9,y-1.5,.4,.4); P(c,Rgb(159,147,105),x+.5,y-1.5,.4,.4); }
        }
        for(double y=25;y<39;y+=.5)
        {
            double x=InstrumentAxis(y),half=2.4+(39-y)*.035;
            P(c,Rgb(14,19,12),x-half-1,y,half*2+2,.5);
            P(c,Rgb(62,42,22),x-half,y,half*2,.5);
            P(c,Rgb(105,75,37),x+half-.5,y,.5,.5);
        }
        for(int i=0;i<3;i++)
        {
            double y=28+i*3.6,x=InstrumentAxis(y);
            P(c,Rgb(103,99,68),x-5,y,2,1.3); P(c,Rgb(130,118,74),x+3,y,2,1.3);
            P(c,Rgb(176,155,95),x+4,y,.5,.5);
        }
        double bridge=InstrumentAxis(124);
        P(c,Rgb(45,28,16),bridge-8,123,16,3.5);
        P(c,Rgb(171,144,86),bridge-5,123.4,10,.55);
        P(c,Rgb(170,147,94),InstrumentAxis(39)-1.8,39,3.6,.65);
        for(int i=0;i<6;i++)
        {
            double spread=(i-2.5)*.58;
            c.DrawLine(FrozenPen(Rgb(135-i*7,114-i*5,69-i*3),.13),
                new Point(InstrumentAxis(39)+spread,39),new Point(bridge+spread*1.8,124));
            P(c,Rgb(174,145,84),bridge+spread*1.8,125,.35,.45);
        }
        // Two feet and the cradle anchor the leaning instrument, without a giant oval.
        c.DrawLine(FrozenPen(Rgb(10,16,11),1.3),new Point(24,138),new Point(16,145));
        c.DrawLine(FrozenPen(Rgb(10,16,11),1.3),new Point(30,138),new Point(43,145));
        P(c,Rgb(78,68,40),15,144,5,.5); P(c,Rgb(78,68,40),41,144,5,.5);
    }

    private static double RoadCaseHalf(double y)
    {
        if(y<48) return Math.Sqrt(Math.Max(0,49-Math.Pow(y-48,2)));
        if(y<85) return 7+(y-48)*.025;
        if(y<100) return 8+(y-85)*.35;
        if(y<126) return 13.25;
        return 13.25*Math.Sqrt(Math.Max(0,1-Math.Pow((y-126)/9,2)));
    }

    private static void CraftedCase(DrawingContext c)
    {
        SoftPool(c,Color.FromArgb(200,2,7,4),new Rect(172,129,46,12));
        for(double y=41;y<135;y+=.5)
        {
            double axis=194+(135-y)*.055,half=RoadCaseHalf(y);
            P(c,Rgb(7,16,12),axis-half-1,y,half*2+4,.5);
            P(c,Rgb(45,45,30),axis-half,y,half*2,.5);
            P(c,Rgb(12,22,17),axis-half+1,y,Math.Max(0,half*2-2),.5);
            P(c,Rgb(81,65,34),axis-half,y,.4,.5);
            P(c,Rgb(33,40,27),axis-half+1.8,y,.4,.5);
            for(double x=axis-half+3;x<axis+half-2;x+=1)
                if(SurfaceHash((int)(x*2),(int)(y*2),8)%5==0)
                    P(c,Rgb(23,31,22),x,y,.3,.3);
        }
        // Stitched rim, brass latches and worn paper are surface details on one volume.
        for(double y=48;y<130;y+=1.5)
        {
            double x=194+(135-y)*.055-RoadCaseHalf(y)+1.4;
            P(c,Rgb(69,61,36),x,y,.25,.5);
        }
        for(int i=0;i<3;i++)
        {
            int y=73+i*24;
            P(c,Rgb(65,58,35),207,y,1.4,3); P(c,Rgb(116,94,48),207,y,.4,1.4);
        }
        Poly(c,Rgb(102,85,49),new Point(190,80),new Point(196,81),new Point(195,92),new Point(189,91));
        P(c,Rgb(42,57,43),190,83,4,6); P(c,Rgb(135,114,65),190,90,4,.5);
        Poly(c,Rgb(112,70,38),new Point(194,111),new Point(201,109),new Point(202,114),new Point(195,116));
        for(int i=0;i<6;i++) P(c,Rgb(22,31,21),192+i%3,81+i*1.5,.5,.5);
    }

    private static void NearLeaf(DrawingContext c, Point start, Point end, double halfWidth, int tone)
    {
        double dx=end.X-start.X,dy=end.Y-start.Y,len=Math.Sqrt(dx*dx+dy*dy);
        double nx=-dy/len,ny=dx/len;
        Point At(double t,double w)=>new(start.X+dx*t+nx*w,start.Y+dy*t+ny*w);
        Poly(c,Rgb(14+tone,29+tone,20+tone/2),start,At(.3,halfWidth*.8),
            At(.6,halfWidth),At(.85,halfWidth*.45),end,At(.72,-halfWidth*.7),At(.35,-halfWidth*.65));
        Poly(c,Rgb(24+tone,41+tone,25+tone/2),start,At(.3,halfWidth*.8),At(.6,halfWidth),
            At(.85,halfWidth*.45),end,At(.5,.3));
        c.DrawLine(FrozenPen(Rgb(47+tone,59+tone,31+tone/2),.4),start,end);
        for(int i=2;i<8;i++)
        {
            double t=i/10d;
            c.DrawLine(FrozenPen(Rgb(25+tone,39+tone,24+tone/2),.3),At(t,0),At(t+.13,halfWidth*Math.Sin(t*Math.PI)*.75));
        }
    }

    private static void CraftedNearLeaves(DrawingContext c,double sway)
    {
        // Close leaves enter from outside the lens; no visible small flowerpots in corners.
        for(int branch=0;branch<5;branch++)
        {
            double x=-14+branch*5;
            var start=new Point(-23,251);
            var tip=new Point(x+sway,148+branch*11);
            c.DrawLine(FrozenPen(Rgb(19,32,20),1.4),start,tip);
            for(int leaf=0;leaf<4;leaf++)
            {
                double t=.28+leaf*.2;
                var root=new Point(start.X+(tip.X-start.X)*t,start.Y+(tip.Y-start.Y)*t);
                double direction=leaf%2==0?1:-1;
                NearLeaf(c,root,new Point(root.X+direction*(16+branch*2)+sway,root.Y-12-branch),4+branch*.7,branch*2);
            }
        }
        NearLeaf(c,new Point(218,241),new Point(207-sway,193),7,0);
        NearLeaf(c,new Point(219,234),new Point(193-sway,218),8,2);
    }
}
