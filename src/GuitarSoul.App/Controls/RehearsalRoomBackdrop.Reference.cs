using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace GuitarSoul.App.Controls;

public sealed partial class RehearsalRoomBackdrop
{
    // This image was redrawn from the user's exact selected reference. It is a clean
    // room plate, never a screenshot containing the character, labels or fake controls.
    private static readonly BitmapSource ReferenceRoom = LoadReferenceRoom();
    private readonly Viewport3D _roomViewport = new() { IsHitTestVisible=false, ClipToBounds=true };
    private readonly DrawingVisual _roomOverlay = new();
    private readonly DrawingVisual _windowSky = new();
    private readonly MeshGeometry3D _roomMesh = new();
    private readonly OrthographicCamera _roomCamera = new(new Point3D(0,0,10),new Vector3D(0,0,-1),new Vector3D(0,1,0),1);
    private Point3D[] _roomRestPoints=[];
    private (double Near,double Vine,double Phase)[] _roomMotion=[];
    private Size _roomMeshSize;
    private double _roomMeshTime=double.NaN;
    private Rect _roomImageBounds;
    private const int MeshColumns=80, MeshRows=100;

    private static BitmapSource LoadReferenceRoom()
    {
        var image=new BitmapImage();
        image.BeginInit();
        image.UriSource=new Uri("pack://application:,,,/GuitarSoul;component/Assets/rehearsal-room-window-v1.png");
        image.CacheOption=BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void InitializeReferenceRoom()
    {
        var texture=CreateRoomLightTexture();
        var material=new EmissiveMaterial(texture);
        _roomViewport.Camera=_roomCamera;
        _roomViewport.Children.Add(new ModelVisual3D
        {
            Content=new GeometryModel3D(_roomMesh,material) { BackMaterial=material }
        });
        RenderOptions.SetBitmapScalingMode(_roomViewport,BitmapScalingMode.HighQuality);
        AddVisualChild(_windowSky);
        AddVisualChild(_roomViewport);
        AddVisualChild(_roomOverlay);
    }

    protected override int VisualChildrenCount=>3;
    protected override Visual GetVisualChild(int index)=>index switch
    {
        0=>_windowSky,1=>_roomViewport,2=>_roomOverlay,_=>throw new ArgumentOutOfRangeException(nameof(index))
    };

    protected override Size MeasureOverride(Size availableSize)
    {
        _roomViewport.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width)?0:availableSize.Width,
            double.IsInfinity(availableSize.Height)?0:availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _roomViewport.Arrange(new Rect(finalSize));
        return finalSize;
    }

    internal static Rect ReferenceBounds(double width,double height)
    {
        double scale=Math.Max(width/ReferenceRoom.PixelWidth,height/ReferenceRoom.PixelHeight);
        double w=ReferenceRoom.PixelWidth*scale,h=ReferenceRoom.PixelHeight*scale;
        return new Rect((width-w)/2,(height-h)/2,w,h);
    }

    private void BuildReferenceMesh(double width,double height)
    {
        _roomMeshSize=new Size(width,height);
        _roomImageBounds=ReferenceBounds(width,height);
        _roomCamera.Width=width;
        int count=(MeshColumns+1)*(MeshRows+1);
        _roomRestPoints=new Point3D[count];
        _roomMotion=new (double,double,double)[count];
        var uv=new PointCollection(count);
        var triangles=new Int32Collection(MeshColumns*MeshRows*6);
        for(int row=0;row<=MeshRows;row++)
        for(int col=0;col<=MeshColumns;col++)
        {
            double u=col/(double)MeshColumns,v=row/(double)MeshRows;
            int i=row*(MeshColumns+1)+col;
            _roomRestPoints[i]=new Point3D(_roomImageBounds.X+u*_roomImageBounds.Width-width/2,
                height/2-_roomImageBounds.Y-v*_roomImageBounds.Height,0);
            uv.Add(new Point(u,v));
            // Compact masks affect only painted foliage, never the amp, guitar, art,
            // floor or camera. A continuous mesh avoids moving cut-out rectangles.
            double near=FoliageWeight(u,v,.055,.76,.17,.27);
            double vine=FoliageWeight(u,v,.32,.13,.17,.13)+FoliageWeight(u,v,.95,.10,.08,.13);
            double edge=Math.Clamp(u*24,0,1)*Math.Clamp((1-u)*24,0,1);
            _roomMotion[i]=(near*edge,vine*edge,v*4+u*2);
            if(row<MeshRows&&col<MeshColumns)
            {
                int next=i+MeshColumns+1;
                triangles.Add(i);triangles.Add(next);triangles.Add(i+1);
                triangles.Add(i+1);triangles.Add(next);triangles.Add(next+1);
            }
        }
        uv.Freeze(); triangles.Freeze();
        _roomMesh.TextureCoordinates=uv;
        _roomMesh.TriangleIndices=triangles;
        _roomMeshTime=double.NaN;
    }

