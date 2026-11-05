using System.IO;
using System.Windows.Controls;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private sealed record SourceChoice(AudioSourceKind Kind, string Label);
    private readonly AudioReception _reception = new();
    private readonly InputOverloadWarning _overloadWarning = new();
    private string OverloadMessage => SelectedSourceKind == AudioSourceKind.ComputerPlayback
        ? "播放信号可能削波 · 检查源播放器音量"
        : "输入可能削波 · 检查声卡或效果器增益";
    private AudioSourceKind SelectedSourceKind =>
        (SourceKindSelector.SelectedItem as SourceChoice)?.Kind ?? AudioSourceKind.GuitarInput;

    private void InitializeSources()
    {
        SourceKindSelector.ItemsSource = new[] {
            new SourceChoice(AudioSourceKind.GuitarInput, "吉他 / 效果器"),
            new SourceChoice(AudioSourceKind.ComputerPlayback, "电脑正在播放") };
        SourceKindSelector.SelectedIndex = _inputPreferences?.SourceKind == AudioSourceKind.ComputerPlayback ? 1 : 0;
    }

    private void SourceKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_preferencesReady || _refreshingDevices) return;
        StopListening("声音来源已更改。选好设备后，点击“开始倾听”。");
        _refreshingDevices = true;
        InputDevices.SelectedItem = null;
        InputChannels.ItemsSource = null;
        _refreshingDevices = false;
        // Only an explicit switch to playback may preselect the current output. It never opens capture.
        string? preferred = null;
        if (SelectedSourceKind == AudioSourceKind.ComputerPlayback)
        {
            try { preferred = AudioDevices.DefaultOutputId(); }
            catch (Exception) { /* Keep the choice empty if Windows has no default output. */ }
        }
        RefreshDevices(preferred);
    }

    private string ReceptionDescription(ReceptionState state) => state switch
    {
        ReceptionState.Stopped => "还没有开始听",
        ReceptionState.Waiting => SelectedSourceKind == AudioSourceKind.ComputerPlayback
            ? "已经连上，但还没听到声音 · 在所选耳机或音箱上放首歌试试"
            : "已经连上，但还没听到声音 · 拨一下弦试试",
        ReceptionState.Receiving => "已经听到声音了",
        ReceptionState.BelowThreshold => "有一点声音，但太轻了，它听不清",
        ReceptionState.Silent => "现在很安静 · 它停下来等",
        _ => SelectedSourceKind == AudioSourceKind.ComputerPlayback
            ? "声音断了 · 确认播放器还在这台设备上放"
            : "声音断了 · 检查一下线有没有松"
    };

    private void RefreshReception(ReceptionState state)
    {
        ActivityLabel.Text = _overloadWarning.IsVisible && state == ReceptionState.Receiving
            ? OverloadMessage : ReceptionDescription(state);
        if (state is ReceptionState.Stopped or ReceptionState.Waiting or ReceptionState.NoData)
        {
            LevelMeter.Value = 0;
            _overloadWarning.Reset();
            InputVu.Reset();
            ActivityLabel.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
            LevelLabel.Text = "没有声音";
        }
        InputWarning.Text = OverloadMessage;
        InputWarning.Visibility = _overloadWarning.IsVisible && state == ReceptionState.Receiving
            ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        UpdateConnectionStatus();
    }

    private void RunSourceUiChecks(string reportPath, Action<bool, string> check)
    {
        var beforeSource = SelectedSourceKind;
        SourceKindSelector.SelectedIndex = 1;
        check(SelectedSourceKind == AudioSourceKind.ComputerPlayback && !_capture.IsCapturing && _capture.SourceKind is null,
            "Explicit playback source selection never starts capture");
        check(_inputs.All(input => _outputs.Any(output => output.Id == input.Id)) &&
            (InputDevices.SelectedItem is null || (InputChannels.SelectedItem as ChannelChoice)?.Index == -1),
            "Playback source lists render endpoints and defaults to all channels");
        StatusMessage.Text = "已选择电脑播放；点击开始倾听后，才会接收所选耳机或扬声器的声音。";
        RenderPreview(Path.ChangeExtension(Path.GetFullPath(reportPath), ".computer-source.png"));
        SetListeningReaction("琴声与拨弦");
        check(PetReaction.Text == "声音与起音" && _petWindow.DisplayedReaction == PetReaction.Text,
            "Playback observations describe sound without claiming guitar-only input");
        SetListeningReaction(null);
        var playback = new InputPreferences("fixture", -1, 2, AudioSourceKind.ComputerPlayback);
        var fixture = new AudioDeviceInfo("fixture", "Output fixture", 2, 48000, 32);
        check(playback.Resolve([fixture]) is null && playback.Resolve([fixture], AudioSourceKind.ComputerPlayback) == fixture,
            "Remembered playback endpoint cannot resolve as a guitar input");
        check((playback with { SourceKind = (AudioSourceKind)999 }).Resolve([fixture]) is null,
            "Unknown source kind never resolves an endpoint");
        var path = Path.ChangeExtension(Path.GetFullPath(reportPath), ".playback-preferences.json");
        playback.Save(path);
        check(InputPreferences.Load(path) == playback, "Playback source preference survives reload");
        File.WriteAllText(path, "{\"DeviceId\":\"fixture\",\"Channel\":-1,\"ChannelCount\":2}");
        check(InputPreferences.Load(path)?.SourceKind == AudioSourceKind.GuitarInput,
            "Legacy input preferences remain guitar inputs");
        var receive = new AudioReception();
        check(receive.GetState(false, TimeSpan.Zero) == ReceptionState.Stopped &&
            receive.GetState(true, TimeSpan.FromSeconds(10)) == ReceptionState.Waiting,
            "An open endpoint with no samples never claims to hear music");
        receive.Observe(SignalFrame.Silence, TimeSpan.FromSeconds(10));
        check(receive.GetState(true, TimeSpan.FromSeconds(10.1)) == ReceptionState.Waiting && !receive.HasHeardSound,
            "Silent callbacks cannot be mistaken for received music");
        receive.Observe(new(.1, .05, -20, -26, true, false), TimeSpan.FromSeconds(11));
        check(receive.GetState(true, TimeSpan.FromSeconds(11.1)) == ReceptionState.Receiving && receive.HasHeardSound,
            "Actual signal is required for the received-sound state");
        receive.Observe(new(.002, .001, -54, -60, false, false), TimeSpan.FromSeconds(11.5));
        check(receive.GetState(true, TimeSpan.FromSeconds(11.6)) == ReceptionState.BelowThreshold,
            "A visible low signal is distinguished from no audio");
        receive.Observe(SignalFrame.Silence, TimeSpan.FromSeconds(12));
        check(receive.GetState(true, TimeSpan.FromSeconds(12.1)) == ReceptionState.Silent &&
            receive.GetState(true, TimeSpan.FromSeconds(13)) == ReceptionState.NoData,
            "Zero audio and missing callbacks have separate states");
        receive.Reset();
        check(!receive.HasHeardSound && receive.GetState(true, TimeSpan.FromSeconds(20)) == ReceptionState.Waiting,
            "A new source cannot inherit the prior session's sound evidence");
        SourceKindSelector.SelectedIndex = beforeSource == AudioSourceKind.ComputerPlayback ? 1 : 0;
        check(!_capture.IsCapturing && _capture.BufferedSeconds == 0 && SourceKindSelector.IsEnabled,
            "Returning to the guitar source leaves capture and buffers stopped");
    }
}
