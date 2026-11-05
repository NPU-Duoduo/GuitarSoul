using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Devices;
using GuitarSoul.Core.Companion;
using GuitarSoul.App.Controls;
using Microsoft.Win32;

namespace GuitarSoul.App;

public partial class MainWindow : Window, IDisposable
{
    private sealed record ChannelChoice(int Index, string Name);

    private readonly AudioCaptureService _capture = new();
    private readonly AudioPlaybackService _playback = new();
    private readonly AudioDeviceWatcher _deviceWatcher = new();
    private readonly SignalAnalyzer _analyzer = new();
    private readonly DispatcherTimer _uiTimer;
    private readonly DispatcherTimer _companionTimer;
    private readonly DispatcherTimer _recordTimer;
    private readonly Stopwatch _recordElapsed = new();
    private readonly PetWindow _petWindow = new();
    private DeviceProfileCatalog? _profiles;
    private CalibrationAccumulator? _calibration;
    private IReadOnlyList<AudioDeviceInfo> _inputs = [];
    private IReadOnlyList<AudioDeviceInfo> _outputs = [];
    private string? _lastRecordingPath;
    private bool _disposed;
    private bool _allowClose;
    private bool _refreshingDevices;
    // The input picker stays folded once a device is remembered; "更换" opens it again.
    private bool _inputPickerOpen;
    private readonly ChordTimeline _progression = new();
    private (int Count, int Root, int Quality, int Tonic, bool Minor, bool Confident, bool Ambiguous) _progressionState = (-1, -2, -1, -1, false, false, false);
    private bool _savingRiff;
    private enum PlaybackPurpose { PracticeTrack, KeepsakeReview }
    private PlaybackPurpose? _playbackPurpose;
    private bool IsKeepsakeReviewPlaying =>
        _playback.IsPlaying && _playbackPurpose == PlaybackPurpose.KeepsakeReview;
    private readonly CompanionDirector _companion = new();
    private readonly CompanionVoice _voice = new();
    private bool _preferencesReady;
    private bool _suppressPreferenceSave;
    private PetPreferences _petPreferences = new();
    private InputPreferences? _inputPreferences;
    private bool _pendingOnset;
    private long _pendingOnsetTimestamp;
    private readonly Queue<double> _behaviorDelays = new();
    private RhythmFrame _latestRhythm = RhythmFrame.Empty;
    private TimeSpan _lastAttackTime = TimeSpan.FromSeconds(-100);
    private sealed record SkinChoice(CatSkin Skin, string Label);
    private sealed record GuitarChoice(GuitarStyle Guitar, string Label);
    private sealed record KeyChoice(KeySignature? Key, string Label)
    {
        public bool IsAuto => Key is null;
    }
    private static KeyChoice[] BuildKeyChoices()
    {
        // Auto first: the player should not have to declare a key before the cat has heard anything.
        var choices = new List<KeyChoice> { new(null, "自动推断") };
        for (int tonic = 0; tonic < 12; tonic++) choices.Add(new(new KeySignature(tonic, false), PitchNames.Of(tonic) + " 大调"));
        for (int tonic = 0; tonic < 12; tonic++) choices.Add(new(new KeySignature(tonic, true), PitchNames.Of(tonic) + " 小调"));
        return choices.ToArray();
    }
    private readonly Stopwatch _companionClock = Stopwatch.StartNew();
    private TimeSpan _lastCompanionTick;
    private TimeSpan _lastSignalTime;
    private SignalFrame _latestSignal = SignalFrame.Silence;
    private sealed record RiffItem(string Path, string Label);
    private static string RiffDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Guitar Soul", "Riffs");

