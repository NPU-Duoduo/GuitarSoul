using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Game;
using GuitarSoul.Audio.Windows;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private string? _progressionExportDirectory;

    public void RunOfflineRehearsal(string path, string output)
    {
        if (!_suppressPreferenceSave) throw new InvalidOperationException("离线重放必须使用隔离窗口。");
        Directory.CreateDirectory(output);
        var audio = OfflineTrackDecoder.DecodeAsync(path).GetAwaiter().GetResult();
        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var arc = new TrackArcAnalyzer().Analyze(audio.Samples, audio.SampleRate, hash[..20]);
        var rhythm = new RhythmTracker(audio.SampleRate); var signal = new SignalAnalyzer();
        int hop = Math.Max(1, audio.SampleRate / 20);
        int known = 0, total = 0;
        _progression.Clear(); _chordLoop.Clear();
        for (int start = 0; start + hop <= audio.Samples.Length; start += hop)
        {
            var block = audio.Samples.AsSpan(start, hop);
            var features = rhythm.Process(block);
            var measured = signal.Process(block);
            var heard = features.Listening.Harmony.HasRoot ? features.Listening.Harmony : features.Listening.Phrase;
            if (heard.HasRoot) known++;
            total++;
            _progression.Observe(heard, features.AudioSeconds);
            _latestRhythm = features; _latestSignal = measured; _chordEvidence = heard;
            _chordLoop.Update(_progression.Segments, _progression.Current);
        }
        _lastChordSoundAt = _companionClock.Elapsed.TotalSeconds;
        _gameTrackArc = arc; _gameTrackPath = path;
        TrackName.Text = Path.GetFileName(path);
        ConnectionStatus.Text = "公开录音离线重放 · 未打开声卡";
        ConnectionStatus.Visibility = Visibility.Visible; InputPicker.Visibility = Visibility.Collapsed;
        LevelMeter.Value = Math.Clamp(_latestSignal.PeakDb + 60, 0, 60);
        SceneWave.Push(audio.Samples.AsSpan(Math.Max(0, audio.Samples.Length - hop * 4)).ToArray(), audio.SampleRate);
        TrackTime.Text = $"{TimeSpan.FromSeconds(arc.DurationSeconds):mm\\:ss}";
        MusicCueStatus.Text = $"实际文件分析：{arc.Highlights.Count} 个结构候选";
        StageMomentBadge.Visibility = Visibility.Visible;
        StatusMessage.Text = "离线检查：真实文件→DSP→和弦记录→结构分析；未采集、未播放音频。";
        RefreshProgression();
        RenderPreview(Path.Combine(output, "replay.png"));
        _draftStore = new ChordDraftStore(Path.Combine(output, "draft.json"));
        CheckpointChordDraft(force: true);
        if (!_draftStore.TryLoad(out var recovered, out var error)) throw new IOException(error);
        _progressionExportDirectory = Path.Combine(output, "exports");
        SaveProgression_Click(this, new RoutedEventArgs());
        string[] files = Directory.Exists(_progressionExportDirectory) ? Directory.GetFiles(_progressionExportDirectory, "*.txt") : [];
        if (_progression.Count > 0 && files.Length == 0) throw new IOException("真实文件重放后的和弦导出失败。");
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            source = Path.GetFullPath(path), sha256 = hash, audio.SampleRate, arc.DurationSeconds,
            frames = total, rootEvidenceFrames = known, segments = _progression.Segments,
            confirmedLoop = _chordLoop.Pattern, recoveredSegments = recovered.Length,
            exportedFiles = files, highlights = arc.Highlights,
            captureOpened = false, audioPlayed = false,
            scope = "Public recorded audio through production decoder/DSP/timeline/loop, native UI and actual text-save handler. Not a live-device or timing-accuracy acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }));
        _draftStore = null;
    }
}
