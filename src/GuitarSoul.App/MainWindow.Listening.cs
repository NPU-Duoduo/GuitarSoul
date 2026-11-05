using System.IO;
using System.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private ListeningFeatures RefreshListeningFeedback(bool fresh, bool active, double elapsed, bool soundActive)
    {
        bool eligible = fresh && _calibration is null && !IsKeepsakeReviewPlaying;
        if (!eligible)
        {
            _voice.Reset();
            PresentListeningReaction(null, ListeningFeatures.Empty);
            return ListeningFeatures.Empty;
        }
        var features = active ? _latestRhythm.Listening : ListeningFeatures.Empty;
        // An invitation owns the desktop bubble; do not spend an unseen sentence's display or cooldown.
        bool speechSuppressed = _petPreferences.QuietMode || MemoryInvitation.Visibility == Visibility.Visible;
        // Motion can bridge the beat; an observation only accumulates evidence during actual sound.
        var fallback = _voice.Step(features, soundActive, speechSuppressed,
            _petPreferences.Expression, elapsed);
        PresentListeningReaction(fallback, _voice.Source);
        return features;
    }

    private void SetListeningReaction(string? text)
    {
        if (SelectedSourceKind == GuitarSoul.Audio.Windows.AudioSourceKind.ComputerPlayback)
            text = text?.Replace("琴声", "声音").Replace("拨弦", "起音");
        PetReaction.Text = text ?? "";
        ScenePetReaction.Text = text ?? "";
        _petWindow.SetReaction(text);
    }

    private static string DescribeListening(ListeningFeatures features)
    {
        var tone = features.Tone switch
        {
            ToneColor.Warm => "温厚的声音",
            ToneColor.Bright => "明亮的声音",
            _ => "声音"
        };
        var expression = features.Expression switch
        {
            PlayingExpression.Sustained => "长音舒展开来，它陪着余音慢慢听",
            PlayingExpression.Flowing => "起音接连流动，它轻轻跟着",
            PlayingExpression.Animated => "起音变得密集，它也想动一动",
            _ => "它在留意这段声音的变化"
        };
        if (features.Dynamics == DynamicContour.Building) expression = "力度渐渐展开，它抬耳听着";
        else if (features.Dynamics == DynamicContour.Easing) expression = "力度轻轻收住，它也慢下来";
        return $"{tone} · {expression}。";
    }

    // Same feature-to-presentation path as live input, with synthetic samples only.
    private void RunListeningUiChecks(string reportPath, Action<bool, string> check)
    {
        // Verify the one measured waveform shown beside the cat.
        UpdateLayout();
        byte[] WavePixels()
        {
            SceneWave.InvalidateVisual();
            SceneWave.UpdateLayout();
            SceneWave.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, () => { });
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)SceneWave.ActualWidth,
                (int)SceneWave.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(SceneWave);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }
        SceneWave.Clear();
        var waveSamples = Enumerable.Range(0, 9600).Select(i => (float)(.3 * Math.Sin(i * .1) *
            Math.Exp(-(i % 1600) / 400d))).ToArray();
        SceneWave.Push(waveSamples); _ = WavePixels();
        check(SceneWave.HasMeasuredSignal, "Measured sound reaches the rendered waveform state");
        MusicCueStatus.Text = "合成声音示例";
        BeatStatus.Text = "真实振幅包络 · 本次未打开设备或播放声音";
        RenderPreview(Path.ChangeExtension(reportPath, ".wave-active.png"));
        SceneWave.Clear();
        _ = WavePixels();
        check(!SceneWave.HasMeasuredSignal, "Stopping the wave clears all measured history");
        var tracker = new RhythmTracker(8000);
        var samples = Enumerable.Range(0, 80000)
            .Select(i => (float)(.15 * Math.Sin(2 * Math.PI * 196 * i / 8000))).ToArray();
        _latestRhythm = tracker.Process(samples);
        _voice.Reset();
        for (int tick = 0; tick < 150 && string.IsNullOrEmpty(PetReaction.Text); tick++)
            RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text.Length > 0 && _petWindow.DisplayedReaction == PetReaction.Text && !_capture.IsCapturing,
            "Synthetic sustained tone reaches matching panel and desktop responses without capture");
        check(ScenePetReaction.Text == PetReaction.Text,
            "The listening scene mirrors the cat response shown in the companion rail");
        var reaction = PetReaction.Text;
        var stem = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            "listening-" + typeof(App).Assembly.GetName().Version?.ToString(3));
        RhythmStatus.Text = DescribeListening(_latestRhythm.Listening);
        PetStatus.Text = "它在听这段长音";
        StatusMessage.Text = "合成温厚长音的回应示例 · 没有开启设备、播放声音或录音。";
        CatPreview.SetPose(new(CompanionAction.Listen, CompanionMood.Listening, 0));
        _petWindow.SetPose(new(CompanionAction.Listen, CompanionMood.Listening, 0));
        CatPreview.PreviewSeconds = 1; CatPreview.PreviewPulse = 0;
        _petWindow.SetPreview(1, 0);
        RenderPreview(stem + ".png");
        _petWindow.RenderInvitationPreview(stem + ".pet.png");
        _petWindow.SetInvitation("想再听一听旧记忆");
        check(_petWindow.DisplayedReaction == "想再听一听旧记忆", "Memory invitation takes priority over listening response");
        _petWindow.SetInvitation(null);
        check(_petWindow.DisplayedReaction == reaction, "Listening response survives invitation presentation changes");
        _voice.Reset();
        MemoryInvitation.Visibility = Visibility.Visible;
        _petWindow.SetInvitation("想再听一听旧记忆");
        for (int tick = 0; tick < 240; tick++) RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text == "" && _voice.Current is null && _voice.Source == ListeningFeatures.Empty &&
            _petWindow.DisplayedReaction == "想再听一听旧记忆",
            "Invitation priority prevents an unseen sentence from consuming its display and cooldown");
        MemoryInvitation.Visibility = Visibility.Collapsed;
        _petWindow.SetInvitation(null);
        for (int tick = 0; tick < 150 && string.IsNullOrEmpty(PetReaction.Text); tick++)
            RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text.Length > 0 && _petWindow.DisplayedReaction == PetReaction.Text,
            "A stable observation can speak after invitation dismissal without an unseen cooldown");
        RefreshListeningFeedback(false, true, .05, true);
        check(PetReaction.Text == "" && _petWindow.DisplayedReaction is null,
            "Stale audio clears panel and desktop listening responses");
        RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text == "", "A returning callback must accumulate new evidence after a stale input");
        _voice.Reset();
        for (int tick = 0; tick < 240; tick++) RefreshListeningFeedback(true, true, .05, false);
        check(PetReaction.Text == "" && _voice.Source == ListeningFeatures.Empty,
            "Motion bridging cannot count silence as speech confirmation");
        RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text == "", "Actual sound must accumulate its own evidence after a motion bridge");
        _voice.Reset();
        _calibration = new CalibrationAccumulator(8000);
        for (int tick = 0; tick < 180; tick++) RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text == "" && _petWindow.DisplayedReaction is null, "Calibration cannot produce a listening response");
        _calibration = null;
        _voice.Reset();
        QuietMode.IsChecked = true;
        for (int tick = 0; tick < 180; tick++) RefreshListeningFeedback(true, true, .05, true);
        check(PetReaction.Text == "" && _petWindow.DisplayedReaction is null, "Quiet mode suppresses spontaneous words as well as motion");
        QuietMode.IsChecked = false;
        CatPreview.PreviewSeconds = CatPreview.PreviewPulse = null;
        _petWindow.SetPreview(null, null);
        StopListening("听觉回应检查完成；未开启采集。");
        check(PetReaction.Text == "" && _petWindow.DisplayedReaction is null && !_capture.IsCapturing,
            "Stopping clears all listening feedback and leaves capture off");
    }
}