    public MainWindow(bool isolatedChecks = false)
    {
        _suppressPreferenceSave = isolatedChecks;
        InitializeComponent();
        MainTabs.SelectedIndex = 0;
        Title = "GuitarSoul · " + typeof(App).Assembly.GetName().Version?.ToString(3);
        AboutVersion.Text = "GuitarSoul · 版本 " + typeof(App).Assembly.GetName().Version?.ToString(3) + " · 开发者 Duoduo_127";
        SkinSelector.ItemsSource = new[] { new SkinChoice(CatSkin.Snow, "白猫 · 奶油白"),
            new SkinChoice(CatSkin.Calico, "三花 · 焦糖拼色"), new SkinChoice(CatSkin.Ginger, "橘猫 · 暖杏虎纹"),
            new SkinChoice(CatSkin.Tabby, "狸花 · 暖灰虎纹") };
        GuitarSelector.ItemsSource = new[] { new GuitarChoice(GuitarStyle.Stratocaster, "Stratocaster"),
            new GuitarChoice(GuitarStyle.LesPaul, "Les Paul") };
        KeySelector.ItemsSource = BuildKeyChoices();
        _petPreferences = isolatedChecks ? new(LocalLanguage: false) : PetPreferences.Load();
        InitializeChordRecovery();
        ChordResetSecondsInput.Text = _petPreferences.ChordResetSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SkinSelector.SelectedIndex = (int)_petPreferences.Skin;
        GuitarSelector.SelectedValue = _petPreferences.Guitar;
        KeySelector.SelectedIndex = _petPreferences.AutoKey ? 0 : _petPreferences.Key.Tonic + (_petPreferences.Key.Minor ? 13 : 1);
        MotionAmount.Value = _petPreferences.MotionAmount;
        ReduceMotion.IsChecked = _petPreferences.ReducedMotion;
        QuietMode.IsChecked = _petPreferences.QuietMode;
        ExpressionSelector.SelectedIndex = (int)_petPreferences.Expression;
        MemoryInvitations.IsChecked = _petPreferences.MemoryInvitations;
        AutoRiffToggle.IsChecked = _petPreferences.AutoRiffs;
        LocalLanguageToggle.IsChecked = _petPreferences.LocalLanguage;
        InitializeLocalLanguage();
        _inputPreferences = isolatedChecks ? null : InputPreferences.Load();
        InitializeSources();
        PreviewMood.SelectedIndex = 1;
        ApplyPetPreferences();
        _preferencesReady = true;
        SyncRiffBuffer();
        InitializeMemories(isolatedChecks);
        InitializeStages(isolatedChecks);
        InitializePerformanceExperience();
        _capture.SamplesAvailable += Capture_SamplesAvailable;
        _capture.CaptureFailed += (_, e) =>
        {
            var session = _capture.SessionId;
            Dispatcher.BeginInvoke(() =>
            {
                if (_disposed || session != _capture.SessionId) return;
                StopListening(e.Message);
            });
        };
        _capture.RecordingCompleted += Capture_RecordingCompleted;
        _playback.PlaybackFailed += (_, e) => OnWorkerError(e.Message, e.Exception);
        _playback.PlaybackEnded += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            UpdatePlaybackUi(false);
            UpdateRiffPlaybackLabel();
            NotifyPerformanceEnded();
        });
        _deviceWatcher.DevicesChanged += DeviceWatcher_DevicesChanged;
        _petWindow.OpenPanelRequested += (_, _) => ShowPanel();
        _petWindow.InvitationOpenRequested += (_, _) => OpenInvitation();
        _uiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background,
            (_, _) => { RefreshPlaybackPosition(); RefreshRiffBuffer(); }, Dispatcher);
        _companionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background,
            (_, _) => { RefreshCompanion(); RefreshPerformanceExperience(); }, Dispatcher);
        _recordTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => RefreshRecordingTime(), Dispatcher);
        LoadProfiles();
        Loaded += (_, _) =>
        {
            RefreshDevices();
            RefreshRiffs();
            RefreshMemories();
            _uiTimer.Start();
            _companionTimer.Start();
            _petWindow.Show();
            if (_petPreferences.LocalLanguage) _ = WarmUpLocalLanguageAsync();
        };
        Closing += MainWindow_Closing;
        RoomBackdrop.PixelScaleChanged += (_, _) => SyncSceneScale();
        PerformanceScene.SizeChanged += (_, _) => SyncSceneScale();
        SyncSceneScale();
    }

    /// <summary>
    /// Keeps the character on exactly the same pixel grid as the room. The backdrop
    /// publishes the integer scale it chose, so cat and room can never disagree.
    /// </summary>
    private void SyncSceneScale()
    {
        if (CatPreview is null || RoomBackdrop is null) return;
        int scale = Math.Clamp(RoomBackdrop.PixelScale, 2, 9);
        double sceneHeight = Math.Max(1, PerformanceScene.ActualHeight);
        double sceneWidth = Math.Max(1, PerformanceScene.ActualWidth);
        var roomBounds=RehearsalRoomBackdrop.ReferenceBounds(sceneWidth,sceneHeight);
        double stageTop = RehearsalRoomBackdrop.StageTopFor(sceneHeight, scale);
        // Anchor the independent sprite and wave to measured positions in the selected
        // reference, using exactly the same cover/crop transform as the room painting.
        double foot=roomBounds.Y+roomBounds.Height*(720d/1064);
        int characterScale = Math.Clamp((int)Math.Round(roomBounds.Height*.19/32),3,9);
        CatPreview.Width = 40 * characterScale;
        CatPreview.Height = 42 * characterScale;
        CatPreview.Margin = new Thickness(0, 0, 0,Math.Max(0,sceneHeight-foot-4*characterScale));
        PetCaption.Margin = new Thickness(0, 0, 0,Math.Max(0,sceneHeight-foot-roomBounds.Height*.022-42));
        SceneWave.PixelUnit = scale;
        SceneWave.Height = 34 * scale;
        double waveCenter=roomBounds.Y+roomBounds.Height*(590d/1064);
        SceneWave.Margin = new Thickness(0,0,0,Math.Max(0,sceneHeight-waveCenter-SceneWave.Height/2));
        SceneFootBar.Margin = new Thickness(34, 0, 0, 32);
        DesktopPinButton.Margin = new Thickness(0, 0, 22, 32);

        SceneWave.CenterGap = Math.Clamp(38.0 * characterScale / sceneWidth, .12, .62);
        // Keep each band about as wide as the cat is tall, so the wave stays a compact
        // cluster around the character instead of a rule across the whole room.
        SceneWave.BandWidth = 34 * scale;

        // The contact shadow needs the row the paws actually rest on: the box bottom plus
        // the sprite's own empty padding under the feet.
        RoomBackdrop.SetCharacterFoot(200-(foot-stageTop)/scale);
    }

    private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeWindow_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void LoadProfiles()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "profiles", "devices.json");
            if (File.Exists(path)) _profiles = DeviceProfileCatalog.Load(path);
        }
        catch (Exception exception)
        {
            StatusMessage.Text = $"设备提示配置未加载：{exception.Message}";
        }
    }

    private void RefreshDevices(string? preferredInputId = null, string? preferredOutputId = null)
    {
        preferredInputId ??= (InputDevices.SelectedItem as AudioDeviceInfo)?.Id;
        preferredOutputId ??= (OutputDevices.SelectedItem as AudioDeviceInfo)?.Id;
        try
        {
            var selectedChannel = (InputChannels.SelectedItem as ChannelChoice)?.Index;
            _inputs = AudioDevices.ListSources(SelectedSourceKind);
            _outputs = AudioDevices.ListOutputs();
            if (preferredInputId is null && _inputPreferences?.Resolve(_inputs, SelectedSourceKind) is AudioDeviceInfo remembered)
            {
                preferredInputId = remembered.Id;
                selectedChannel = _inputPreferences.Channel;
            }
            _refreshingDevices = true;
            InputDevices.ItemsSource = _inputs;
            OutputDevices.ItemsSource = _outputs;
            InputDevices.SelectedItem = _inputs.FirstOrDefault(x => x.Id == preferredInputId);
            OutputDevices.SelectedItem = _outputs.FirstOrDefault(x => x.Id == preferredOutputId);
            _refreshingDevices = false;
            UpdateInputSelection(selectedChannel);
            UpdateConnectionStatus();
            if (_capture.DeviceId is not null && !_inputs.Any(x => x.Id == _capture.DeviceId))
                StopListening("所选输入已断开，请重新选择设备。");
            if (_playback.DeviceId is not null && !_outputs.Any(x => x.Id == _playback.DeviceId))
                UnloadTrack("所选输出已断开，请重新选择输出。", clearTrack: false);
            if (_inputs.Count == 0)
                StatusMessage.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
                    ? "没有发现可用播放设备。请连接耳机或扬声器后刷新。"
                    : "没有发现活动输入设备。连接综合效果器后点击“刷新设备”。";
            else if (InputDevices.SelectedItem is null)
                StatusMessage.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
                    ? "请选择播放器正在使用的耳机或扬声器，再开始倾听。"
                    : "请选择综合效果器或声卡输入。程序不会自动选择电脑麦克风。";
        }
        catch (Exception exception)
        {
            ShowError("无法枚举 Windows 音频设备。", exception);
        }
        finally { _refreshingDevices = false; }
    }

    private void Input_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingDevices) return;
        // Choosing a device finishes the change, so the picker folds back to one summary line.
        _inputPickerOpen = false;
        UpdateInputSelection();
    }

    private void ChangeInput_Click(object sender, RoutedEventArgs e)
    {
        _inputPickerOpen = true;
        UpdateConnectionStatus();
        InputDevices.Focus();
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageTitle is null || !ReferenceEquals(e.OriginalSource, MainTabs)) return;
        PageTitle.Text = MainTabs.SelectedItem == MemoriesTab ? "共同记忆"
            : MainTabs.SelectedItem == StagesTab ? "演奏中的意外收获"
            : MainTabs.SelectedItem == SettingsTab ? "按你的习惯来" : "陪你弹一会儿";
    }

    private void InputChannel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ListenButton is null) return;
        ListenButton.IsEnabled = InputDevices.SelectedItem is AudioDeviceInfo && InputChannels.SelectedItem is ChannelChoice;
        UpdateConnectionStatus();
    }

    private void UpdateConnectionStatus()
    {
        if (ConnectionStatus is null) return;
        var device = InputDevices.SelectedItem as AudioDeviceInfo;
        var channel = InputChannels.SelectedItem as ChannelChoice;
        var configured = device is not null && channel is not null;
        if (!configured)
        {
            ConnectionStatus.Text = "还没有选设备，所以按钮暂时不能点。";
        }
        else
        {
            ConnectionStatus.Text = _capture.IsCapturing
                ? $"正在听 · {device!.Name} · {channel!.Name}"
                : $"输入：{device!.Name} · {channel!.Name}";
        }
        // The hint and the picker already explain a first run; a third line would repeat them.
        ConnectionStatus.Visibility = configured ? Visibility.Visible : Visibility.Collapsed;
        // Once a device is remembered, the whole picker folds away: one button, one summary line.
        var showPicker = !configured || _inputPickerOpen;
        if (StepHint is not null)
            StepHint.Visibility = Visibility.Collapsed;
        if (InputPicker is not null)
            InputPicker.Visibility = showPicker ? Visibility.Visible : Visibility.Collapsed;
        if (ChangeInputButton is not null)
            ChangeInputButton.Visibility = configured && !_inputPickerOpen ? Visibility.Visible : Visibility.Collapsed;
        // An empty combo box reads as broken; say what the box is for until something is chosen.
        if (InputDeviceHint is not null)
        {
            InputDeviceHint.Visibility = InputDevices.SelectedItem is AudioDeviceInfo ? Visibility.Collapsed : Visibility.Visible;
            InputDeviceHint.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
                ? "选择正在使用的耳机或音箱" : "选择效果器或声卡";
        }
    }

    private void UpdateInputSelection(int? preferredChannel = null)
    {
        if (InputDevices.SelectedItem is not AudioDeviceInfo device)
        {
            InputChannels.ItemsSource = null;
            ListenButton.IsEnabled = false;
            InputFormat.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
                ? "选择播放器正在使用的耳机或扬声器。" : "请选择综合效果器或声卡，不会自动启用麦克风。";
            DeviceHint.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
                ? "电脑播放只接收所选输出上的声音；换耳机后需重新选择。" : "选择效果器或声卡输入后查看设备提示。";
            UpdateConnectionStatus();
            return;
        }

        var channels = new List<ChannelChoice> { new(-1, $"全部 {device.Channels} 声道") };
        for (var i = 0; i < device.Channels; i++) channels.Add(new(i, $"声道 {i + 1}"));
        InputChannels.ItemsSource = channels;
        InputChannels.SelectedItem = preferredChannel.HasValue
            ? channels.FirstOrDefault(c => c.Index == preferredChannel)
            : channels[SelectedSourceKind == AudioSourceKind.GuitarInput && device.Channels > 1 ? 1 : 0];
        InputFormat.Text = $"Windows 共享格式：{device.SampleRate:N0} Hz · {device.BitsPerSample}-bit · {device.Channels} 声道";
        var suggestion = _profiles?.GetDisplayNameSuggestions(device.Name).FirstOrDefault();
        DeviceHint.Text = SelectedSourceKind == AudioSourceKind.ComputerPlayback
            ? "接收这个输出上的全部播放声音。请确认播放器也使用此设备；系统更换默认输出不会自动改变监听。"
            : suggestion is null
            ? "通用设备模式：以实际端点格式接入。首次使用请验证通道与伴奏是否混入。"
            : $"设备提示：{suggestion.Manufacturer} {suggestion.Model}。 " +
              (suggestion.IsCaptureSmokeTested
                  ? "此型号曾通过 WASAPI 采集测试；当前接线仍需确认伴奏是否混入。"
                  : "采集链路尚未实机验证。");
        ListenButton.IsEnabled = InputChannels.SelectedItem is ChannelChoice;
        UpdateConnectionStatus();
        if (_capture.IsCapturing && _capture.DeviceId != device.Id) StopListening("输入选择已改变，监听已停止。");
    }

    private void Output_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingDevices) return;
        if (_playback.IsLoaded) UnloadTrack("伴奏输出已改变，请重新选择文件。", clearTrack: false);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDevices();

    private void Listen_Click(object sender, RoutedEventArgs e)
    {
        if (_capture.IsCapturing)
        {
            StopListening("监听已停止。未保存的输入没有写入文件。");
            return;
        }
        if (InputDevices.SelectedItem is not AudioDeviceInfo device || InputChannels.SelectedItem is not ChannelChoice channel)
            return;
        try
        {
            _capture.Start(device.Id, channel.Index, SelectedSourceKind);
            _reception.Reset();
            _audienceRiffs.Reset();
            PreviewMotion.IsChecked = false;
            PreviewMotion.IsEnabled = false;
            _companion.Reset();
            _stageDetector.Reset();
            _voice.Reset();
            PresentListeningReaction(null, ListeningFeatures.Empty);
            _pendingOnset = false;
            _latestRhythm = RhythmFrame.Empty;
            _lastSignalTime = TimeSpan.FromSeconds(-100);
            _behaviorDelays.Clear();
            _lastAttackTime = TimeSpan.FromSeconds(-100);
            _latestSignal = SignalFrame.Silence;
            _lastCompanionTick = _companionClock.Elapsed;
            _analyzer.Reset();
            _analyzer.NoiseFloorDb = -90;
            if (_petPreferences.LocalLanguage) _ = WarmUpLocalLanguageAsync();
            var format = _capture.Format!;
            ConfigurePerformanceInput(format.SampleRate, device.Id, SelectedSourceKind);
            ListenButtonLabel.Text = "停止倾听";
            CalibrateButton.IsEnabled = true;
            RecordButton.IsEnabled = true;
            InputDevices.IsEnabled = false;
            InputChannels.IsEnabled = false;
            SourceKindSelector.IsEnabled = false;
            PetStatus.Text = "它在等声音传来";
            StatusMessage.Text = $"已连接“{device.Name}”，{format.SampleRate:N0} Hz / {format.BitsPerSample}-bit / {format.RecordingChannels} 声道。等待接收声音。";
            RefreshReception(ReceptionState.Waiting);
            UpdateConnectionStatus();
            DeviceSettings.IsExpanded = false;
            _inputPreferences = new(device.Id, channel.Index, device.Channels, SelectedSourceKind);
            if (!_suppressPreferenceSave)
            {
                try { _inputPreferences.Save(); }
                catch (Exception exception) { StatusMessage.Text += $" 输入偏好未保存：{exception.Message}"; }
            }
        }
        catch (Exception exception)
        {
            ShowError(SelectedSourceKind == AudioSourceKind.ComputerPlayback
                ? "无法接收电脑播放。请检查所选耳机或扬声器是否可用。"
                : "无法开始监听。请检查设备占用、USB 连接和 Windows 麦克风权限。", exception);
        }
    }

    private void StopListening(string message)
    {
        _stageDetector.Reset();
        StopPerformanceInput();
        if (_capture.IsRecording) _ = StopRecordingAsync();
        _capture.Stop();
        _reception.Reset();
        _audienceRiffs.Reset();
        _calibration = null;
        _recordTimer.Stop();
        _recordElapsed.Stop();
        _companion.Reset();
        _voice.Reset();
        PresentListeningReaction(null, ListeningFeatures.Empty);
        _pendingOnset = false;
        _latestSignal = SignalFrame.Silence;
        _overloadWarning.Reset();
        _lastSignalTime = _lastAttackTime = TimeSpan.FromSeconds(-100);
        PreviewMotion.IsEnabled = true;
        _invitations.Dismiss(); UpdateInvitationPresentation(null);
        _latestRhythm = RhythmFrame.Empty;
        CatPreview.SetRhythm(RhythmFrame.Empty, 0);
        _petWindow.SetRhythm(RhythmFrame.Empty, 0);
        RhythmStatus.Text = "开始倾听后，它会留意声音的起音与变化。";
        ListenButtonLabel.Text = "开始倾听";
        InputDevices.IsEnabled = true;
        InputChannels.IsEnabled = true;
        SourceKindSelector.IsEnabled = true;
        CalibrateButton.IsEnabled = false;
        RecordButton.IsEnabled = false;
        RecordButton.Content = "开始测试录音";
        ActivityLabel.Text = "还没有开始听";
        LevelLabel.Text = "没有声音";
        LevelMeter.Value = 0;
        InputVu.Reset();
        InputWarning.Visibility = Visibility.Collapsed;
        SceneWave.Clear();
        PetStatus.Text = "它在等你带来声音";
        CatPreview.IsListening = false;
        CatPreview.Mood = CompanionMood.Waiting;
        CatPreview.Level = 0;
        _petWindow.SetListening(false);
        _petWindow.SetMood(CompanionMood.Waiting);
        CatPreview.ClearPose();
        _petWindow.ClearPose();
        // Keep the last chord as played; a stop is not a chord change.
        _progression.Close();
        RefreshProgression();
        UpdateConnectionStatus();
        RefreshRiffBuffer();
        StatusMessage.Text = message;
    }

    private void Capture_SamplesAvailable(object? sender, AudioSamplesEventArgs e)
    {
        var copy = e.MonoSamples;
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || !_capture.IsCapturing || e.SessionId != _capture.SessionId) return;
            _analyzer.ActivityThresholdDb = Threshold.Value;
            var frame = _analyzer.Process(copy);
            var dispatchAge = e.CapturedAtTimestamp > 0 ? Stopwatch.GetElapsedTime(e.CapturedAtTimestamp).TotalSeconds : 0;
            if (dispatchAge < .6 && e.SampleRate > 0)
                _overloadWarning.Observe(copy, e.SampleRate);
            else _overloadWarning.Reset();
            var displayDb = Math.Max(SignalAnalyzer.DisplayFloorDb, frame.PeakDb);
            LevelMeter.Value = displayDb + 60;
            InputVu.IsClipping = _overloadWarning.HasFlatTop;
            InputVu.ToolTip = $"输入峰值 {frame.PeakDb:0.0} dBFS · 平均 {frame.RmsDb:0.0} dBFS\n最近检测窗触顶样本 {_overloadWarning.RailFraction:P2}；连续平顶才提示可能削波。";
            // Peak level is a measurement, not evidence of sustained overload.
            LevelLabel.Text = displayDb <= -59.9 ? "没有声音"
                : displayDb <= -34 ? "声音很轻"
                : $"峰值 {displayDb:0.0} dBFS";
            ActivityLabel.Text = _overloadWarning.IsVisible ? OverloadMessage : frame.IsActive ? "正在听到声音" : "现在很安静";
            ActivityLabel.Foreground = _overloadWarning.IsVisible
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 143, 106))
                : frame.IsActive
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(175, 199, 146))
                    : (System.Windows.Media.Brush)FindResource("Muted");
            _latestSignal = frame;
            if (e.Rhythm.OnsetCount > _latestRhythm.OnsetCount)
            {
                _lastAttackTime = _companionClock.Elapsed - TimeSpan.FromSeconds(Math.Max(0, e.Rhythm.AudioSeconds - e.Rhythm.LastOnsetSeconds) + dispatchAge);
                _pendingOnset = true;
                _pendingOnsetTimestamp = e.CapturedAtTimestamp;
            }
            _latestRhythm = e.Rhythm;
            if (dispatchAge < .6 && _calibration is null && !IsKeepsakeReviewPlaying)
            {
                // A struck triad is read inside one frame. A broken chord never is, so the
                // phrase reading stands in only when the frame has nothing to say.
                AdvanceChordSession(_companionClock.Elapsed.TotalSeconds - dispatchAge, frame.IsActive);
                var heard = e.Rhythm.Listening.Harmony;
                _chordEvidence = heard.HasRoot ? heard : e.Rhythm.Listening.Phrase;
                _progression.Observe(_chordEvidence, ChordSessionSeconds(_companionClock.Elapsed.TotalSeconds - dispatchAge));
            }
            SceneWave.ReducedMotion = _petPreferences.ReducedMotion || !SystemParameters.ClientAreaAnimation;
            if (dispatchAge < .6 && _calibration is null && !IsKeepsakeReviewPlaying)
            {
                // The scene draws the same measured envelope as two pixel bands beside the cat.
                SceneWave.Push(copy, e.SampleRate);
            }
            if (dispatchAge < .6 && _calibration is null && !IsKeepsakeReviewPlaying)
                ObservePerformanceInput(copy, e.SampleRate, e.CapturedAtTimestamp);
            var visibleRhythm = dispatchAge < .6 && _calibration is null && !IsKeepsakeReviewPlaying ? e.Rhythm : RhythmFrame.Empty;
            CatPreview.SetRhythm(visibleRhythm, e.CapturedAtTimestamp);
            _petWindow.SetRhythm(visibleRhythm, e.CapturedAtTimestamp);
            _lastSignalTime = _companionClock.Elapsed - TimeSpan.FromSeconds(dispatchAge);
            _reception.Observe(frame, _lastSignalTime);
            ProcessAudienceRiff(e.Rhythm, frame.IsActive, dispatchAge < .6);
            ProcessStageAchievements(e.Rhythm, frame.IsActive, frame.IsClipping, dispatchAge < .6);
            if (_calibration is not null)
            {
                var result = _calibration.AddSamples(copy);
                UpdateCalibration(result);
            }
        }, DispatcherPriority.Background);
    }

    private void RefreshCompanion()
    {
        AdvanceChordSession(_companionClock.Elapsed.TotalSeconds, false);
        CheckpointChordDraft();
        RefreshProgression();
        var now = _companionClock.Elapsed;
        var elapsed = (now - _lastCompanionTick).TotalSeconds;
        _lastCompanionTick = now;
        if (PreviewMotion.IsChecked == true)
        {
            _voice.Reset(); PresentListeningReaction(null, ListeningFeatures.Empty);
            _invitations.Dismiss(); UpdateInvitationPresentation(null);
            bool jamming = PreviewMood.SelectedIndex == 4;
            var mood = jamming ? CompanionMood.Excited : (CompanionMood)Math.Max(0, PreviewMood.SelectedIndex);
            CatPreview.Mood = mood;
            CatPreview.IsJamming = jamming;
            _petWindow.SetMood(mood);
            _petWindow.SetJamming(jamming);
            PetStatus.Text = jamming ? "一起弹 · 模拟节奏" : "动作预览 · 模拟节奏";
            return;
        }
        bool fresh = _capture.IsCapturing && (now - _lastSignalTime).TotalSeconds < .6;
        var reception = _reception.GetState(_capture.IsCapturing, now);
        RefreshReception(reception);
        if (!fresh || _calibration is not null || IsKeepsakeReviewPlaying) { SceneWave.Clear(); }
        BeatStatus.Text = fresh && _latestRhythm.BeatBpm is double songBpm && _latestRhythm.BeatConfidence >= .6
            ? $"节拍约 {songBpm:0} BPM · 正在跟随" : "寻找稳定节拍 · 暂时跟着实际起音";
        MusicCueStatus.Text = !_capture.IsCapturing ? "尚未开始倾听"
            : _calibration is not null ? "正在校准"
            : IsKeepsakeReviewPlaying ? "正在回听片段"
            : reception == ReceptionState.NoData ? "声音中断 · 检查连接"
            : reception == ReceptionState.BelowThreshold ? "声音太轻"
            : _overloadWarning.IsVisible && fresh ? OverloadMessage
            : !fresh || !_latestSignal.IsActive ? "等待声音"
            : _latestRhythm.Listening.SectionLift >= .35 ? "这一段展开了"
            : _latestRhythm.Listening.PluckedPresence >= .6 ? "拨弦线索突出" : "听着音乐的变化";
        var attackAge = (now - _lastAttackTime).TotalSeconds;
        bool groove = fresh && _latestRhythm.TempoBpm is double tempo && _latestRhythm.Confidence >= .7 &&
            attackAge < Math.Min(1.5, 90 / tempo);
        var onset = fresh && _pendingOnset && attackAge < .6;
        if (onset && _pendingOnsetTimestamp > 0)
        {
            _behaviorDelays.Enqueue(Stopwatch.GetElapsedTime(_pendingOnsetTimestamp).TotalMilliseconds);
            while (_behaviorDelays.Count > 128) _behaviorDelays.Dequeue();
        }
        _pendingOnset = false;
        bool inhibited = _calibration is not null || IsKeepsakeReviewPlaying;
        bool active = fresh && !inhibited && (_latestSignal.IsActive || attackAge < .35 || groove);
        RefreshMemoryInvitation(elapsed, fresh && (_latestSignal.IsActive || attackAge < .35 || groove));
        var listening = RefreshListeningFeedback(fresh, active, elapsed, _latestSignal.IsActive);
        // Rendering receives the same signal eligibility as the new listening response.
        if (!fresh || _calibration is not null || IsKeepsakeReviewPlaying)
        {
            CatPreview.SetRhythm(RhythmFrame.Empty, 0);
            _petWindow.SetRhythm(RhythmFrame.Empty, 0);
        }
        var pose = _companion.Step(active,
            groove && !inhibited, onset && !inhibited, elapsed, _petPreferences.QuietMode || inhibited, _petPreferences.Expression,
            fresh && _calibration is null && !IsKeepsakeReviewPlaying ? _latestRhythm.Drive : 0, listening);
        CatPreview.SetPose(pose);
        _petWindow.SetPose(pose);
        RhythmStatus.Text = !_capture.IsCapturing ? "还没开始倾听。选择声音来源，再带一段音乐给它。"
            : reception is ReceptionState.Waiting or ReceptionState.NoData ? ReceptionDescription(reception)
            : _petPreferences.QuietMode ? "安静陪着你 · 保留表情，暂停跟拍和主动小动作"
            : _calibration is not null ? "正在听输入的底噪，稍后再陪你弹。"
            : IsKeepsakeReviewPlaying ? "正在回听留念，新的声音回应暂歇一会儿。"
            : !active ? "这一刻没有新的声音，它陪你歇一歇。"
            : listening.Confidence >= .6 ? DescribeListening(listening)
            : _latestRhythm.TempoBpm is double bpm
                ? $"正在跟随 · 起音间隔约 {bpm:0} 次/分"
                : _latestRhythm.Drive >= .35 ? "它正跟着连续的起音投入，不必先找到固定速度。"
                : _latestRhythm.Density > .8 ? "声音绵密地延续着，它正留意下一次变化。"
                : "它在听声音的起落和音色变化。";
        PetStatus.Text = !_capture.IsCapturing ? "它在等你带来声音"
            : reception == ReceptionState.Waiting ? "它在等声音传来"
            : reception == ReceptionState.NoData ? "声音暂时没传过来"
            : reception == ReceptionState.BelowThreshold ? "声音太轻，它还没跟上"
            : reception == ReceptionState.Silent ? "这一刻，它安静陪着你"
            : pose.Action switch
        {
            CompanionAction.Notice => "它留意到了声音",
            CompanionAction.Engage => "它轻轻跟着摇起来",
            CompanionAction.Jam => "它想和你一起弹一段",
            CompanionAction.Groove => "它跟着起音轻轻点头",
            CompanionAction.Listen => _latestRhythm.Drive >= .35 && fresh ? "它竖起耳朵听着" : "它在听",
            CompanionAction.Settle => "它慢慢收住动作",
            CompanionAction.Groom => "它理了理自己的毛",
            CompanionAction.Rest => "它眯着眼陪你休息",
            _ => "它正在等下一个音"
        };
    }

    private void Skin_Changed(object sender, SelectionChangedEventArgs e) => UpdatePetPreferences();
    private void Guitar_Changed(object sender, SelectionChangedEventArgs e) => UpdatePetPreferences();
    private void Motion_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePetPreferences();
    private void ReduceMotion_Changed(object sender, RoutedEventArgs e) => UpdatePetPreferences();
    private void QuietMode_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePetPreferences();
        if (_preferencesReady) RefreshCompanion();
    }
    private void Expression_Changed(object sender, SelectionChangedEventArgs e) => UpdatePetPreferences();
    private void Key_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_preferencesReady) return;
        _progressionState = default;
        RefreshProgression();
        UpdatePetPreferences();
    }
    private KeyChoice? SelectedKeyChoice => KeySelector?.SelectedItem as KeyChoice;

    /// <summary>
    /// Null means "no key to print numbers against": either the guess is not confident yet,
    /// or the recording is too short. Chips then show the absolute chord names only.
    /// </summary>
    private KeySignature? EffectiveKey => SelectedKeyChoice is { IsAuto: false } choice
        ? choice.Key
        : GuessKey() is { IsConfident: true } guess ? guess.Key : null;

    private KeyGuess? _keyGuess;
    private long _keyGuessRevision = -1;

    /// <summary>Recomputed when committed chords change, including same-length restore and capacity rollover.</summary>
    private KeyGuess? GuessKey()
    {
        if (_progression.Revision == _keyGuessRevision) return _keyGuess;
        _keyGuessRevision = _progression.Revision;
        _keyGuess = KeyEstimator.Estimate(_progression.Segments);
        return _keyGuess;
    }

    /// <summary>Rebuilds the strip only when the recording or the key actually changed.</summary>
    private void RefreshProgression()
    {
        if (ProgressionStrip is null) return;
        var current = _progression.Current;
        var key = EffectiveKey;
        var guess = SelectedKeyChoice?.IsAuto == true ? GuessKey() : null;
        var state = (_progression.Count, current?.Root ?? -1, (int)(current?.Quality ?? ChordQuality.Unknown),
            key?.Tonic ?? -1, key?.Minor ?? false, guess?.IsConfident ?? false, guess?.Alternative is not null);
        _chordLoop.Update(_progression.Segments, current);
        bool sounding = _latestSignal.IsActive && _companionClock.Elapsed.TotalSeconds - _lastChordSoundAt < .6;
        bool stableCurrent = current is ChordSegment c && c.Duration >= ChordTimeline.MinimumSegmentSeconds;
        bool evidenceMatches = current is ChordSegment currentChord && _chordEvidence.HasRoot &&
            _chordEvidence.Root == currentChord.Root && _chordEvidence.Quality == currentChord.Quality;
        int highlighted = sounding && stableCurrent && evidenceMatches ? _chordLoop.ActiveIndex : -1;
        string visualKey = $"{_progression.Revision}|{stableCurrent}|{highlighted}|{_chordLoop.IsConfirmed}|{sounding}|{_draftBlocked}|{_previousChordDraft.Length}";
        if (state == _progressionState && visualKey == _chordVisualKey) return;
        _chordVisualKey = visualKey;
        _progressionState = state;

        ProgressionStrip.Children.Clear();
        if (_chordLoop.IsConfirmed)
        {
            for (int i = 0; i < _chordLoop.Pattern.Count; i++)
                ProgressionStrip.Children.Add(ProgressionChip(_chordLoop.Pattern[i], key, i == highlighted));
        }
        else
        {
            for (int i = Math.Max(0, _progression.Count - 12); i < _progression.Count; i++)
                ProgressionStrip.Children.Add(ProgressionChip(_progression.Segments[i], key, false));
            if (current is ChordSegment live) ProgressionStrip.Children.Add(ProgressionChip(live, key, sounding));
        }

        bool empty = _progression.Count == 0 && current is null;
        ProgressionHint.Visibility = Visibility.Collapsed;
        ProgressionActions.Visibility = Visibility.Visible;
        SaveProgressionButton.IsEnabled = !empty;
        ClearProgressionButton.IsEnabled = !empty || _draftBlocked || _previousChordDraft.Length > 0;
        var shown = current ?? (_progression.Count > 0 ? _progression.Segments[^1] : (ChordSegment?)null);
        CurrentChordName.Text = shown?.ChordName ?? "—";
        CurrentChordContext.Text = shown is ChordSegment chord
            ? (current is null ? "最近记录" : "正在记录") + (key is KeySignature k ? $" · 第 {chord.DegreeLabel(k)} 级" : "")
            : "等待和弦";
        if (_chordLoop.IsConfirmed)
            CurrentChordContext.Text = highlighted >= 0 ? $"循环 · 第 {highlighted + 1}/{_chordLoop.Pattern.Count} 位"
                : sounding ? "偏离循环 · 继续记录" : "循环已确认 · 等待演奏";
        else if (!empty) CurrentChordContext.Text = sounding ? "记录中 · 等待循环" : "和弦草稿";
        ProgressionDetails.ToolTip = _petPreferences.ChordResetSeconds == 0 ? "空闲重置已关闭；可在设置中调整。"
            : $"停止演奏 {_petPreferences.ChordResetSeconds} 秒后清空；设置中可调整，清空后可恢复上一段。";
        if (!_chordLoop.IsConfirmed) ProgressionScroll.ScrollToRightEnd();

        // Automatic keys must say what they guessed, including when they guessed nothing.
        var note = !empty && SelectedKeyChoice?.IsAuto == true
            ? guess is { IsConfident: true } g ? "听起来像 " + g.Label : "还听不出调"
            : string.Empty;
        KeyGuessLabel.Text = note;
        KeyGuessLabel.Visibility = note.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private FrameworkElement ProgressionChip(ChordSegment segment, KeySignature? key, bool live)
    {
        var ink = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 214, 173));
        var muted = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(183, 197, 174));
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new TextBlock { Text = segment.ChordName, FontSize = 20,
            FontWeight = FontWeights.SemiBold, Foreground = ink, HorizontalAlignment = HorizontalAlignment.Center });
        content.Children.Add(new TextBlock { Text = key is KeySignature k ? $"{segment.DegreeLabel(k)} 级" : "调性未定",
            FontSize = 10.5, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Center });
        var chip = new Border
        {
            Width = 57, MinHeight = 54, CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 3, 4, 4), Margin = new Thickness(0, 0, 6, 2),
            Background = new System.Windows.Media.SolidColorBrush(live
                ? System.Windows.Media.Color.FromRgb(48, 65, 48) : System.Windows.Media.Color.FromRgb(26, 36, 30)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(live
                ? System.Windows.Media.Color.FromRgb(188, 159, 99) : System.Windows.Media.Color.FromRgb(68, 85, 65)),
            BorderThickness = new Thickness(1), Child = content,
            ToolTip = $"{segment.ChordName} · {segment.StartSeconds:0.0}–{segment.EndSeconds:0.0} 秒" +
                (segment.IsRootOnly ? " · 仅根音与五度证据" : "")
        };
        System.Windows.Automation.AutomationProperties.SetName(chip, segment.ChordName);
        System.Windows.Automation.AutomationProperties.SetItemStatus(chip, live ? "当前和弦" : "已记录和弦");
        return chip;
    }

    private void SaveProgression_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _progression.Close();
            if (_progression.Count == 0) { StatusMessage.Text = "还没有记下和弦。"; return; }
            var key = EffectiveKey;
            var guess = SelectedKeyChoice?.IsAuto == true ? GuessKey() : null;
            var directory = _progressionExportDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Guitar Soul", "Progressions");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"Progression-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.txt");
            var text = new System.Text.StringBuilder();
            text.AppendLine("Guitar Soul 和弦进行");
            text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            if (_progression.TruncatedCount > 0) text.AppendLine($"注意：记录超过容量，已丢弃最早 {_progression.TruncatedCount} 段；以下为最近 {ChordTimeline.Capacity} 段。");
            text.AppendLine(key is not KeySignature printed ? "调性：未定，以下只是听到的和弦名"
                : guess is { IsConfident: true } inferred ? $"调性：{inferred.Label}（自动推断，可改）"
                : $"调性：{printed.Name}（你选的）");
            text.AppendLine();
            text.AppendLine(key is KeySignature described ? _progression.Describe(described) : _progression.DescribeAbsolute());
            text.AppendLine();
            if (_chordLoop.IsConfirmed)
                text.AppendLine("已确认循环：" + string.Join(" → ", _chordLoop.Pattern.Select(x => x.ChordName)));
            foreach (var segment in _progression.Segments)
                text.AppendLine(key is KeySignature degreeKey
                    ? $"{segment.DegreeLabel(degreeKey),-4} {segment.ChordName,-4} {segment.StartSeconds:0.0}s - {segment.EndSeconds:0.0}s"
                    : $"{segment.ChordName,-4} {segment.StartSeconds:0.0}s - {segment.EndSeconds:0.0}s");
            var temporary = path + ".tmp";
            try { File.WriteAllText(temporary, text.ToString()); File.Move(temporary, path); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            RefreshProgression();
            StatusMessage.Text = $"已保存这次进行：{Path.GetFileName(path)}";
        }
        catch (Exception exception) { ShowError("无法保存和弦进行。", exception); }
    }

    private void ClearProgression_Click(object sender, RoutedEventArgs e)
    {
        _chordLoop.Clear(); _chordIdle.Restart();
        _chordTimeOrigin = null; _previousChordDraft = [];
        RestoreChordButton.Visibility = Visibility.Collapsed;
        string? clearError = null;
        try { _draftStore?.Clear(); _draftBlocked = false; _lastCheckpointDraft = null; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { clearError = exception.Message; }
        _progression.Clear();
        RefreshProgression();
        StatusMessage.Text = clearError is null ? "和弦进行已清空。" : "当前记录已清空，但磁盘草稿未删除：" + clearError;
    }
    private void UpdatePetPreferences()
    {
        if (!_preferencesReady || SkinSelector.SelectedItem is not SkinChoice selected) return;
        _petPreferences = new(selected.Skin, MotionAmount.Value, ReduceMotion.IsChecked == true,
            QuietMode.IsChecked == true, (ExpressionStyle)Math.Max(0, ExpressionSelector.SelectedIndex),
            MemoryInvitations.IsChecked == true, (GuitarSelector.SelectedItem as GuitarChoice)?.Guitar ?? GuitarStyle.Stratocaster,
            AutoRiffToggle.IsChecked == true, SelectedKeyChoice?.Key ?? _petPreferences.Key,
            SelectedKeyChoice?.IsAuto ?? _petPreferences.AutoKey, LocalLanguageToggle.IsChecked == true, _petPreferences.ChordResetSeconds);
        ApplyPetPreferences();
        if (_suppressPreferenceSave) return;
        try { _petPreferences.Save(); }
        catch (Exception exception) { StatusMessage.Text = $"外观已应用，但偏好未保存：{exception.Message}"; }
    }
    private void ApplyPetPreferences()
    {
        CatPreview.Skin = _petPreferences.Skin;
        CatPreview.Guitar = _petPreferences.Guitar;
        CatPreview.MotionAmount = _petPreferences.MotionAmount;
        CatPreview.ReducedMotion = _petPreferences.ReducedMotion || _petPreferences.QuietMode;
        CatPreview.InvalidateVisual();
        _petWindow.SetAppearance(_petPreferences with { ReducedMotion = CatPreview.ReducedMotion });
        UpdateStagePreview();
    }
    private void PreviewMotion_Changed(object sender, RoutedEventArgs e)
    {
        if (!_preferencesReady) return;
        CatPreview.DemoMotion = PreviewMotion.IsChecked == true;
        _petWindow.SetDemo(CatPreview.DemoMotion);
        CatPreview.ClearPose();
        _petWindow.ClearPose();
        if (PreviewMotion.IsChecked != true)
        {
            _companion.Reset();
            _pendingOnset = false;
            CatPreview.PreviewSeconds = CatPreview.PreviewPulse = null;
            _petWindow.SetPreview(null, null);
            if (!_capture.IsCapturing)
            {
                CatPreview.Mood = CompanionMood.Waiting;
                _petWindow.SetMood(CompanionMood.Waiting);
                PetStatus.Text = "它在等你带来声音";
            }
        }
        CatPreview.InvalidateVisual();
    }

    private void RiffBuffer_Changed(object sender, RoutedEventArgs e)
    {
        if (SaveRiffButton is null) return;
        try { SyncRiffBuffer(); }
        catch (Exception exception) { ShowError("无法开启回溯缓存。", exception); }
        RefreshRiffBuffer();
    }

    private void RefreshRiffBuffer()
    {
        var seconds = _capture.BufferedSeconds;
        RiffBufferStatus.Text = RiffBufferToggle.IsChecked == true || AutoRiffToggle.IsChecked == true
            ? $"内存中已有 {seconds:0.0} / 30 秒 · 停止监听即清空"
            : "开启后只在内存保留最近 30 秒，收藏时才写入文件。";
        SaveRiffButton.IsEnabled = !_savingRiff && _capture.IsCapturing && seconds >= .1;
    }

    private async void SaveRiff_Click(object sender, RoutedEventArgs e)
    {
        if (_savingRiff) return;
        _savingRiff = true;
        RefreshRiffBuffer();
        try
        {
            Directory.CreateDirectory(RiffDirectory);
            var name = $"Riff-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid().ToString("N")[..6]}.wav";
            var seconds = _capture.BufferedSeconds;
            await _capture.SaveRecentRiffAsync(Path.Combine(RiffDirectory, name));
            if (_disposed) return;
            RefreshRiffs();
            StatusMessage.Text = $"已收藏刚才 {seconds:0.0} 秒：{name}";
        }
        catch (Exception exception) { if (!_disposed) ShowError("Riff 保存失败，缓存仍可继续使用。", exception); }
        finally { _savingRiff = false; if (!_disposed) RefreshRiffBuffer(); }
    }

    private void RefreshRiffs()
    {
        try
        {
            var files = Directory.Exists(RiffDirectory)
                ? new DirectoryInfo(RiffDirectory).EnumerateFiles("Riff-*.wav")
                    .OrderByDescending(f => f.LastWriteTimeUtc).Take(100)
                    .Select(f => new RiffItem(f.FullName, RiffLabel(f))).ToArray()
                : [];
            RiffList.ItemsSource = files;
            // An empty list box is a large white void that reads as broken; the hint says it better.
            RiffList.Visibility = files.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            RiffActions.Visibility = files.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            RiffEmpty.Text = files.Length == 0 ? "还没有留念。开启后，你尽管弹，它会挑选一小段。" : $"最近 {files.Length} 段 · 双击回听";
        }
        catch (Exception exception) { StatusMessage.Text = $"无法读取收藏目录：{exception.Message}"; }
    }

    private void PlayRiff_Click(object sender, RoutedEventArgs e)
    {
        if (IsKeepsakeReviewPlaying)
        {
            _playback.Stop();
            UpdatePlaybackUi(false);
            UpdateRiffPlaybackLabel();
            StatusMessage.Text = "已停止回听。";
            return;
        }
        if (RiffList.SelectedItem is not RiffItem item) { StatusMessage.Text = "先选择一段 Riff。"; return; }
        var output = ResolvePlaybackOutput();
        if (output is null) return;
        try
        {
            _playback.Load(item.Path, output.Id);
            _playbackPurpose = PlaybackPurpose.KeepsakeReview;
            _playback.Volume = (float)Volume.Value;
            TrackName.Text = Path.GetFileName(item.Path);
            TrackTransport.Visibility = Visibility.Visible;
            PlaybackOptions.Visibility = Visibility.Visible;
            Seek.Maximum = Math.Max(1, _playback.Duration.TotalSeconds);
            Seek.IsEnabled = PlayButton.IsEnabled = StopPlayButton.IsEnabled = true;
            _playback.Play();
            UpdatePlaybackUi(true);
            UpdateRiffPlaybackLabel();
            StatusMessage.Text = "正在回听收藏。播放控件在「陪伴」页的伴奏区。";
        }
        catch (Exception exception) { ShowError("无法回听该 Riff。", exception); }
    }

    private void RiffList_DoubleClick(object sender, MouseButtonEventArgs e) => PlayRiff_Click(sender, e);

    private void OpenRiffs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(RiffDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{RiffDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError("无法打开收藏目录。", exception); }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guitar Soul");
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception exception) { ShowError("无法打开数据目录。", exception); }
    }

    private void Calibrate_Click(object sender, RoutedEventArgs e)
    {
        if (!_capture.IsCapturing || _capture.Format is null) return;
        _calibration = new CalibrationAccumulator(_capture.Format.SampleRate, 1, 3);
        CalibrateButton.IsEnabled = false;
        CalibrationText.Text = "校准中 0%：停止伴奏，轻按琴弦保持安静。";
    }

    private void UpdateCalibration(CalibrationResult result)
    {
        switch (result.Status)
        {
            case CalibrationStatus.Collecting:
                CalibrationText.Text = $"校准中 {result.Progress:P0}：停止伴奏，轻按琴弦保持安静。";
                return;
            case CalibrationStatus.Complete:
                _analyzer.NoiseFloorDb = result.SuggestedNoiseFloorDb!.Value;
                Threshold.Value = result.SuggestedActivityThresholdDb!.Value;
                CalibrationText.Text = $"校准完成：底噪约 {_analyzer.NoiseFloorDb:0.0} dBFS，活动阈值建议 {Threshold.Value:0.0} dBFS。请轻拨琴弦验证。";
                StatusMessage.Text = "安静输入校准完成。此结果只用于活动检测，不证明声音来源是纯吉他。";
                break;
            case CalibrationStatus.Rejected:
                CalibrationText.Text = result.Message;
                StatusMessage.Text = "校准没有采用。请让效果尾音消失、停止伴奏后重试。";
                break;
        }
        _calibration = null;
        CalibrateButton.IsEnabled = _capture.IsCapturing;
    }

    private void Threshold_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdLabel is null) return;
        ThresholdLabel.Text = $"{e.NewValue:0} dB";
        _capture.ActivityThresholdDb = e.NewValue;
    }

    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (_capture.IsRecording)
        {
            await StopRecordingAsync();
            return;
        }
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Guitar Soul", "测试录音");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"设备测试-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
            _capture.StartRecording(path);
            _recordElapsed.Restart();
            _recordTimer.Start();
            RecordButton.Content = "停止并保存";
            RecordingTime.Text = "00:00 / 01:00";
            RecordingPath.Text = "正在写入测试录音。";
            StatusMessage.Text = "测试录音进行中。最长 60 秒；输入监听仍保持开启。";
        }
        catch (Exception exception)
        {
            ShowError("无法开始测试录音。", exception);
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            RecordButton.IsEnabled = false;
            _recordTimer.Stop();
            _recordElapsed.Stop();
            var path = await _capture.StopRecordingAsync();
            if (!string.IsNullOrWhiteSpace(path)) SetRecordingPath(path);
        }
        catch (Exception exception)
        {
            ShowError("测试录音没有正常完成。", exception);
        }
        finally
        {
            RecordButton.Content = "开始测试录音";
            RecordButton.IsEnabled = _capture.IsCapturing;
            RecordingTime.Text = "最长 60 秒";
        }
    }

    private void RefreshRecordingTime()
    {
        if (!_capture.IsRecording) return;
        var elapsed = _recordElapsed.Elapsed;
        RecordingTime.Text = $"{elapsed:mm\\:ss} / 01:00";
        if (elapsed >= TimeSpan.FromSeconds(60)) _ = StopRecordingAsync();
    }

    private void Capture_RecordingCompleted(object? sender, RecordingCompletedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            if (!_capture.IsRecording)
            {
                _recordTimer.Stop();
                RecordButton.Content = "开始测试录音";
                RecordButton.IsEnabled = _capture.IsCapturing;
                RecordingTime.Text = "最长 60 秒";
            }
            if (e.Error is null) SetRecordingPath(e.Path);
            else ShowError("录音文件可能只保存了部分内容。", e.Error);
        });
    }

    private void SetRecordingPath(string path)
    {
        _lastRecordingPath = path;
        RecordingPath.Text = path;
        OpenRecordingButton.IsEnabled = File.Exists(path);
        StatusMessage.Text = "测试录音已保存。请回听确认通道、音量以及伴奏是否混入。";
    }

    private void OpenRecording_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastRecordingPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_lastRecordingPath}\"") { UseShellExecute = true });
    }

    private void ChooseTrack_Click(object sender, RoutedEventArgs e)
    {
        // Playback is not private data, so the system default output is a safe fallback.
        // Requiring an output choice first was an invisible precondition on a visible button.
        var output = ResolvePlaybackOutput();
        if (output is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "选择本地伴奏",
            Filter = "支持的音频|*.wav;*.mp3;*.m4a;*.mp4;*.wma;*.aac|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _playback.Load(dialog.FileName, output.Id);
            BeginTrackArcAnalysis(dialog.FileName);
            _playbackPurpose = PlaybackPurpose.PracticeTrack;
            _playback.Volume = (float)Volume.Value;
            TrackName.Text = Path.GetFileName(dialog.FileName);
            TrackTransport.Visibility = Visibility.Visible;
            PlaybackOptions.Visibility = Visibility.Visible;
            Seek.Maximum = Math.Max(1, _playback.Duration.TotalSeconds);
            Seek.IsEnabled = true;
            PlayButton.IsEnabled = true;
            StopPlayButton.IsEnabled = true;
            UpdateTrackTime();
            UpdatePlaybackUi(false);
            UpdateRiffPlaybackLabel();
            StatusMessage.Text = "伴奏准备好了。点“播放”或按空格开始。";
            // Loading expands the transport below the default window's fold. Wait for
            // layout, then reveal the next action without starting playback or capture.
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (_disposed || !IsActive || !_playback.IsLoaded || MainTabs.SelectedIndex != 0) return;
                PlayButton.BringIntoView();
                PlayButton.Focus();
            });
        }
        catch (Exception exception)
        {
            ShowError("无法载入伴奏。文件可能没有音轨，或音频编码不受当前 Windows 支持。", exception);
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_playback.IsPlaying)
            {
                _playback.Pause();
                NotifyPerformancePause();
                UpdatePlaybackUi(false);
            }
            else
            {
                _playback.Play();
                NotifyPerformancePlay();
                UpdatePlaybackUi(true);
            }
        }
        catch (Exception exception) { ShowError("伴奏无法播放。", exception); }
    }

    private void StopPlay_Click(object sender, RoutedEventArgs e)
    {
        _playback.Stop();
        NotifyPerformanceStop();
        UpdatePlaybackUi(false);
        UpdateRiffPlaybackLabel();
        RefreshPlaybackPosition();
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_playback is not null) _playback.Volume = (float)e.NewValue;
    }

    private void Seek_Released(object sender, MouseButtonEventArgs e) => ApplySeek();
    private void Seek_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) ApplySeek();
    }
    private void ApplySeek()
    {
        if (_playback.IsLoaded)
        {
            _playback.Position = TimeSpan.FromSeconds(Seek.Value);
            NotifyPerformanceSeek();
        }
    }

    private void RefreshPlaybackPosition()
    {
        if (!_playback.IsLoaded) return;
        if (!Seek.IsMouseCaptureWithin && !Seek.IsKeyboardFocused) Seek.Value = _playback.Position.TotalSeconds;
        UpdateTrackTime();
    }

    private void UpdateTrackTime() => TrackTime.Text = $"{FormatTime(_playback.Position)} / {FormatTime(_playback.Duration)}";
    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1 ? value.ToString("h\\:mm\\:ss") : value.ToString("mm\\:ss");
    private void UpdatePlaybackUi(bool playing) => PlayButton.Content = playing ? "暂停" : "播放";

    /// <summary>The keepsakes tab has no transport of its own, so its one button doubles as stop.</summary>
    private void UpdateRiffPlaybackLabel()
    {
        if (PlayRiffButton is not null) PlayRiffButton.Content = IsKeepsakeReviewPlaying ? "停止回听" : "回听所选";
    }

    private void UnloadTrack(string message, bool clearTrack)
    {
        UnloadPerformanceTrack();
        _playback.Unload();
        UpdatePlaybackUi(false);
        UpdateRiffPlaybackLabel();
        if (clearTrack) TrackName.Text = string.Empty;
        // No track loaded means no transport; the block shrinks back to a single entry point.
        TrackTransport.Visibility = Visibility.Collapsed;
        PlaybackOptions.Visibility = Visibility.Collapsed;
        PlaybackOptions.IsExpanded = false;
        Seek.Value = 0;
        Seek.IsEnabled = false;
        PlayButton.IsEnabled = false;
        StopPlayButton.IsEnabled = false;
        TrackTime.Text = "00:00 / 00:00";
        StatusMessage.Text = message;
    }

    private void ShowPet_Click(object sender, RoutedEventArgs e)
    {
        _petWindow.Show();
        _petWindow.Activate();
    }

    private void ShowPanel()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void DeviceWatcher_DevicesChanged(object? sender, AudioDeviceChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed) return;
            if (e.IsUnavailable && e.DeviceId == _capture.DeviceId)
                StopListening("所选输入设备已断开。已停止监听，不会切换到其他麦克风。");
            if (e.IsUnavailable && e.DeviceId == _playback.DeviceId)
                UnloadTrack("伴奏输出设备已断开，请重新选择输出。", clearTrack: false);
            RefreshDevices();
        }, DispatcherPriority.Background);
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        RefreshDevices();
        var dialog = new SaveFileDialog
        {
            Title = "导出 Guitar Soul 设备诊断",
            FileName = $"Guitar-Soul-设备诊断-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExt = ".json",
            Filter = "JSON 文件|*.json"
        };
        if (dialog.ShowDialog(this) != true) return;
        var report = new
        {
            generatedAt = DateTimeOffset.Now,
            operatingSystem = Environment.OSVersion.VersionString,
            appVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString(),
            selectedInput = InputDevices.SelectedItem,
            selectedChannel = InputChannels.SelectedItem,
            activeCaptureFormat = _capture.Format,
            selectedSource = SelectedSourceKind.ToString(),
            activeSource = _capture.SourceKind?.ToString(),
            receptionState = _reception.GetState(_capture.IsCapturing, _companionClock.Elapsed).ToString(),
            behaviorUpdateDelaysMs = _behaviorDelays.ToArray(),
            selectedOutput = OutputDevices.SelectedItem,
            inputs = _inputs,
            outputs = _outputs,
            note = "端点名称与共享格式来自当前 Windows。该报告不证明硬件路由、干湿声或多客户端兼容性。"
        };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        StatusMessage.Text = $"设备诊断已保存：{dialog.FileName}";
    }

    private void OnWorkerError(string message, Exception? exception) => Dispatcher.BeginInvoke(() => ShowError(message, exception));

    private void ShowError(string message, Exception? exception)
    {
        var detail = exception?.Message;
        StatusMessage.Text = string.IsNullOrWhiteSpace(detail) ? message : $"{message} {detail}";
        MessageBox.Show(this, StatusMessage.Text, "Guitar Soul", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
        StatusMessage.Text = "控制面板已隐藏。可双击桌面猫或托盘图标重新打开。";
    }

    public void RenderPreview(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Content as FrameworkElement ?? throw new InvalidOperationException("主界面没有可渲染内容。");
        var width = (int)Width;
        var height = (int)Height;
        target.Measure(new Size(width, height));
        target.Arrange(new Rect(0, 0, width, height));
        target.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(target);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = File.Create(fullPath);
        encoder.Save(stream);
    }

    /// <summary>Design review helper: renders one daily tab without audio or showing the window.</summary>
    public void RenderTab(int index, string path, bool demoInput = false, int demoChords = 0)
    {
        if (index < 0 || index >= MainTabs.Items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        // The window is never shown in this mode, so Loaded never runs; mirror its read-only setup.
        RefreshDevices();
        if (demoInput && InputDevices.SelectedItem is null)
        {
            // Shows the remembered-device layout on a machine with no interface plugged in.
            var demo = new AudioDeviceInfo("demo-input", "QME-10 · USB 音频", 2, 48000, 24);
            InputDevices.ItemsSource = new[] { demo };
            InputDevices.SelectedItem = demo;
        }
        if (demoChords > 0)
        {
            // Shows the chord strip with content, including a root-only entry, without audio.
            // Two chords is the honest fallback: too little to infer a key from.
            var demo = new (int Root, ChordQuality Quality, bool RootOnly)[]
                { (0, ChordQuality.Major, false), (7, ChordQuality.Unknown, true), (9, ChordQuality.Minor, false), (5, ChordQuality.Unknown, true), (0, ChordQuality.Major, false) };
            double time = 0;
            foreach (var (root, quality, rootOnly) in demo.Take(demoChords))
                for (int step = 0; step < 24; step++, time += .05)
                    _progression.Observe(new HarmonyFrame(root, quality, .95, 0) { RootOnly = rootOnly }, time);
        }
        RefreshRiffs();
        RefreshMemories();
        RefreshProgression();
        MainTabs.SelectedIndex = index;
        RenderPreview(path);
    }

    /// <summary>Exercises device refresh using real endpoints, without starting audio.</summary>
    public void RunUiChecks(string reportPath)
    {
        _suppressPreferenceSave = true;
        var checks = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("UI check failed: " + name);
            checks.Add(name);
        }
        RefreshDevices();
        Check(!_capture.IsCapturing && !_capture.IsRecording && RiffBufferToggle.IsChecked != true, "Startup does not capture, record, or buffer");
        Check(MainTabs.Items.Count == 4 && MainTabs.SelectedIndex == 0,
            $"Daily view opens on listening with an optional stage collection (items={MainTabs.Items.Count}, selected={MainTabs.SelectedIndex})");
        Check(AutoRiffToggle.IsChecked != true && !_petPreferences.AutoRiffs && _capture.BufferedSeconds == 0,
            "Automatic audience keepsakes default off with no buffered audio");
        AutoRiffToggle.IsChecked = true;
        Check(_petPreferences.AutoRiffs && !_capture.IsCapturing && _capture.BufferedSeconds == 0,
            "One-time audience opt-in persists without starting capture");
        AutoRiffToggle.IsChecked = false;
        Check(!_petPreferences.AutoRiffs && !_capture.IsCapturing && _capture.BufferedSeconds == 0,
            "Audience opt-out leaves the device stopped and clears pending audio");
        Check(InputDevices.SelectedItem is null && OutputDevices.SelectedItem is null, "Endpoints require explicit selection");
        RunSourceUiChecks(reportPath, Check);
        var input = _inputs.FirstOrDefault(x => x.Name.Contains("Pocket Master", StringComparison.OrdinalIgnoreCase)) ?? _inputs.FirstOrDefault();
        if (input is not null)
        {
            InputDevices.SelectedItem = input;
            InputChannels.SelectedIndex = 0; // All channels differs from the initial channel-1 selection.
            var output = _outputs.FirstOrDefault();
            OutputDevices.SelectedItem = output;
            RefreshDevices();
            Check((InputDevices.SelectedItem as AudioDeviceInfo)?.Id == input.Id, "Refresh preserves input endpoint");
            Check((InputChannels.SelectedItem as ChannelChoice)?.Index == -1, "Refresh preserves all-channel selection");
            Check((OutputDevices.SelectedItem as AudioDeviceInfo)?.Id == output?.Id, "Refresh preserves output endpoint");
        }
        RiffBufferToggle.IsChecked = true;
        Check(!_capture.IsCapturing && _capture.BufferedSeconds == 0 && !SaveRiffButton.IsEnabled, "Enabling history alone does not start capture");
        RiffBufferToggle.IsChecked = false;
        StopListening("界面检查通过，尚未监听。");
        Check(InputDevices.IsEnabled && InputChannels.IsEnabled && !RecordButton.IsEnabled && !CalibrateButton.IsEnabled, "Stopped state restores controls");
        RefreshRiffs();
        var preferencesPath = Path.ChangeExtension(Path.GetFullPath(reportPath), ".preferences.json");
        var expectedPreferences = new PetPreferences(CatSkin.Calico, .4, true, true, ExpressionStyle.Curious, true, GuitarStyle.LesPaul, true);
        expectedPreferences.Save(preferencesPath);
        Check(PetPreferences.Load(preferencesPath) == expectedPreferences, "Appearance preferences survive save and reload");
        var legacyPath = Path.ChangeExtension(preferencesPath, ".legacy.json");
        File.WriteAllText(legacyPath, "{\"Skin\":2,\"QuietMode\":true,\"MemoryInvitations\":true}");
        var legacy = PetPreferences.Load(legacyPath);
        Check(legacy.Guitar == GuitarStyle.Stratocaster && legacy.Skin == CatSkin.Ginger && legacy.QuietMode && legacy.MemoryInvitations && !legacy.AutoRiffs,
            "Legacy preferences retain appearance and invitations while automatic audio saving stays off");
        File.WriteAllText(legacyPath, "{\"Skin\":1,\"Guitar\":999}");
        Check(PetPreferences.Load(legacyPath) is { Guitar: GuitarStyle.Stratocaster, Skin: CatSkin.Calico },
            "Unknown guitar preference falls back safely without losing skin");
        foreach (var guitar in new[] { GuitarStyle.Stratocaster, GuitarStyle.LesPaul })
        {
            GuitarSelector.SelectedValue = guitar;
            Check(CatPreview.Guitar == guitar && _petWindow.CurrentGuitar == guitar && !_capture.IsCapturing && !_capture.IsRecording,
                "Guitar selection synchronizes panel and pet without capture: " + guitar);
        }
        QuietMode.IsChecked = true;
        Check(CatPreview.ReducedMotion && !_capture.IsCapturing, "Quiet mode immediately stops motion without opening capture");
        QuietMode.IsChecked = false;
        ExpressionSelector.SelectedIndex = 1;
        Check(_petPreferences.Expression == ExpressionStyle.Curious && !_capture.IsCapturing, "Expression style changes independently of skin or capture");
        ExpressionSelector.SelectedIndex = 0;
        var remembered = new InputPreferences("exact-device-id", 1, 2);
        var fixture = new AudioDeviceInfo("exact-device-id", "Fixture guitar", 2, 44100, 32);
        Check(remembered.Resolve([fixture]) == fixture, "Remembered input resolves exact identity and valid channel");
        Check(remembered.Resolve([fixture with { Id = "other-device" }]) is null, "Same friendly name never substitutes a different endpoint");
        Check(remembered.Resolve([fixture with { Channels = 1 }]) is null, "Changed channel layout requires a new explicit selection");
        var inputPreferencesPath = Path.ChangeExtension(Path.GetFullPath(reportPath), ".input-preferences.json");
        remembered.Save(inputPreferencesPath);
        Check(InputPreferences.Load(inputPreferencesPath) == remembered, "Input preference survives save and reload");
        if (input is not null)
        {
            _inputPreferences = new(input.Id, -1, input.Channels);
            InputDevices.SelectedItem = null;
            RefreshDevices();
            Check((InputDevices.SelectedItem as AudioDeviceInfo)?.Id == input.Id &&
                (InputChannels.SelectedItem as ChannelChoice)?.Index == -1 && !_capture.IsCapturing,
                "Exact remembered endpoint and channel restore without capture");
            _inputPreferences = new("missing-input", 0, 2);
            InputDevices.SelectedItem = null;
            RefreshDevices();
            Check(InputDevices.SelectedItem is null && !ListenButton.IsEnabled && !_capture.IsCapturing,
                "Absent remembered input does not fall back to a microphone");
        }
        foreach (var skin in Enum.GetValues<CatSkin>())
        {
            SkinSelector.SelectedIndex = (int)skin;
            Check(CatPreview.Skin == skin, "Skin control applies " + skin);
        }
        Check(CatPreview.Guitar == GuitarStyle.LesPaul && _petPreferences.Guitar == GuitarStyle.LesPaul,
            "Changing skins preserves selected guitar");
        ReduceMotion.IsChecked = true;
        Check(CatPreview.ReducedMotion, "Reduce motion control applies");
        ReduceMotion.IsChecked = false;
        PreviewMotion.IsChecked = true;
        PreviewMood.SelectedIndex = 2;
        RefreshCompanion();
        Check(CatPreview.DemoMotion && CatPreview.Mood == CompanionMood.Excited && !_capture.IsCapturing, "Motion preview never starts audio capture");
        Check(!CatPreview.IsJamming, "Existing excited preview remains unarmed");
        PreviewMood.SelectedIndex = 4;
        RefreshCompanion();
        Check(CatPreview.IsJamming && _petWindow.IsJamming && !_capture.IsCapturing, "New jam preview synchronizes both cats without capture");
        PreviewMood.SelectedIndex = 1;
        RefreshCompanion();
        Check(!CatPreview.IsJamming && !_petWindow.IsJamming, "Leaving jam preview restores original listening state");
        PreviewMotion.IsChecked = false;
        Check(!CatPreview.DemoMotion && CatPreview.Mood == CompanionMood.Waiting, "Ending preview restores idle appearance");
        SkinSelector.SelectedIndex = (int)CatSkin.Calico;
        DeviceSettings.IsExpanded = false;
        RunMemoryUiChecks(reportPath, Check);
        var savedMemoryCount = _memories!.Memories.Count;
        GuitarSelector.SelectedValue = GuitarStyle.Stratocaster;
        Check(_memories.Memories.Count == savedMemoryCount && _memories.Memories.Single().Title == "September",
            "Switching guitar preserves shared memories");
        MainTabs.SelectedIndex = 0;
        SkinSelector.SelectedIndex = (int)CatSkin.Calico;
        RunListeningUiChecks(reportPath, Check);
        RunPerformanceUiChecks(Check);
        StatusMessage.Text = "陪伴界面检查通过；尚未开始倾听或录音。";
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        PreviewMotion.IsChecked = true;
        PreviewMood.SelectedIndex = 4;
        RefreshCompanion();
        CatPreview.PreviewSeconds = .12;
        CatPreview.PreviewPulse = 1;
        _petWindow.SetPreview(.12, 1);
        foreach (var guitar in new[] { GuitarStyle.Stratocaster, GuitarStyle.LesPaul })
        {
            GuitarSelector.SelectedValue = guitar;
            Check(CatPreview.IsJamming && _petWindow.IsJamming && CatPreview.Guitar == guitar &&
                _petWindow.CurrentGuitar == guitar && !_capture.IsCapturing, "Live preview shows Jam with " + guitar + " on both cats without capture");
            var stem = Path.Combine(Path.GetDirectoryName(fullPath)!, Path.GetFileNameWithoutExtension(fullPath));
            RenderPreview(stem + "." + guitar + ".png");
            _petWindow.RenderInvitationPreview(stem + ".pet-" + guitar + ".png");
        }
        CatPreview.PreviewSeconds = CatPreview.PreviewPulse = null;
        _petWindow.SetPreview(null, null);
        PreviewMotion.IsChecked = false;
        Check(!CatPreview.IsJamming && !_petWindow.IsJamming && !_capture.IsCapturing,
            "Ending guitar previews restores the original idle cat without capture");
        RunStageUiChecks(fullPath, Check);
        RunPanelUiChecks(fullPath, Check);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(new { generatedAt = DateTimeOffset.Now, checks, selectedInput = input,
            note = "真实设备枚举和 WPF 控件事件检查；未启动音频采集。" }, new JsonSerializerOptions { WriteIndented = true }));
        RenderPreview(Path.ChangeExtension(fullPath, ".png"));
    }

    public void Dispose()
    {
        if (_disposed) return;
        CheckpointChordDraft(force: true);
        _disposed = true;
        _allowClose = true;
        _trackAnalysisCancellation?.Cancel();
        _trackAnalysisCancellation?.Dispose();
        _uiTimer.Stop();
        _companionTimer.Stop();
        _recordTimer.Stop();
        DisposeLocalLanguage();
        _deviceWatcher.Dispose();
        _playback.Dispose();
        _capture.Dispose();
        CatPreview.Dispose();
        StagePreview.Dispose();
        _achievementTimer?.Stop();
        _petWindow.Close();
        Close();
    }
}