    private static double FoliageWeight(double u,double v,double cx,double cy,double rx,double ry)
    {
        double distance=Math.Pow((u-cx)/rx,2)+Math.Pow((v-cy)/ry,2);
        if(distance>=1) return 0;
        double w=1-distance;
        return w*w;
    }

    private void DrawReferenceRoom(double width,double height,double time)
    {
        if(_roomMeshSize!=new Size(width,height)) BuildReferenceMesh(width,height);
        UpdateDayCycle();
        if(_roomMeshTime!=time)
        {
            var positions=new Point3D[_roomRestPoints.Length];
            double density=_roomImageBounds.Width/862;
            for(int i=0;i<positions.Length;i++)
            {
                Point3D p=_roomRestPoints[i];
                var m=_roomMotion[i];
                // Sub-pixel amplitude at desktop scale; fixed roots and distinct slow
                // phases produce a breeze rather than whole-picture oscillation.
                p.X+=density*(m.Near*1.8*Math.Sin(time*.17+m.Phase)+m.Vine*1.1*Math.Sin(time*.21+m.Phase));
                p.Y+=density*m.Near*.35*Math.Sin(time*.13+m.Phase);
                positions[i]=p;
            }
            var points=new Point3DCollection(positions);points.Freeze();
            _roomMesh.Positions=points;
            _roomMeshTime=time;
        }
        using var c=_roomOverlay.RenderOpen();
        // Darken the room in the live composition while preserving the reference art.
        // A frozen fine grain adds a matte finish; it does not blur the focus plane.
        c.DrawRectangle(new SolidColorBrush(Color.FromArgb(47,5,13,10)),null,new Rect(0,0,width,height));
        c.DrawRectangle(EquipmentMaterials.RoomGrain,null,new Rect(0,0,width,height));
        Point At(double u,double v)=>new(_roomImageBounds.X+u*_roomImageBounds.Width,
            _roomImageBounds.Y+v*_roomImageBounds.Height);
        Rect Area(double u,double v,double w,double h)=>new(At(u,v),new Size(w*_roomImageBounds.Width,h*_roomImageBounds.Height));

        if(_catFootY is double foot)
        {
            double pawY=StageTopFor(height,PixelScale)+foot*PixelScale;
            SoftPool(c,Color.FromArgb(155,3,8,5),new Rect(width/2-54,pawY-5,108,18));
        }

        // Keep the reference's exposure and amber/green palette. Only the local lamp
        // breathes; daytime never washes the whole illustration with a flat tint.
        SoftPool(c,Color.FromArgb((byte)(7+4*Math.Sin(time*.37)),241,169,72),Area(.09,.18,.31,.35));
        for(int i=0;i<25;i++)
        {
            double phase=(SurfaceHash(i,6)%1000)/1000d;
            double v=.235+(phase*.31+time*(.00026+i%4*.00006))%.31;
            double u=.13+SurfaceHash(i,8)%160/1000d+Math.Sin(time*.13+i)*.0015;
            var p=At(u,v);
            c.PushOpacity(.10+.24*(.5+.5*Math.Sin(time*.29+i)));
            c.DrawEllipse(Glow,null,p,.45+i%3*.12,.45+i%3*.12);
            c.Pop();
        }

        // Light in the art is part of the room. Playback feedback is an additional,
        // distinctly green meter; it never claims the painted amp pilot is a live input.
        if(IsPlaying)
        {
            int lit=(int)Math.Round(Math.Clamp(AccompanimentEnergy,0,1)*7);
            for(int i=0;i<lit;i++)
            {
                var p=At(.61+i*.009,.419);
                P(c,Signal,p.X,p.Y,2,1);
            }
        }
        if(HasAccompaniment)
        {
            int lit=(int)Math.Round(Math.Clamp(PlaybackProgress,0,1)*30);
            for(int i=0;i<lit;i++)
            {
                var p=At(.61+i*.005,.426);
                c.PushOpacity(.45);P(c,Signal,p.X,p.Y,1,.8);c.Pop();
            }
        }
        if(PlayerActive)
        {
            var p=At(.38,.828);
            c.PushOpacity(.35+.45*Math.Clamp(Level/60,0,1));
            c.DrawEllipse(Signal,null,p,1.4,1);c.Pop();
        }
        double age=time-_rugPulseStarted;
        if(age is >=0 and <1.6)
            SoftPool(c,Color.FromArgb((byte)(25*(1-age/1.6)),228,177,88),Area(.32,.63,.34,.10));

        // Celestial bodies live behind the actual window/tree alpha, never over labels.
    }
}
