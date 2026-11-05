using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;

namespace GuitarSoul.App.Controls;

/// <summary>Analog presentation of existing peak dBFS, not a calibrated VU instrument.</summary>
public sealed class RackVuMeter : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty=DependencyProperty.Register(nameof(Level),typeof(double),typeof(RackVuMeter),
        new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender,OnLevel,CoerceLevel));
    public static readonly DependencyProperty ReducedMotionProperty=DependencyProperty.Register(nameof(ReducedMotion),typeof(bool),typeof(RackVuMeter),
        new FrameworkPropertyMetadata(false,OnMotion));
    public static readonly DependencyProperty IsClippingProperty=DependencyProperty.Register(nameof(IsClipping),typeof(bool),typeof(RackVuMeter),
        new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    private readonly DispatcherTimer _timer;
    private long _lastTick;
    private double _shown,_held,_holdRemaining;
    public double Level { get=>(double)GetValue(LevelProperty); set=>SetValue(LevelProperty,value); }
    public bool ReducedMotion { get=>(bool)GetValue(ReducedMotionProperty); set=>SetValue(ReducedMotionProperty,value); }
    public bool IsClipping { get=>(bool)GetValue(IsClippingProperty); set=>SetValue(IsClippingProperty,value); }
    internal double DisplayedLevel=>_shown;
    internal double HeldLevel=>_held;

    public RackVuMeter()
    {
        MinWidth=190;MinHeight=104;
        AutomationProperties.SetName(this,"输入峰值电平表，单位 dBFS");
        AutomationProperties.SetHelpText(this,"指针为平滑输入峰值，短线保留峰值；CLIP 表示检测到连续触顶平顶，提示可能削波。悬停查看电平详情。");
        _timer=new DispatcherTimer(TimeSpan.FromMilliseconds(33),DispatcherPriority.Render,(_,_)=>Tick(),Dispatcher);
        _timer.Stop();
        Loaded+=(_,_)=>StartMotion();Unloaded+=(_,_)=>_timer.Stop();
        IsVisibleChanged+=(_,_)=>{ if(IsVisible) StartMotion();else _timer.Stop(); };
    }
    private static object CoerceLevel(DependencyObject d,object v)=>v is double x&&double.IsFinite(x)?Math.Clamp(x,0,60):0d;
    private static void OnLevel(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var m=(RackVuMeter)d;
        if(m.Level>=m._held){m._held=m.Level;m._holdRemaining=1.1;}
        if(m.ReducedMotion||!m.IsLoaded){m._shown=m.Level;m._held=m.Level;}
        AutomationProperties.SetItemStatus(m,m.Level<=0?"无输入":$"峰值 {m.Level-60:0.0} dBFS");
        m.StartMotion();
    }
    private static void OnMotion(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {
        var m=(RackVuMeter)d;
        if(m.ReducedMotion){m._timer.Stop();m._shown=m.Level;m._held=m.Level;m.InvalidateVisual();}
        else m.StartMotion();
    }
    private void StartMotion()
    {
        if(!IsLoaded||!IsVisible||ReducedMotion||_timer.IsEnabled) return;
        _lastTick=Stopwatch.GetTimestamp();_timer.Start();
    }
    private void Tick()
    {
        double dt=Math.Clamp(Stopwatch.GetElapsedTime(_lastTick).TotalSeconds,0,.1);
        _lastTick=Stopwatch.GetTimestamp();Advance(dt,Level);InvalidateVisual();
        if(Math.Abs(_shown-Level)<.03&&_held<=Level+.03){_shown=Level;_held=Level;_timer.Stop();}
    }
    internal void Advance(double dt,double target)
    {
        target=double.IsFinite(target)?Math.Clamp(target,0,60):0;
        if(ReducedMotion){_shown=target;_held=target;return;}
        dt=Math.Clamp(dt,0,.1);
        _shown+=(target-_shown)*(1-Math.Exp(-dt/(target>_shown?.085:.38)));
        _holdRemaining=Math.Max(0,_holdRemaining-dt);
        if(_holdRemaining<=0) _held=Math.Max(target,_held-18*dt);
    }
    public void Reset()
    {
        _timer.Stop();_shown=_held=_holdRemaining=0;IsClipping=false;InvalidateVisual();
        ToolTip="尚未接收声音；开始倾听后可查看峰值、平均电平与触顶比例。";
    }
    private static Brush B(byte r,byte g,byte b)=>new SolidColorBrush(Color.FromRgb(r,g,b));
    private static readonly Brush FaceGrain = CreateFaceGrain();
    private static Brush CreateFaceGrain()
    {
        var drawing = new DrawingGroup();
        using (var c = drawing.Open())
        {
            var random = new Random(71);
            for (int i=0;i<700;i++)
                c.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)random.Next(3,12),76,55,25)),null,
                    new Rect(random.NextDouble()*320,random.NextDouble()*190,.45,.45));
        }
        drawing.Freeze();
        var brush = new DrawingBrush(drawing) { ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,320,190),Stretch=Stretch.Fill };
        brush.Freeze();return brush;
    }
    protected override void OnRender(DrawingContext c)
    {
        if(ActualWidth<40||ActualHeight<30)return;
        double scaleFactor=Math.Min(ActualWidth/320,ActualHeight/190);
        c.PushTransform(new TranslateTransform((ActualWidth-320*scaleFactor)/2,(ActualHeight-190*scaleFactor)/2));
        c.PushTransform(new ScaleTransform(scaleFactor,scaleFactor));
        // Separate cast shell, bevel, recessed gasket and glass. Fixed lighting never follows audio.
        c.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(95,0,0,0)),null,new Rect(1,4,318,185),10,10);
        c.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(97,100,87),Color.FromRgb(16,23,20),90),
            new Pen(B(6,11,9),1.2),new Rect(1,1,318,184),9,9);
        c.DrawRoundedRectangle(null,new Pen(B(125,122,102),.65),new Rect(3,2.5,314,180),7,7);
        c.DrawRoundedRectangle(B(12,18,14),new Pen(B(3,8,6),1.4),new Rect(10,9,300,156),6,6);
        c.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(69,60,40),Color.FromRgb(137,119,78),90),null,new Rect(12,11,296,152),5,5);
        var face=new Rect(16,15,288,144);
        var paper=new LinearGradientBrush();
        paper.StartPoint=new Point(0,0);paper.EndPoint=new Point(0,1);
        paper.GradientStops.Add(new GradientStop(Color.FromRgb(166,144,94),0));
        paper.GradientStops.Add(new GradientStop(Color.FromRgb(229,211,164),.33));
        paper.GradientStops.Add(new GradientStop(Color.FromRgb(231,212,163),.7));
        paper.GradientStops.Add(new GradientStop(Color.FromRgb(192,165,111),1));
        c.DrawRoundedRectangle(paper,null,face,3,3);
        c.PushClip(new RectangleGeometry(face,3,3));
        c.DrawRectangle(FaceGrain,null,face);
        c.DrawRectangle(new LinearGradientBrush(Color.FromArgb(70,25,24,14),Color.FromArgb(0,25,24,14),90),null,new Rect(16,15,288,13));
        var pivot=new Point(160,147);
        Point Arc(double level,double rx=123,double ry=101)
        {
            double a=(-63+126*Math.Pow(Math.Clamp(level/60,0,1),1.65))*Math.PI/180;
            return new Point(pivot.X+Math.Sin(a)*rx,pivot.Y-Math.Cos(a)*ry);
        }
        void ScaleArc(int from,int to,Pen pen,double rx=123,double ry=101)
        {
            var path=new StreamGeometry();using(var g=path.Open())
            {
                g.BeginFigure(Arc(from,rx,ry),false,false);
                for(int i=from+1;i<=to;i++)g.LineTo(Arc(i,rx,ry),true,false);
            }
            c.DrawGeometry(null,pen,path);
        }
        ScaleArc(0,60,new Pen(B(70,62,42),.7));
        ScaleArc(54,60,new Pen(B(143,56,34),2),126,104);
        for(int db=-60;db<=0;db+=2)
        {
            bool major=db is -60 or -40 or -20 or -10 or 0;
            c.DrawLine(new Pen(db>=-6?B(130,45,29):B(44,44,31),major?1.1:.6),
                Arc(db+60),Arc(db+60,major?112:117,major?91:96));
        }
        foreach(int db in new[]{-60,-40,-20,-10,-5,0})
        {
            var p=Arc(db+60,132,114);
            Text(c,db.ToString(CultureInfo.InvariantCulture),p.X,p.Y-5,9.5,db>=-5?B(102,33,23):B(32,34,24),true);
        }
        Text(c,"INPUT",160,107,12,B(51,49,33),true);
        Text(c,"PEAK · dBFS",160,123,8,B(78,69,45),true);
        if(_held>0)c.DrawLine(new Pen(B(145,65,35),1.6),Arc(_held,126,105),Arc(_held,131,111));
        var tip=Arc(_shown,115,94);
        var needle=new StreamGeometry();using(var g=needle.Open())
        {
            double dx=tip.X-pivot.X,dy=tip.Y-pivot.Y,len=Math.Sqrt(dx*dx+dy*dy);
            g.BeginFigure(new Point(pivot.X-dy/len*1.5,pivot.Y+dx/len*1.5),true,true);
            g.LineTo(tip,true,false);g.LineTo(new Point(pivot.X+dy/len*1.5,pivot.Y-dx/len*1.5),true,false);
        }
        c.PushTransform(new TranslateTransform(1.6,2));
        c.DrawGeometry(new SolidColorBrush(Color.FromArgb(65,34,20,7)),null,needle);c.Pop();
        c.DrawGeometry(B(138,41,26),null,needle);
        c.DrawEllipse(new LinearGradientBrush(Color.FromRgb(145,138,109),Color.FromRgb(37,40,29),110),new Pen(B(41,43,29),.7),pivot,7,7);
        c.DrawEllipse(B(43,47,33),new Pen(B(176,160,116),.7),pivot,3.7,3.7);
        // A faint diagonal reflection stays away from the scale and readout.
        var reflection=new StreamGeometry();using(var g=reflection.Open())
        {
            g.BeginFigure(new Point(16,15),true,true);g.LineTo(new Point(114,15),true,false);
            g.LineTo(new Point(36,159),true,false);g.LineTo(new Point(16,159),true,false);
        }
        c.DrawGeometry(new SolidColorBrush(Color.FromArgb(13,255,250,223)),null,reflection);
        c.Pop();
        c.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(100,248,227,174)),.7),new Rect(15.5,14.5,289,145),3,3);
        Text(c,"PEAK",48,171,8,B(181,182,159),true);
        Text(c,Level<=0?"— dBFS":$"{Level-60:0.0} dBFS",160,169,11,B(233,216,167),true);
        c.DrawEllipse(IsClipping?B(227,90,55):B(56,48,34),new Pen(B(5,12,8),1),new Point(281,174),3.3,3.3);
        Text(c,"CLIP",259,170,8,B(181,182,159),true);
        EquipmentSurface.DrawScrew(c,new Point(6,92));EquipmentSurface.DrawScrew(c,new Point(314,92));
        c.Pop();c.Pop();
    }
    private void Text(DrawingContext c,string value,double x,double y,double size,Brush ink,bool centered)
    {
        var text=new FormattedText(value,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
            new Typeface("Consolas"),size,ink,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        c.DrawText(text,new Point(centered?x-text.Width/2:x,y));
    }
}
