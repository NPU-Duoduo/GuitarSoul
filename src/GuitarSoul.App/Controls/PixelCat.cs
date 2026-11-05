using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App.Controls;

/// <summary>Layered pixel character. Audio clock drives attacks; the render clock interpolates poses.</summary>
public sealed class PixelCat : FrameworkElement, IDisposable
{
    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(nameof(Mood), typeof(CompanionMood), typeof(PixelCat),
        new FrameworkPropertyMetadata(CompanionMood.Waiting, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SkinProperty = DependencyProperty.Register(nameof(Skin), typeof(CatSkin), typeof(PixelCat),
        new FrameworkPropertyMetadata(CatSkin.Snow, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(nameof(Level), typeof(double), typeof(PixelCat),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty GuitarProperty = DependencyProperty.Register(nameof(Guitar), typeof(GuitarStyle), typeof(PixelCat),
        new FrameworkPropertyMetadata(GuitarStyle.None, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsJammingProperty = DependencyProperty.Register(nameof(IsJamming), typeof(bool), typeof(PixelCat),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public CompanionMood Mood { get => (CompanionMood)GetValue(MoodProperty); set => SetValue(MoodProperty, value); }
    public CatSkin Skin { get => (CatSkin)GetValue(SkinProperty); set => SetValue(SkinProperty, value); }
    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public GuitarStyle Guitar { get => (GuitarStyle)GetValue(GuitarProperty); set => SetValue(GuitarProperty, value); }
    public bool IsJamming { get => (bool)GetValue(IsJammingProperty); set => SetValue(IsJammingProperty, value); }
    public static readonly DependencyProperty StageProperty = DependencyProperty.Register(nameof(Stage), typeof(StageStyle), typeof(PixelCat),
        new FrameworkPropertyMetadata(StageStyle.Everyday, FrameworkPropertyMetadataOptions.AffectsRender));
    public StageStyle Stage { get => (StageStyle)GetValue(StageProperty); set => SetValue(StageProperty, value); }
    public bool IsListening { get; set; }
    public bool ReducedMotion { get; set; }
    public bool DemoMotion { get; set; }
    public double MotionAmount { get; set; } = .7;
    private readonly DispatcherTimer _timer;
    private RhythmFrame _rhythm = RhythmFrame.Empty;
    private long _capturedAt;
    private double _restBlend;
    private bool _disposed;
    private CompanionPose? _pose;
    // The cat's own clock. Tail speed and body lean come from the director's inner life,
    // not from the music, so an unchanged phrase no longer freezes the whole character.
    private double _tailPhase, _tailAmount = .35, _gaze, _lean;
    private (int Sway, int Bob, int Nod, int Left, int Right) _lastMotion, _settleMotion;
    private (double Stroke, double Lift) _lastGuitarMotion, _settleGuitarMotion;
    internal int PreviewFaceVariant { get; set; }
    private long _lastTick = Stopwatch.GetTimestamp();
    internal double? PreviewSeconds { get; set; }
    internal double? PreviewPulse { get; set; }
    internal double? PreviewOnsetAge { get; set; }
    internal double? PreviewAudioAdvance { get; set; }
    private double AudioAdvance => PreviewAudioAdvance ?? (_capturedAt == 0 ? 100 : Stopwatch.GetElapsedTime(_capturedAt).TotalSeconds);

    public PixelCat()
    {
        SnapsToDevicePixels = UseLayoutRounding = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        AutomationProperties.SetName(this, "吉他伙伴：抬耳倾听、随节奏点头踏爪、安静时蜷睡");
        _timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += Tick;
        Loaded += VisibilityUpdated; Unloaded += VisibilityUpdated;
        IsVisibleChanged += (_, _) => UpdateTimer();
    }
    public void SetRhythm(RhythmFrame rhythm, long capturedAt) { _rhythm = rhythm; _capturedAt = capturedAt; }
    public void SetPose(CompanionPose pose)
    {
        if (pose.Action == CompanionAction.Settle && _pose?.Action != CompanionAction.Settle)
        {
            _settleMotion = _lastMotion;
            _settleGuitarMotion = _lastGuitarMotion;
        }
        IsJamming = pose.Action == CompanionAction.Jam ||
            (pose.Action == CompanionAction.Settle && IsJamming && pose.Weight > .01);
        _pose = pose;
        Mood = pose.Mood;
        _tailAmount = pose.Tail;
        _gaze = pose.Gaze;
        _lean = pose.Lean;
        IsListening = pose.Mood is CompanionMood.Listening or CompanionMood.Excited;
        InvalidateVisual();
    }
    public void ClearPose()
    {
        _pose = null; IsJamming = false;
        _lastMotion = _settleMotion = default;
        _lastGuitarMotion = _settleGuitarMotion = default;
        _gaze = _lean = 0; _tailAmount = .35;
        InvalidateVisual();
    }
    private void Tick(object? sender, EventArgs e)
    {
        var dt = Math.Min(.1, Stopwatch.GetElapsedTime(_lastTick).TotalSeconds);
        _lastTick = Stopwatch.GetTimestamp();
        _restBlend += ((Mood == CompanionMood.Resting ? 1 : 0) - _restBlend) * (1 - Math.Exp(-dt / .18));
        // A resting cat's tail stops; a waking one never holds exactly one speed.
        _tailPhase += dt * (Mood == CompanionMood.Resting ? 0 : .7 + 2.6 * _tailAmount);
        InvalidateVisual();
    }
    private void VisibilityUpdated(object sender, RoutedEventArgs e) => UpdateTimer();
    private void UpdateTimer()
    {
        if (!_disposed && IsLoaded && IsVisible) { _lastTick = Stopwatch.GetTimestamp(); _timer.Start(); }
        else _timer.Stop();
    }
    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsInfinity(availableSize.Width) ? 224 : Math.Min(224, availableSize.Width),
        double.IsInfinity(availableSize.Height) ? 224 : Math.Min(224, availableSize.Height));

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 1 || ActualHeight < 1) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        double pixel = Math.Max(1, Math.Floor(Math.Min(ActualWidth * dpi.DpiScaleX / 40, ActualHeight * dpi.DpiScaleY / 42)));
        double sx = pixel / dpi.DpiScaleX, sy = pixel / dpi.DpiScaleY;
        double ox = Math.Round((ActualWidth - 40 * sx) / 2 * dpi.DpiScaleX) / dpi.DpiScaleX;
        double oy = Math.Round((ActualHeight - 42 * sy) / 2 * dpi.DpiScaleY) / dpi.DpiScaleY;
        dc.PushTransform(new TranslateTransform(ox, oy)); dc.PushTransform(new ScaleTransform(sx, sy));
        var time = PreviewSeconds ?? Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        bool reduced = ReducedMotion || !SystemParameters.ClientAreaAnimation;
        double amount = reduced ? 0 : Math.Clamp(MotionAmount, 0, 1);
        var age = PreviewOnsetAge ?? (_rhythm.AudioSeconds - _rhythm.LastOnsetSeconds +
            AudioAdvance);
        // Hold a detected attack briefly, then ease it out. At the default 0.7 motion
        // amount this keeps the onset above one pixel of movement through the UI's
        // typical dispatch interval, while leaving the beat prediction unchanged.
        double onsetEnvelope = age <= .1 ? 1 : Math.Exp(-(age - .1) / .32);
        double pulse = onsetEnvelope;
        double onsetPulse = pulse;
        bool beatLocked = _rhythm.BeatBpm.HasValue && _rhythm.BeatConfidence >= .6 &&
            _capturedAt != 0 && AudioAdvance < .6 && age < 1.2;
        double beatPosition = 0;
        if (beatLocked)
        {
            double period = 60 / _rhythm.BeatBpm!.Value;
            beatPosition = (_rhythm.AudioSeconds + AudioAdvance -
                _rhythm.BeatAnchorSeconds) / period;
            double phase = beatPosition - Math.Floor(beatPosition);
            pulse = onsetPulse = Math.Exp(-phase * period / .15);
        }
        // Stop prediction after two beats; silence never becomes a permanent metronome.
        if (!beatLocked && _rhythm.TempoBpm is double bpm && _rhythm.Confidence >= .6 && age >= 0 && age < Math.Min(1.5, 120 / bpm))
            pulse = Math.Max(pulse, .65 * _rhythm.Confidence * Math.Exp(-(age % (60 / bpm)) / .13) * Math.Exp(-age / 1.5));
        pulse = PreviewPulse ?? (DemoMotion ? Math.Exp(-(time % .5) / .16) : pulse);
        onsetPulse = PreviewPulse ?? (DemoMotion ? Math.Exp(-(time % .5) / .16) : onsetPulse);
        if (Mood is CompanionMood.Waiting or CompanionMood.Resting) pulse = onsetPulse = 0;
        pulse = Math.Clamp(pulse, 0, 1) * amount;
        onsetPulse = Math.Clamp(onsetPulse, 0, 1) * amount;
        double rest = PreviewSeconds.HasValue || reduced ? (Mood == CompanionMood.Resting ? 1 : 0) : _restBlend;
        var palette = CatPalette.All[Enum.IsDefined(Skin) ? (int)Skin : 0];
        if (rest < .99) { dc.PushOpacity(1 - rest); DrawSeated(dc, palette, time, pulse, amount, onsetPulse); dc.Pop(); }
        if (rest > .01) { dc.PushOpacity(rest); DrawSleeping(dc, palette, time, amount); dc.Pop(); }
        dc.Pop(); dc.Pop();
    }

    private void DrawSeated(DrawingContext dc, CatPalette c, double time, double pulse, double amount, double onsetPulse)
    {
        var ink = CatPalette.Ink;
        var heldGuitar = StageLook.Guitar(Stage, Guitar);
        bool guitar = IsJamming && heldGuitar is GuitarStyle.Stratocaster or GuitarStyle.LesPaul;
        var stage = guitar ? Stage : StageStyle.Everyday;
        double stageVisibility = _pose is { Action: CompanionAction.Settle } settling && amount > 0
            ? Math.Clamp(settling.Weight, 0, 1) : 1;
        bool excited = Mood == CompanionMood.Excited, listening = Mood is CompanionMood.Listening or CompanionMood.Excited;
        int bob = excited ? -(int)Math.Round(pulse) : 0;
        double renderBeat = _rhythm.BeatBpm is double beatBpm && _rhythm.BeatConfidence >= .6 && _capturedAt != 0
            ? (_rhythm.AudioSeconds + AudioAdvance - _rhythm.BeatAnchorSeconds) * beatBpm / 60 : _rhythm.OnsetCount;
        bool leftBeat = (PreviewSeconds.HasValue && !PreviewAudioAdvance.HasValue) || DemoMotion ? (int)(time * 2) % 2 == 0 : (long)Math.Floor(renderBeat) % 2 == 0;
        int sway = excited ? (int)Math.Round(pulse) * (leftBeat ? -1 : 1) : 0;
        int breath = amount > 0 && Math.Sin(time * 1.8) > .75 ? 1 : 0;
        int nod = listening ? -1 + (int)Math.Round(pulse) : 0;
        int lift = excited ? (int)Math.Round(pulse) : 0;
        int leftPaw = leftBeat ? lift : 0, rightPaw = leftBeat ? 0 : lift;
        if (_pose is CompanionPose pose)
        {
            var weight = Math.Clamp(pose.Weight, 0, 1);
            bob = sway = leftPaw = rightPaw = 0;
            nod = listening ? -1 : 0;
            switch (pose.Action)
            {
                case CompanionAction.Notice:
                    nod = -(int)Math.Round(weight * amount);
                    // A short attack can otherwise land between two render ticks and
                    // disappear when the pose's fractional first-pixel lift is rounded.
                    // Keep the onset itself visible while this notice is active; the
                    // motion amount still controls whether a pixel is allowed to move.
                    rightPaw = Math.Max((int)Math.Round(weight * amount),
                        (int)Math.Round(onsetPulse));
                    break;
                case CompanionAction.Groove:
                case CompanionAction.Engage:
                case CompanionAction.Jam:
                    var intensity = pose.HasMusicalEvidence ? .65 + .85 * Math.Clamp(pose.Engagement, 0, 1) : 1;
                    // Eagerness is the cat's own, not the player's. It is why the same phrase
                    // is not tapped with the same force for three minutes. A pose that did not
                    // come from the director has no eagerness, and keeps the old full amount.
                    var eagerness = pose.Eagerness is double eager ? .4 + .8 * Math.Clamp(eager, 0, 1) : 1;
                    var tap = (int)Math.Round(pulse * weight * intensity * eagerness);
                    nod = -1 + tap;
                    if (pose.Action is CompanionAction.Engage or CompanionAction.Jam) { bob = -tap; sway = (leftBeat ? -1 : 1) * tap; }
                    leftPaw = leftBeat ? tap : 0; rightPaw = leftBeat ? 0 : tap;
                    break;
                case CompanionAction.Groom:
                    rightPaw = (int)Math.Round(6 * weight * amount);
                    nod = (int)Math.Round(weight * amount);
                    break;
                case CompanionAction.Settle:
                    sway = (int)Math.Round(_settleMotion.Sway * weight);
                    bob = (int)Math.Round(_settleMotion.Bob * weight);
                    nod = (int)Math.Round(_settleMotion.Nod * weight);
                    leftPaw = (int)Math.Round(_settleMotion.Left * weight);
                    rightPaw = (int)Math.Round(_settleMotion.Right * weight);
                    break;
            }
            // The user's quiet/reduced-motion preference also overrides a gesture already in flight.
            if (amount == 0) { bob = sway = nod = leftPaw = rightPaw = 0; }
            // Weight shift is the one movement the cat keeps while it is watching. It is not a
            // gesture and never competes with a beat, so it rides on top of every seated pose.
            sway += (int)Math.Round(_lean * 2 * amount);
            if (pose.Action is not (CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam)) pulse = 0;
        }
        if (guitar && _pose?.Action != CompanionAction.Settle)
        {
            if (stage is StageStyle.HardRock or StageStyle.JimmyPage) { sway = 0; leftPaw = rightPaw = 0; }
            if (stage is StageStyle.Psychedelic or StageStyle.NileRodgers) { bob = 0; leftPaw = rightPaw = 0; }
            if (stage is StageStyle.Mellow or StageStyle.DavidGilmour or StageStyle.BBKing)
            {
                sway = bob = leftPaw = rightPaw = 0;
                nod = -(int)Math.Round(onsetPulse * (_pose?.Weight ?? 1));
            }
        }
        _lastMotion = (sway, bob, nod, leftPaw, rightPaw);
        int tailY = 25 + (int)Math.Round(Math.Sin(
            PreviewSeconds.HasValue ? time * (.7 + 2.6 * _tailAmount) : _tailPhase) *
            (.45 + 1.7 * _tailAmount) * amount - pulse);
        void R(Brush b, int x, int y, int w, int h) => dc.DrawRectangle(b, null, new Rect(x, y, w, h));
        // Keep the first cat's small body and upright tail, with a softer stepped tip.
        R(ink, 25, 31, 11, 5); R(ink, 33, tailY + 1, 4, 35 - tailY); R(ink, 34, tailY, 2, 2);
        R(c.Fur, 27, 32, 8, 3); R(c.Fur, 34, tailY + 1, 2, 34 - tailY); R(c.Shade, 28, 34, 6, 1);
        if (Skin != CatSkin.Snow) { R(c.Stripe, 34, tailY + 4, 2, 1); R(c.Stripe, 30, 32, 1, 2); }
        dc.PushTransform(new TranslateTransform(sway, bob));
        R(ink, 12, 23, 16, 13); R(ink, 11, 31, 18, 5);
        R(c.Fur, 13, 24, 14, 11); R(c.Fur, 12, 32, 16, 3);
        R(c.Shade, 26, 29, 1, 5); R(c.Shade, 13, 34, 14, 1);
        if (Skin != CatSkin.Snow) { R(c.Bib, 17, 26 + breath, 6, 8 - breath); R(c.Bib, 18, 25 + breath, 4, 2); }
        else R(c.Shade, 16, 26 + breath, 1, 1);
        if (Skin == CatSkin.Calico)
        { R(c.Patch, 13, 28, 3, 5); R(c.Patch, 12, 32, 3, 2); R(c.Stripe, 24, 30, 3, 4); }
        else if (Skin is CatSkin.Ginger or CatSkin.Tabby)
        {
            R(c.Stripe, 13, 29, 3, 1); R(c.Stripe, 12, 32, 3, 1); R(c.Stripe, 24, 30, 3, 1);
        }
        R(c.Shade, 18, 30, 1, 5); R(c.Shade, 21, 30, 1, 5);
        void Paw(int x, int raised)
        {
            R(ink, x + 1, 34 - raised, 7, 4); R(ink, x, 35 - raised, 9, 2);
            R(c.Bib, x + 1, 35 - raised, 7, 2);
            R(c.Shade, x + 3, 36 - raised, 1, 1); R(c.Shade, x + 6, 36 - raised, 1, 1);
        }
        Paw(10, leftPaw); Paw(21, rightPaw);
        if (guitar)
        {
            dc.PushOpacity(stageVisibility);
            StageLook.Body(dc, stage);
            dc.Pop();
        }
        if (guitar)
        {
            double stroke = onsetPulse * 1.8 * (leftBeat ? -1 : 1), neckLift = onsetPulse;
            double visibility = 1;
            if (_pose is CompanionPose guitarPose)
            {
                var weight = Math.Clamp(guitarPose.Weight, 0, 1);
                if (guitarPose.Action == CompanionAction.Settle)
                {
                    stroke = _settleGuitarMotion.Stroke * weight;
                    neckLift = _settleGuitarMotion.Lift * weight;
                    visibility = weight;
                }
                else
                {
                    var intensity = guitarPose.HasMusicalEvidence ? .65 + .85 * Math.Clamp(guitarPose.Engagement, 0, 1) : 1;
                    stroke *= weight * intensity; neckLift *= weight * intensity;
                }
            }
            if (_pose?.Action != CompanionAction.Settle)
            {
                if (stage is StageStyle.Mellow or StageStyle.DavidGilmour or StageStyle.BBKing) { stroke *= .55; neckLift = 0; }
                if (stage == StageStyle.Psychedelic) neckLift *= .7;
                if (stage == StageStyle.NileRodgers) { stroke *= .8; neckLift = 0; }
                if (stage == StageStyle.JimmyPage) neckLift *= .5;
            }
            if (amount == 0) { stroke = neckLift = 0; visibility = 1; }
            _lastGuitarMotion = (stroke, neckLift);
            dc.PushOpacity(visibility);
            PixelGuitar.DrawHeld(dc, c, heldGuitar, stroke, neckLift, stage);
            dc.Pop();
        }
        else _lastGuitarMotion = default;
        dc.PushTransform(new TranslateTransform(0, nod));
        DrawFace(dc, c, time, amount, excited, false, _gaze);
        if (guitar)
        {
            dc.PushOpacity(stageVisibility);
            StageLook.Head(dc, stage);
            dc.Pop();
        }
        dc.Pop(); dc.Pop();
    }

    private void DrawFace(DrawingContext dc, CatPalette c, double time, double amount, bool excited, bool sleeping,
        double gaze = 0)
    {
        var ink = CatPalette.Ink;
        // Candidate-only geometry: the shipped face (variant 0) is unchanged.
        // Variant 1 rounds the ears; variant 2 gives the forehead one more row.
        void R(Brush b, int x, int y, int w, int h)
        {
            if (PreviewFaceVariant == 1 && y < 10) { int bottom = y + h; y = Math.Max(8, y); h = Math.Max(0, bottom - y); }
            if (PreviewFaceVariant == 2 && y <= 15) { if (y + h > 15) h++; y--; }
            if (h > 0) dc.DrawRectangle(b, null, new Rect(x, y, w, h));
        }
        // Original tall face and pointed ears, rounded down through a stepped chin.
        R(ink, 10, 6, 3, 9); R(ink, 11, 5, 2, 3); R(ink, 13, 8, 2, 7); R(ink, 15, 10, 2, 5);
        R(ink, 26, 5, 2, 10); R(ink, 24, 8, 2, 7); R(ink, 22, 10, 2, 5);
        R(ink, 10, 11, 19, 13); R(ink, 9, 14, 21, 9); R(ink, 12, 23, 15, 2); R(ink, 14, 25, 11, 1);
        R(c.Fur, 11, 8, 2, 7); R(c.Fur, 13, 10, 2, 5); R(c.Fur, 26, 7, 1, 8); R(c.Fur, 24, 10, 2, 5);
        R(c.Fur, 11, 12, 17, 11); R(c.Fur, 10, 15, 19, 7); R(c.Fur, 13, 23, 13, 1); R(c.Fur, 14, 24, 11, 1);
        R(c.Shade, 27, 15, 1, 7); R(c.Shade, 14, 24, 11, 1);
        R(CatPalette.Ear, 12, 10, 1, 3); R(CatPalette.Ear, 25, 10, 1, 3);
        if (Skin == CatSkin.Calico)
        {
            // Markings stay above/outside the eyes, so every skin keeps the same gaze.
            R(c.Patch, 11, 13, 5, 3); R(c.Patch, 10, 15, 4, 4); R(c.Patch, 13, 12, 3, 1);
            R(c.Stripe, 25, 13, 3, 3); R(c.Stripe, 26, 15, 3, 4);
        }
        else if (Skin is CatSkin.Ginger or CatSkin.Tabby)
        {
            R(c.Stripe, 16, 13, 1, 2); R(c.Stripe, 19, 12, 1, 3); R(c.Stripe, 22, 13, 1, 2);
            R(c.Stripe, 10, 19, 3, 1); R(c.Stripe, 26, 19, 3, 1);
        }
        if (Skin != CatSkin.Snow) { R(c.Bib, 17, 21, 6, 3); R(c.Bib, 18, 20, 4, 1); }
        bool blink = amount > 0 && time % 7.1 > 6.86;
        // The gaze offset is capped at one pixel: the face is 19 pixels wide, and a two-pixel
        // shift reads as a different head rather than a different look.
        int look = sleeping ? 0 : Math.Clamp((int)Math.Round(gaze * amount * 1.6), -1, 1);
        void Eye(int x)
        {
            x += look;
            if (blink || sleeping) R(ink, x - 1, 19, 3, 1);
            else if (excited) { R(ink, x, 18, 2, 1); R(ink, x - 1, 19, 1, 1); R(ink, x + 2, 19, 1, 1); }
            else { R(ink, x, 17, 2, 3); R(c.Fur, x, 17, 1, 1); }
        }
        Eye(16); Eye(22);
        R(CatPalette.Nose, 19, 21, 2, 1); R(ink, 19, 22, 1, 1);
        R(c.Shade, 12, 21, 2, 1); R(c.Shade, 26, 21, 2, 1);
        R(ink, 7, 20, 3, 1); R(ink, 8, 23, 3, 1); R(ink, 29, 20, 3, 1); R(ink, 28, 23, 3, 1);
    }

    private void DrawSleeping(DrawingContext dc, CatPalette c, double time, double amount)
    {
        int breath = amount > 0 && Math.Sin(time * 1.5) > .55 ? -1 : 0;
        void R(Brush b, int x, int y, int w, int h) => dc.DrawRectangle(b, null, new Rect(x, y, w, h));
        var ink = CatPalette.Ink;
        R(ink, 20, 24 + breath, 10, 13 - breath); R(ink, 17, 27, 17, 10); R(ink, 13, 32, 23, 5);
        R(c.Fur, 21, 25 + breath, 8, 10 - breath); R(c.Fur, 18, 28, 15, 8); R(c.Fur, 14, 33, 20, 3);
        R(c.Shade, 31, 30, 2, 5);
        if (Skin != CatSkin.Snow) { R(Skin == CatSkin.Calico ? c.Patch : c.Stripe, 25, 26 + breath, 2, 4); R(c.Stripe, 30, 29, 2, 4); }
        dc.PushTransform(new TranslateTransform(-3, 9));
        DrawFace(dc, c, time, 0, false, true);
        dc.Pop();
        // Tucked paws and a curved tail wrap the same face used in the seated poses.
        R(ink, 11, 35, 24, 3); R(ink, 33, 32, 3, 4);
        R(c.Fur, 13, 35, 21, 2); R(c.Fur, 34, 32, 1, 4); R(c.Bib, 11, 35, 7, 2);
        if (Skin != CatSkin.Snow) { R(c.Stripe, 26, 35, 1, 2); R(c.Stripe, 31, 35, 1, 2); }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _timer.Stop(); _timer.Tick -= Tick;
        Loaded -= VisibilityUpdated; Unloaded -= VisibilityUpdated;
    }
}
