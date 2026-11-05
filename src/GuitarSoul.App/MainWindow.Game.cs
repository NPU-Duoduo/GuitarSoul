using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Game;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private readonly TrackArcAnalyzer _trackArcAnalyzer = new();
    private CancellationTokenSource? _trackAnalysisCancellation;
    private TrackArc? _gameTrackArc;
    private GameMomentDirector? _gameDirector;
    private PlayerParticipationTracker? _gamePlayer;
    private PlaybackRunState _gamePlaybackState = PlaybackRunState.Ready;
    private string? _gameTrackPath;
    private long _gameEpoch = 1;
    private long _gameSequence;
    private int _gameInputSampleRate;
    private bool _performanceTogetherMode = true;
    private bool _gameAnalysisPending;
    private bool _gameSessionNeedsReload;
    private string? _gameAnalysisNote;

    private void InitializePerformanceExperience()
    {
        TogetherModeButton.IsChecked = true;
        SetPerformanceMode(together: true);
        RefreshCelestialStatus();
    }

    private void TogetherMode_Click(object sender, RoutedEventArgs e) => SetPerformanceMode(together: true);
    private void FreeListenMode_Click(object sender, RoutedEventArgs e) => SetPerformanceMode(together: false);

    private void SetPerformanceMode(bool together)
    {
        _performanceTogetherMode = together;
        if (TrackDock is null) return;
        TogetherModeButton.IsChecked = together;
        FreeListenModeButton.IsChecked = !together;
        TrackDock.Visibility = Visibility.Visible;
        TrackArcStatus.Visibility = Visibility.Collapsed;
        PerformanceModeHint.Text = together
            ? "本地伴奏与独立玩家输入共同推动局内状态"
            : "只听当前声音，保留原来的陪伴与回应";
        if (!together)
        {
            PlayerEntryLabel.Text = _capture.IsCapturing ? "正在接收你选择的声音" : "连接一个声音来源";
            PlayerEntryDot.Background = BrushOf(95, 126, 108);
            RoomBackdrop.Moment = GameMomentState.Ready;
            RoomBackdrop.SharedHighlights = 0;
            SetTracePresence(playerActive: false, hasAccompaniment: false);
            RefreshRoomScene();
        }
        else
        {
            CatPreview.IsJamming = false;
            _petWindow.SetJamming(false);
            RefreshPerformanceExperience();
        }
    }

    private async void BeginTrackArcAnalysis(string path)
    {
        _trackAnalysisCancellation?.Cancel();
        _trackAnalysisCancellation?.Dispose();
        var cancellation = _trackAnalysisCancellation = new CancellationTokenSource();
        _gameTrackPath = Path.GetFullPath(path);
        _gameTrackArc = null;
        _gameDirector = null;
        _gameAnalysisPending = true;
        _gameAnalysisNote = null;
        _gamePlaybackState = PlaybackRunState.Ready;
        _gameSessionNeedsReload = false;
        BumpGameEpoch();
        TrackArcStatus.Text = "正在分析伴奏的相对能量与起音变化…";
        PetStatus.Text = "正在准备这段声场";

        string trackId;
        try
        {
            trackId = await Task.Run(() => TrackId(_gameTrackPath, cancellation.Token), cancellation.Token);
            var decoded = await OfflineTrackDecoder.DecodeAsync(_gameTrackPath, cancellation.Token);
            var arc = await Task.Run(() => _trackArcAnalyzer.Analyze(
                decoded.Samples, decoded.SampleRate, trackId, cancellation.Token), cancellation.Token);
            if (cancellation.IsCancellationRequested || !string.Equals(_gameTrackPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)) return;
            _gameTrackArc = arc;
            _gameDirector = new GameMomentDirector(arc);
            _gamePlaybackState = _playback.IsPlaying ? PlaybackRunState.Playing : PlaybackRunState.Ready;
            _gameAnalysisNote = arc.Highlights.Count == 0
                ? "没有强凑高光候选"
                : $"{arc.Highlights.Count} 处结构候选 · 时机待实际体验";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            if (cancellation.IsCancellationRequested) return;
            trackId = TrackIdFromPath(path);
            double duration = Math.Max(.001, _playback.Duration.TotalSeconds);
            _gameTrackArc = new TrackArc(trackId, duration, [], []);
            _gameDirector = new GameMomentDirector(_gameTrackArc);
            _gamePlaybackState = _playback.IsPlaying ? PlaybackRunState.Playing : PlaybackRunState.Ready;
            _gameAnalysisNote = $"伴奏可播放；结构候选未生成：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_trackAnalysisCancellation, cancellation)) _gameAnalysisPending = false;
        }
        RefreshPerformanceExperience();
    }

    private void UnloadPerformanceTrack()
    {
        _trackAnalysisCancellation?.Cancel();
        _gameTrackPath = null;
        _gameTrackArc = null;
        _gameDirector = null;
        _gameAnalysisPending = false;
        _gameAnalysisNote = null;
        _gamePlaybackState = PlaybackRunState.Ready;
        _gameSessionNeedsReload = false;
        BumpGameEpoch();
    }

    private void ConfigurePerformanceInput(int sampleRate, string deviceId, AudioSourceKind source)
    {
        _gameInputSampleRate = sampleRate;
        var kind = source == AudioSourceKind.GuitarInput
            ? PlayerInputKind.IndependentDevice
            : PlayerInputKind.ComputerLoopback;
        _gamePlayer = new PlayerParticipationTracker(sampleRate, new PlayerInputContract(kind, deviceId));
        _gamePlayer.Reset(_gameEpoch);
        _gameSequence = 0;
    }

    private void StopPerformanceInput()
    {
        BumpGameEpoch();
        _gamePlayer = null;
        _gameInputSampleRate = 0;
        _gameSequence = 0;
    }

    private void ObservePerformanceInput(ReadOnlySpan<float> samples, int sampleRate, long capturedAtTimestamp)
    {
        if (!_performanceTogetherMode || _gamePlayer is null || sampleRate != _gameInputSampleRate) return;
        double packetEnd = capturedAtTimestamp > 0
            ? capturedAtTimestamp / (double)Stopwatch.Frequency
            : Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        double position = _playback.IsLoaded ? _playback.Position.TotalSeconds : 0;
        var beat = _gameTrackArc?.FrameAt(position);
        _gamePlayer.Push(samples, ++_gameSequence, _gameEpoch, packetEnd, position, beat);
    }

    private void NotifyPerformancePlay()
    {
        if (_gameTrackArc is not null && _gameDirector is not null && _gameSessionNeedsReload)
        {
            _gameDirector.Load(_gameTrackArc);
            _gameSessionNeedsReload = false;
        }
        BumpGameEpoch();
        _gamePlaybackState = PlaybackRunState.Playing;
    }

    private void NotifyPerformancePause()
    {
        BumpGameEpoch();
        _gamePlaybackState = PlaybackRunState.Paused;
    }

    private void NotifyPerformanceStop()
    {
        BumpGameEpoch();
        _gamePlaybackState = PlaybackRunState.Stopped;
        _gameSessionNeedsReload = true;
    }

    private void NotifyPerformanceSeek()
    {
        BumpGameEpoch();
        _gamePlaybackState = _playback.IsPlaying ? PlaybackRunState.Playing : PlaybackRunState.Paused;
    }

    private void NotifyPerformanceEnded()
    {
        BumpGameEpoch();
        _gamePlaybackState = PlaybackRunState.Ended;
        _gameSessionNeedsReload = true;
    }

    private void BumpGameEpoch()
    {
        _gameEpoch++;
        _gameSequence = 0;
        _gamePlayer?.Reset(_gameEpoch);
    }

    private void RefreshPerformanceExperience()
    {
        RefreshCelestialStatus();
        RefreshRoomScene();
        StageMomentBadge.Visibility = _performanceTogetherMode &&
            (_gameAnalysisPending || _gameTrackArc is not null) ? Visibility.Visible : Visibility.Collapsed;
        if (!_performanceTogetherMode)
        {
            PlayerEntryLabel.Text = _capture.IsCapturing && _latestSignal.IsActive
                ? "正在接收你选择的声音"
                : _capture.IsCapturing ? "输入已连接，等待声音" : "连接一个声音来源";
            return;
        }

        if (_gameAnalysisPending)
        {
            PetStatus.Text = "正在准备这段声场";
            MusicCueStatus.Text = "伴奏结构预分析";
            PlayerEntryLabel.Text = InputReadinessText();
            RoomBackdrop.Moment = GameMomentState.Ready;
            SetTracePresence(IndependentInputIsActive(), hasAccompaniment: false);
            SetGameCat(false);
            return;
        }
        if (_gameTrackArc is null || _gameDirector is null)
        {
            PetStatus.Text = "选一首伴奏，和它一起演";
            MusicCueStatus.Text = "等待伴奏";
            TrackArcStatus.Text = "结构高光只来自所选本地伴奏，不会把电脑回放算作玩家贡献。";
            PlayerEntryLabel.Text = InputReadinessText();
            PlayerEntryDot.Background = BrushOf(95, 126, 108);
            RoomBackdrop.Moment = GameMomentState.Ready;
            SetTracePresence(IndependentInputIsActive(), hasAccompaniment: false);
            SetGameCat(false);
            return;
        }

        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        double position = Math.Clamp(_playback.Position.TotalSeconds, 0, _gameTrackArc.DurationSeconds);
        var player = _gamePlayer?.Read(now) ?? UnavailablePlayer(now);
        var clock = new PlaybackClock(_gameTrackArc.TrackId, _gameEpoch, position, now, _gamePlaybackState);
        var frame = _gameDirector.Step(clock, player,
            quiet: QuietMode.IsChecked == true,
            reducedMotion: _petPreferences.ReducedMotion || !SystemParameters.ClientAreaAnimation);
        RenderGameFrame(frame, player);
    }

    private void RefreshRoomScene()
    {
        bool reducedMotion = _petPreferences.ReducedMotion || !SystemParameters.ClientAreaAnimation;
        bool hasAccompaniment = _performanceTogetherMode && _playback.IsLoaded;
        bool playerActive = _performanceTogetherMode && IndependentInputIsActive();
        double position = _playback.IsLoaded ? Math.Max(0, _playback.Position.TotalSeconds) : 0;
        double duration = _gameTrackArc?.DurationSeconds ??
            (_playback.IsLoaded ? Math.Max(0, _playback.Duration.TotalSeconds) : 0);
        position = duration > 0 ? Math.Clamp(position, 0, duration) : 0;
        var arcFrame = _gameTrackArc?.FrameAt(position);

        RoomBackdrop.ReducedMotion = reducedMotion;
        RoomBackdrop.PlayerActive = playerActive;
        RoomBackdrop.HasAccompaniment = hasAccompaniment;
        RoomBackdrop.Level = Math.Clamp(LevelMeter.Value, LevelMeter.Minimum, LevelMeter.Maximum);
        RoomBackdrop.PlaybackProgress = duration > 0 ? Math.Clamp(position / duration, 0, 1) : 0;
        RoomBackdrop.AccompanimentEnergy = hasAccompaniment
            ? Math.Clamp(arcFrame?.RelativeEnergy ?? 0, 0, 1)
            : 0;
        RoomBackdrop.IsPlaying = hasAccompaniment && _playback.IsPlaying;
        RoomBackdrop.InputPulseSequence = playerActive ? _latestRhythm.OnsetCount : 0;

        var next = _performanceTogetherMode && _gameTrackArc is not null
            ? _gameTrackArc.Highlights.FirstOrDefault(highlight => highlight.End > position)
            : null;
        if (next is null || duration <= 0)
        {
            RoomBackdrop.NextHighlightProgress = null;
            RoomBackdrop.NextHighlightSeconds = null;
        }
        else
        {
            RoomBackdrop.NextHighlightProgress = Math.Clamp(next.Start / duration, 0, 1);
            RoomBackdrop.NextHighlightSeconds = Math.Max(0, next.Start - position);
        }
        // The dotted bar in the panel is the same real countdown, not a second estimate.
        HighlightProgress.Value = RoomBackdrop.NextHighlightProgress ?? 0;
    }

    private void RenderGameFrame(GameMomentFrame frame, PlayerParticipation player)
    {
        string headline;
        string cue;
        switch (frame.State)
        {
            case GameMomentState.Following:
                headline = "你的声音已加入";
                cue = "正在共同前进";
                break;
            case GameMomentState.Anticipating:
                headline = "共同高光将至";
                cue = "伴奏高光候选 · 约 3 秒内";
                break;
            case GameMomentState.Together:
                headline = "现在，一起把它推起来";
                cue = "共同高光";
                break;
            case GameMomentState.SongHighlight:
                headline = "伴奏进入高光";
                cue = "伴奏高光 · 等你的声音加入";
                break;
            case GameMomentState.Release:
                headline = "这一段刚刚落下";
                cue = "共同余韵";
                break;
            case GameMomentState.Paused:
                headline = "先停在这里";
                cue = "伴奏已暂停";
                break;
            case GameMomentState.Quiet:
                headline = "安静陪着你";
                cue = "局内累计已冻结";
                break;
            case GameMomentState.InputUnavailable:
                headline = "等待独立玩家输入";
                cue = "电脑回放不会计入玩家贡献";
                break;
            case GameMomentState.Ended:
                headline = "这一局结束了";
                cue = frame.SharedHighlights == 0
                    ? "这次还没有共同抵达高光"
                    : $"共同抵达 {frame.SharedHighlights} 个高光 · 仅本局";
                break;
            default:
                headline = _gamePlaybackState == PlaybackRunState.Ready ? "准备一起演" : "让声音进入这段伴奏";
                cue = "等待玩家输入";
                break;
        }

        PetStatus.Text = headline;
        MusicCueStatus.Text = cue;
        TrackArcStatus.Text = _gameAnalysisNote ?? "结构候选来自相对能量与起音变化，不代表副歌或 Solo 识别。";
        PlayerEntryLabel.Text = player.Usable && player.IsActive
            ? "你的声音已加入"
            : player.Eligible ? "已连接，弹一下试试" : "一起演需要吉他输入";
        bool active = player.Usable && player.IsActive;
        PlayerEntryDot.Background = active ? BrushOf(214, 221, 139) : BrushOf(95, 126, 108);
        StageMomentBadge.BorderBrush = frame.State is GameMomentState.Anticipating or GameMomentState.Together
            ? BrushOf(213, 157, 78) : BrushOf(93, 124, 109);
        RoomBackdrop.Moment = frame.State;
        RoomBackdrop.ReducedMotion = _petPreferences.ReducedMotion || !SystemParameters.ClientAreaAnimation;
        RoomBackdrop.SharedHighlights = frame.SharedHighlights;
        bool showPlayerTrace = active && (frame.State is GameMomentState.Following or GameMomentState.Anticipating or
            GameMomentState.Together or GameMomentState.Release);
        SetTracePresence(showPlayerTrace, hasAccompaniment: true);
        SetGameCat(frame.HoldGuitar);
    }

    private void SetGameCat(bool holdGuitar)
    {
        CatPreview.IsJamming = holdGuitar;
        _petWindow.SetJamming(holdGuitar);
    }

    private void SetTracePresence(bool playerActive, bool hasAccompaniment)
    {
        RoomBackdrop.PlayerActive = playerActive;
        RoomBackdrop.HasAccompaniment = hasAccompaniment;
    }

    private bool IndependentInputIsActive() => SelectedSourceKind == AudioSourceKind.GuitarInput &&
        _capture.IsCapturing && _latestSignal.IsActive;

    private string InputReadinessText()
    {
        if (SelectedSourceKind != AudioSourceKind.GuitarInput)
            return "一起演需要吉他输入";
        if (!_capture.IsCapturing) return "连接独立输入";
        return _latestSignal.IsActive ? "你的声音已接入" : "独立输入已连接，等待声音";
    }

    private PlayerParticipation UnavailablePlayer(double now) => new(
        _gameEpoch, -1, false, false, true, false, false, false, 0, false, 0, null, 0);

    private void RefreshCelestialStatus()
    {
        if (RoomBackdrop is not null && SkyStatus is not null)
            SkyStatus.Text = RoomBackdrop.Description;
    }

    private static SolidColorBrush BrushOf(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static string TrackId(string path, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        cancellation.ThrowIfCancellationRequested();
        return Convert.ToHexString(hash)[..20];
    }

    private static string TrackIdFromPath(string path)
    {
        var info = new FileInfo(path);
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{Path.GetFullPath(path)}|{info.Length}|{info.LastWriteTimeUtc.Ticks}");
        return Convert.ToHexString(SHA256.HashData(bytes))[..20];
    }

    public void RenderGamePreview(string path, bool day = false, bool shared = false)
    {
        MainTabs.SelectedIndex = 0;
        TogetherModeButton.IsChecked = true;
        SetPerformanceMode(true);
        RoomBackdrop.PreviewLocalTime = day
            ? new DateTimeOffset(2026, 10, 28, 14, 24, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 28)))
            : new DateTimeOffset(2026, 10, 28, 21, 36, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 28)));
        RoomBackdrop.PreviewMoonPhase = .36;
        SkyStatus.Text = RoomBackdrop.Description;
        InputPicker.Visibility = Visibility.Collapsed;
        StepHint.Visibility = Visibility.Collapsed;
        ConnectionStatus.Visibility = Visibility.Visible;
        ConnectionStatus.Text = "输入：QME-10 · 独立吉他输入";
        ChangeInputButton.Visibility = Visibility.Visible;
        ListenButton.IsEnabled = true;
        ListenButtonLabel.Text = "停止倾听";
        TrackName.Text = "Evening Drive · 本地伴奏";
        TrackTransport.Visibility = Visibility.Visible;
        PlayButton.IsEnabled = StopPlayButton.IsEnabled = Seek.IsEnabled = true;
        PlayButton.Content = "暂停";
        Seek.Maximum = 268;
        Seek.Value = 102;
        TrackTime.Text = "01:42 / 04:28";
        TrackArcStatus.Text = "3 处结构候选 · 时机待实际体验";
        PetStatus.Text = shared ? "现在，一起把它推起来" : "共同高光将至";
        MusicCueStatus.Text = shared ? "共同高光" : "伴奏高光候选 · 约 3 秒内";
        PlayerEntryLabel.Text = "你的声音已加入";
        PlayerEntryDot.Background = BrushOf(214, 221, 139);
        StageMomentBadge.BorderBrush = BrushOf(213, 157, 78);
        StageMomentBadge.Visibility = Visibility.Visible;
        RoomBackdrop.Moment = shared ? GameMomentState.Together : GameMomentState.Anticipating;
        RoomBackdrop.ReducedMotion = false;
        RoomBackdrop.PlaybackProgress = 102d / 268;
        RoomBackdrop.AccompanimentEnergy = shared ? .92 : .72;
        RoomBackdrop.NextHighlightProgress = shared ? 102d / 268 : 105d / 268;
        RoomBackdrop.NextHighlightSeconds = shared ? 0 : 3;
        RoomBackdrop.IsPlaying = true;
        RoomBackdrop.InputPulseSequence = 24;
        RoomBackdrop.SharedHighlights = shared ? 1 : 0;
        SetTracePresence(playerActive: true, hasAccompaniment: true);
        SetGameCat(shared);
        var samples = Enumerable.Range(0, 12000)
            .Select(i => (float)(.24 * Math.Sin(i * .08) * (.25 + .75 * Math.Sin(i * .003) * Math.Sin(i * .003))))
            .ToArray();
        SceneWave.Push(samples, 48000);
        LevelMeter.Value = 42;
        RoomBackdrop.Level = LevelMeter.Value;
        ActivityLabel.Text = "正在听到声音";
        LevelLabel.Text = "声音适中";
        RenderPreview(path);
    }

    private void RunPerformanceUiChecks(Action<bool, string> check)
    {
        SetPerformanceMode(true);
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        var active = new PlayerParticipation(_gameEpoch, now, true, true, false, false, false,
            true, .8, true, 2, .9, 1);
        RenderGameFrame(new(GameMomentState.Anticipating, "diagnostic", "preview",
            false, true, 2, 0, .8, false, null), active);
        check(PetStatus.Text == "共同高光将至" && PlayerEntryLabel.Text == "你的声音已加入" &&
            !CatPreview.IsJamming && !_petWindow.IsJamming,
            "Anticipating maps to the shared-highlight warning while both cats keep their ordinary appearance");
        check(RoomBackdrop.Moment == GameMomentState.Anticipating &&
            RoomBackdrop.PlayerActive && RoomBackdrop.HasAccompaniment && RoomBackdrop.SharedHighlights == 0,
            "Active independent input exposes truthful room input and accompaniment states");

        RenderGameFrame(new(GameMomentState.SongHighlight, "diagnostic", "preview",
            false, true, 3, 0, 1, false, null), UnavailablePlayer(now));
        check(RoomBackdrop.Moment == GameMomentState.SongHighlight &&
            !RoomBackdrop.PlayerActive && RoomBackdrop.HasAccompaniment,
            "Accompaniment highlight stays visible without fabricating an inactive player signal");

        RenderGameFrame(new(GameMomentState.Together, "diagnostic", "preview",
            true, true, 3, 1, 1.2, false, "preview"), active);
        check(CatPreview.IsJamming && _petWindow.IsJamming && MusicCueStatus.Text == "共同高光" &&
            RoomBackdrop.Moment == GameMomentState.Together && RoomBackdrop.SharedHighlights == 1,
            "Only Together maps the validated game frame to the existing guitar-holding state");

        RenderGameFrame(new(GameMomentState.Quiet, "diagnostic", null,
            false, false, 0, 1, 1.2, false, null), active);
        check(!CatPreview.IsJamming && !_petWindow.IsJamming && PetStatus.Text == "安静陪着你",
            "Quiet immediately removes the game guitar state without changing the session summary");
        check(!RoomBackdrop.PlayerActive && RoomBackdrop.HasAccompaniment &&
            RoomBackdrop.Moment == GameMomentState.Quiet,
            "Quiet freezes player motion while retaining the selected accompaniment context");

        RoomBackdrop.PreviewLocalTime = new DateTimeOffset(2026, 10, 28, 14, 0, 0, TimeSpan.FromHours(8));
        check(RoomBackdrop.Description.Contains("日光", StringComparison.Ordinal),
            "Day ambience follows local clock time without location access");
        RoomBackdrop.PreviewLocalTime = new DateTimeOffset(2026, 10, 28, 22, 0, 0, TimeSpan.FromHours(8));
        RoomBackdrop.PreviewMoonPhase = .5;
        check(RoomBackdrop.Description.Contains("满月", StringComparison.Ordinal),
            "Night ambience exposes the approximate lunar phase");
        RoomBackdrop.ReducedMotion = true;
        RoomBackdrop.Moment = GameMomentState.Anticipating;
        RoomBackdrop.PlayerActive = true;
        check(RoomBackdrop.ReducedMotion && RoomBackdrop.Moment == GameMomentState.Anticipating &&
            RoomBackdrop.PlayerActive,
            "Reduced motion keeps truthful static room state while suppressing travel animation");
        RoomBackdrop.PreviewLocalTime = null;
        RoomBackdrop.PreviewMoonPhase = null;

        SetPerformanceMode(false);
        check(TrackDock.Visibility == Visibility.Visible && !_capture.IsCapturing &&
            !RoomBackdrop.PlayerActive && !RoomBackdrop.HasAccompaniment && !RoomBackdrop.IsPlaying &&
            RoomBackdrop.NextHighlightProgress is null && RoomBackdrop.NextHighlightSeconds is null,
            "Free listening separates the listening experience without starting capture");
        SetPerformanceMode(true);
        SetGameCat(false);
    }
}
