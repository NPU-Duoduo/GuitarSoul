using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GuitarSoul.App.Controls;

/// <summary>A real Slider range with amp-style rotary presentation and native range automation.</summary>
public sealed class RotaryKnob : Slider
{
    private Point _dragStart;
    private double _dragValue;

    public RotaryKnob()
    {
        Template = new ControlTemplate(typeof(Slider));
        Focusable = true;
        Cursor = Cursors.SizeNS;
        MinWidth = 62;
        MinHeight = 61;
        SmallChange = .02;
        LargeChange = .1;
        AutomationProperties.SetHelpText(this,"拖动旋钮上下调节；也可使用方向键、Home、End 和鼠标滚轮。");
    }

    protected override void OnValueChanged(double oldValue,double newValue)
    {
        base.OnValueChanged(oldValue,newValue);
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if(!IsEnabled) return;
        Focus();
        _dragStart=e.GetPosition(this);
        _dragValue=Value;
        CaptureMouse();
        e.Handled=true;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(!IsMouseCaptured) return;
        var point=e.GetPosition(this);
        SetCurrentValue(ValueProperty,Math.Clamp(_dragValue+(_dragStart.Y-point.Y+point.X-_dragStart.X)/130*(Maximum-Minimum),Minimum,Maximum));
        e.Handled=true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if(IsMouseCaptured) ReleaseMouseCapture();
        e.Handled=true;
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if(!IsKeyboardFocusWithin) return;
        SetCurrentValue(ValueProperty,Math.Clamp(Value+Math.Sign(e.Delta)*SmallChange,Minimum,Maximum));
        e.Handled=true;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    { base.OnGotKeyboardFocus(e); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    { base.OnLostKeyboardFocus(e); InvalidateVisual(); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if(ActualWidth<10||ActualHeight<10) return;
        double radius=Math.Min(24,Math.Min((ActualWidth-16)/2,(ActualHeight-18)/2));
        var centre=new Point(ActualWidth/2,radius+8);
        var ink=Foreground;
        var rim=new Pen(ink,1); rim.Freeze();
        for(int i=0;i<=10;i++)
        {
            double a=(-135+i*27)*Math.PI/180;
            var a0=new Point(centre.X+Math.Sin(a)*(radius+4),centre.Y-Math.Cos(a)*(radius+4));
            var a1=new Point(centre.X+Math.Sin(a)*(radius+6),centre.Y-Math.Cos(a)*(radius+6));
            dc.DrawLine(rim,a0,a1);
        }
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45,0,0,0)),null,new Point(centre.X,centre.Y+2),radius+1,radius+1);
        var metal=new LinearGradientBrush(Color.FromRgb(189,187,161),Color.FromRgb(59,65,53),115);
        dc.DrawEllipse(metal,new Pen(new SolidColorBrush(Color.FromRgb(17,24,17)),1),centre,radius,radius);
        for(int i=0;i<64;i++)
        {
            double a=i*Math.PI/32;
            var p=new Point(centre.X+Math.Cos(a)*(radius-1),centre.Y+Math.Sin(a)*(radius-1));
            var q=new Point(centre.X+Math.Cos(a)*(radius-3.5),centre.Y+Math.Sin(a)*(radius-3.5));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(41,49,37)),.65),p,q);
        }
        var cap=new RadialGradientBrush(Color.FromRgb(63,71,55),Color.FromRgb(20,28,22))
        { GradientOrigin=new Point(.34,.23),Center=new Point(.45,.4),RadiusX=.7,RadiusY=.7 };
        dc.DrawEllipse(cap,new Pen(new SolidColorBrush(Color.FromRgb(109,118,94)),.8),centre,radius-5,radius-5);
        dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromArgb(65,9,17,10)),1),centre,radius-8,radius-8);
        double fraction=Maximum>Minimum?Math.Clamp((Value-Minimum)/(Maximum-Minimum),0,1):0;
        double angle=(-135+fraction*270)*Math.PI/180;
        var from=new Point(centre.X+Math.Sin(angle)*5,centre.Y-Math.Cos(angle)*5);
        var to=new Point(centre.X+Math.Sin(angle)*(radius-6),centre.Y-Math.Cos(angle)*(radius-6));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(238,218,161)),2.2),from,to);
        if(IsKeyboardFocusWithin) dc.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromRgb(180,116,46)),2),centre,radius+8,radius+8);
        var text=new FormattedText($"{fraction:P0}",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,
            new Typeface("Consolas"),9,ink,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text,new Point((ActualWidth-text.Width)/2,ActualHeight-11));
    }
}
