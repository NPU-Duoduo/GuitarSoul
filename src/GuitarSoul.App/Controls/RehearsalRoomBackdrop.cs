using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using GuitarSoul.Core.Game;
using GuitarSoul.Core.Scenery;

namespace GuitarSoul.App.Controls;

/// <summary>
/// Reference-painted rehearsal room with local foliage deformation and native overlays.
/// The original <see cref="PixelCat"/> remains an independent interactive character.
/// Local-clock illumination is continuous; the authored sun/moon arc is not an ephemeris.
/// </summary>
public sealed partial class RehearsalRoomBackdrop : FrameworkElement
{
    private static readonly DateTimeOffset KnownNewMoon =
        new(2000, 1, 6, 18, 14, 0, TimeSpan.Zero);
    private const double SynodicMonthDays = 29.53058867;

    public static readonly DependencyProperty MomentProperty = VisualProperty<GameMomentState>(
        nameof(Moment), GameMomentState.Ready);
    public static readonly DependencyProperty ReducedMotionProperty = VisualProperty(
        nameof(ReducedMotion), false);
    public static readonly DependencyProperty PlayerActiveProperty = VisualProperty(
        nameof(PlayerActive), false);
    public static readonly DependencyProperty HasAccompanimentProperty = VisualProperty(
        nameof(HasAccompaniment), false);
    public static readonly DependencyProperty LevelProperty = VisualProperty(nameof(Level), 0d);
    public static readonly DependencyProperty PlaybackProgressProperty = VisualProperty(
        nameof(PlaybackProgress), 0d);
    public static readonly DependencyProperty AccompanimentEnergyProperty = VisualProperty(
        nameof(AccompanimentEnergy), 0d);
    public static readonly DependencyProperty NextHighlightProgressProperty = VisualProperty<double?>(
        nameof(NextHighlightProgress), null);
    public static readonly DependencyProperty NextHighlightSecondsProperty = VisualProperty<double?>(
        nameof(NextHighlightSeconds), null);
    public static readonly DependencyProperty IsPlayingProperty = VisualProperty(nameof(IsPlaying), false);
    public static readonly DependencyProperty InputPulseSequenceProperty = DependencyProperty.Register(
        nameof(InputPulseSequence), typeof(long), typeof(RehearsalRoomBackdrop),
        new FrameworkPropertyMetadata(0L, FrameworkPropertyMetadataOptions.AffectsRender, OnSequenceChanged));
    public static readonly DependencyProperty SharedHighlightsProperty = DependencyProperty.Register(
        nameof(SharedHighlights), typeof(int), typeof(RehearsalRoomBackdrop),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender, OnSharedChanged));
    // Integer sampling unit for the native waveform and legacy coordinate conversion.
    public static readonly DependencyProperty PixelScaleProperty = DependencyProperty.Register(
        nameof(PixelScale), typeof(int), typeof(RehearsalRoomBackdrop),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender, OnPixelScaleChanged));

    /// <summary>Raised whenever the integer pixel size changes, so the character can resize with it.</summary>
    public event EventHandler? PixelScaleChanged;

    private static void OnPixelScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        (d as RehearsalRoomBackdrop)?.PixelScaleChanged?.Invoke(d, EventArgs.Empty);

    private readonly DispatcherTimer _motionTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly RoomCycleClock _dayClock = new();
    private DateTimeOffset? _displayLocalTime;
    private DateTimeOffset? _previewLocalTime;
    private double? _previewMoonPhase;
    private double? _previewAnimationSeconds;
    private double _inputPulseStarted = -100;
    private double _rugPulseStarted = -100;
    private int _lastSecond = -1;
    private double? _catFootY;

    public GameMomentState Moment { get => (GameMomentState)GetValue(MomentProperty); set => SetValue(MomentProperty, value); }
    public bool ReducedMotion { get => (bool)GetValue(ReducedMotionProperty); set => SetValue(ReducedMotionProperty, value); }
    public bool PlayerActive { get => (bool)GetValue(PlayerActiveProperty); set => SetValue(PlayerActiveProperty, value); }
    public bool HasAccompaniment { get => (bool)GetValue(HasAccompanimentProperty); set => SetValue(HasAccompanimentProperty, value); }
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public double PlaybackProgress { get => (double)GetValue(PlaybackProgressProperty); set => SetValue(PlaybackProgressProperty, value); }
    public double AccompanimentEnergy { get => (double)GetValue(AccompanimentEnergyProperty); set => SetValue(AccompanimentEnergyProperty, value); }
    public double? NextHighlightProgress { get => (double?)GetValue(NextHighlightProgressProperty); set => SetValue(NextHighlightProgressProperty, value); }
    public double? NextHighlightSeconds { get => (double?)GetValue(NextHighlightSecondsProperty); set => SetValue(NextHighlightSecondsProperty, value); }
    public bool IsPlaying { get => (bool)GetValue(IsPlayingProperty); set => SetValue(IsPlayingProperty, value); }
    public long InputPulseSequence { get => (long)GetValue(InputPulseSequenceProperty); set => SetValue(InputPulseSequenceProperty, value); }
    public int SharedHighlights { get => (int)GetValue(SharedHighlightsProperty); set => SetValue(SharedHighlightsProperty, value); }
    public int PixelScale { get => (int)GetValue(PixelScaleProperty); private set => SetValue(PixelScaleProperty, value); }

    public DateTimeOffset? PreviewLocalTime
    {
        get => _previewLocalTime;
        set { _previewLocalTime = value; InvalidateVisual(); }
    }

    public double? PreviewMoonPhase
    {
        get => _previewMoonPhase;
        set { _previewMoonPhase = value; _lastLightHour = double.NaN; InvalidateVisual(); }
    }

    internal double? PreviewAnimationSeconds
    {
        get=>_previewAnimationSeconds;
        set { _previewAnimationSeconds=value; InvalidateVisual(); }
    }

    public string Description
    {
        get
        {
            var local = CurrentLocalTime;
            double hour = local.TimeOfDay.TotalHours;
            return hour is >= 5 and < 19
                ? $"本地时间 {local:HH:mm} · {DayPeriod(hour)}"
                : $"本地时间 {local:HH:mm} · {MoonPhaseName(CurrentMoonPhase)}";
        }
    }

    private DateTimeOffset CurrentLocalTime => PreviewLocalTime ?? _displayLocalTime ?? DateTimeOffset.Now;
    private double CurrentMoonPhase => PreviewMoonPhase is double preview
        ? Normalize(preview)
        : MoonPhase(CurrentLocalTime.ToUniversalTime());

    public RehearsalRoomBackdrop()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        AutomationProperties.SetName(this,
            "随本地时间变化的像素排练室；灯、浮尘与前景按三层景深运动，设备灯只显示真实伴奏与玩家输入");
        InitializeReferenceRoom();
        _motionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Background,
            (_, _) => TickMotion(), Dispatcher);
        Loaded += (_, _) => _motionTimer.Start();
        Unloaded += (_, _) => _motionTimer.Stop();
    }

    /// <summary>
    /// Vertical offset of the stage inside a viewport of the given height. The stage is
    /// centred, but never climbs so high that the brand block on the wall has no room:
    /// below about 92 px the window frame runs into the wordmark.
    /// </summary>
    public static double StageTopFor(double sceneHeight, int scale) =>
        Math.Max(Math.Round((sceneHeight - StageHeight * scale) / 2), 92);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        // Keep native signal marks on an integer grid; character placement is derived
        // from the reference image bounds by MainWindow.SyncSceneScale.
        int scale = (int)Math.Floor(Math.Min(sizeInfo.NewSize.Width / StageWidth,
            sizeInfo.NewSize.Height / StageHeight));
        PixelScale = Math.Clamp(scale, 2, 9);
    }

    /// <summary>
    /// Tells the room which row the character's paws rest on, measured in room pixels from
    /// the bottom of the stage. The contact shadow has to follow the real layout rather than
    /// a guessed row, or it lands behind the character's head.
    /// </summary>
    public void SetCharacterFoot(double unitsFromStageBottom)
    {
        double stage = StageHeight - unitsFromStageBottom;
        if (_catFootY is double previous && Math.Abs(previous - stage) < .01) return;
        _catFootY = stage;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 80 || ActualHeight < 80) return;
        if (PreviewLocalTime is null)
            _displayLocalTime = _dayClock.Advance(DateTimeOffset.Now, _clock.Elapsed.TotalSeconds);
        double time = ReducedMotion ? 0 : PreviewAnimationSeconds ?? _clock.Elapsed.TotalSeconds;
        DrawReferenceRoom(ActualWidth, ActualHeight, time);
    }

    private void TickMotion()
    {
        if (!IsVisible) return;
        // Colour/light still follows time under reduced motion; foliage and dust freeze.
        if (PreviewLocalTime is null)
            _displayLocalTime = _dayClock.Advance(DateTimeOffset.Now, _clock.Elapsed.TotalSeconds);
        int second = CurrentLocalTime.Second;
        bool secondChanged = second != _lastSecond;
        _lastSecond = second;
        if (!ReducedMotion || secondChanged) InvalidateVisual();
    }

    private static DependencyProperty VisualProperty(string name, bool value) =>
        DependencyProperty.Register(name, typeof(bool), typeof(RehearsalRoomBackdrop),
            new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty VisualProperty(string name, double value) =>
        DependencyProperty.Register(name, typeof(double), typeof(RehearsalRoomBackdrop),
            new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));

    private static DependencyProperty VisualProperty<T>(string name, T value) =>
        DependencyProperty.Register(name, typeof(T), typeof(RehearsalRoomBackdrop),
            new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender));

    private static void OnSequenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RehearsalRoomBackdrop room && (long)e.NewValue > (long)e.OldValue)
            room._inputPulseStarted = room._clock.Elapsed.TotalSeconds;
    }

    private static void OnSharedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RehearsalRoomBackdrop room && (int)e.NewValue > (int)e.OldValue)
            room._rugPulseStarted = room._clock.Elapsed.TotalSeconds;
    }

    private static double MoonPhase(DateTimeOffset utc) =>
        Normalize((utc - KnownNewMoon).TotalDays / SynodicMonthDays);

    private static double Normalize(double value) => value - Math.Floor(value);

    private static string DayPeriod(double hour) => hour < 8 ? "清晨"
        : hour < 16 ? "日光"
        : hour < 19 ? "黄昏"
        : "夜";

    private static string MoonPhaseName(double phase) => phase switch
    {
        < .035 or >= .965 => "新月",
        < .215 => "娥眉月",
        < .285 => "上弦月",
        < .465 => "盈凸月",
        < .535 => "满月",
        < .715 => "亏凸月",
        < .785 => "下弦月",
        _ => "残月"
    };
}
