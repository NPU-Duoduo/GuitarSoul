using System.Threading.Channels;
using GuitarSoul.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

/// <summary>
/// Explicitly selected source only. Public commands are intended for the UI thread; events run on background threads.
/// Playback capture requires a selected render endpoint; there is no default-device fallback or capture before Start.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    private readonly object _recordingGate = new();
    private readonly object _historyGate = new();
    private AudioRingBuffer? _history;
    private WaveFormat? _historyFormat;
    private bool _historyEnabled;
    private Task<string>? _riffSave;
    private readonly Channel<(WasapiCapture Source, AudioSamplesEventArgs Data)> _samples =
        Channel.CreateBounded<(WasapiCapture, AudioSamplesEventArgs)>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });
    private readonly Task _samplePump;
    private WasapiCapture? _capture;
    private MMDevice? _device;
    private WaveFormat? _waveFormat;
    private BackgroundWaveRecording? _recording;
    private Task<RecordingCompletedEventArgs>? _lastRecordingCompletion;
    private int _selectedChannel;
    private volatile bool _isCapturing;
    private bool _disposed;
    private RhythmTracker? _rhythm;
    private double _activityThresholdDb = -45;
    public double ActivityThresholdDb
    {
        get => Volatile.Read(ref _activityThresholdDb);
        set
        {
            if (!double.IsFinite(value) || value is < -120 or > 0) throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _activityThresholdDb, value);
        }
    }

    public event EventHandler<AudioSamplesEventArgs>? SamplesAvailable;
    public event EventHandler<AudioErrorEventArgs>? CaptureFailed;
    public event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;
    public bool IsCapturing => _isCapturing;
    public bool IsRecording => Volatile.Read(ref _recording)?.IsAccepting == true;
    public AudioCaptureFormat? Format { get; private set; }
    public string? DeviceId { get; private set; }
    public AudioSourceKind? SourceKind { get; private set; }
    public long SessionId { get; private set; }
    public double BufferedSeconds
    {
        get { lock (_historyGate) return _historyFormat is null ? 0 : (_history?.Count ?? 0) / (double)_historyFormat.AverageBytesPerSecond; }
    }

    public AudioCaptureService() => _samplePump = Task.Run(PumpSamplesAsync);

    public void Start(string deviceId, int selectedChannel = -1, AudioSourceKind sourceKind = AudioSourceKind.GuitarInput)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Reject invalid source requests before disturbing the current capture or its pending history.
        var sourceFlow = AudioDevices.GetSourceDataFlow(sourceKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        Stop();
        var device = AudioDevices.Open(deviceId, sourceFlow);
        WasapiCapture? capture = null;
        try
        {
            capture = sourceKind == AudioSourceKind.ComputerPlayback
                ? new WasapiLoopbackCapture(device) { ShareMode = AudioClientShareMode.Shared }
                : new WasapiCapture(device, false, 40) { ShareMode = AudioClientShareMode.Shared };
            var format = capture.WaveFormat;
            PcmSamples.Validate(format);
            if (selectedChannel < -1 || selectedChannel >= format.Channels)
                throw new ArgumentOutOfRangeException(nameof(selectedChannel), "请选择有效的音源声道。");
            _device = device;
            _capture = capture;
            _waveFormat = format;
            _selectedChannel = selectedChannel;
            DeviceId = deviceId;
            SourceKind = sourceKind;
            Format = new(format.SampleRate, format.Channels, format.BitsPerSample, format.Encoding.ToString(),
                selectedChannel, selectedChannel < 0 ? format.Channels : 1, sourceKind);
            SessionId++;
            _rhythm = new RhythmTracker(format.SampleRate);
            SetRiffBufferEnabled(_historyEnabled);
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnCaptureStopped;
            _isCapturing = true;
            capture.StartRecording();
        }
        catch
        {
            _isCapturing = false;
            if (capture is not null)
            {
                capture.DataAvailable -= OnDataAvailable;
                capture.RecordingStopped -= OnCaptureStopped;
                capture.Dispose();
            }
            device.Dispose();
            _capture = null;
            _device = null;
            _waveFormat = null;
            DeviceId = null;
            SourceKind = null;
            Format = null;
            ClearHistory();
            throw;
        }
    }

    public void Stop()
    {
        _isCapturing = false;
        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            capture.RecordingStopped -= OnCaptureStopped;
            try { capture.Dispose(); }
            finally
            {
                _device?.Dispose();
                _device = null;
                FinishRecording();
            }
        }
        else FinishRecording();
        DeviceId = null;
        SourceKind = null;
        Format = null;
        _waveFormat = null;
        _rhythm = null;
        ClearHistory();
    }

    public void SetRiffBufferEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_historyGate)
        {
            _historyEnabled = enabled;
            _history?.Clear();
            _history = null;
            _historyFormat = null;
            if (!enabled || _waveFormat is null) return;
            var source = _waveFormat;
            int channels = _selectedChannel < 0 ? source.Channels : 1;
            _historyFormat = source.Encoding == WaveFormatEncoding.IeeeFloat
                ? WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, channels)
                : new WaveFormat(source.SampleRate, source.BitsPerSample, channels);
            _history = new AudioRingBuffer(checked(source.SampleRate * 30), _historyFormat.BlockAlign);
        }
    }

    private void ClearHistory()
    {
        lock (_historyGate)
        {
            _history?.Clear();
            _history = null;
            _historyFormat = null;
        }
    }

    public Task<string> SaveRecentRiffAsync(string path, double maxSeconds = 30)
    {
        if (!double.IsFinite(maxSeconds) || maxSeconds <= 0 || maxSeconds > 30)
            throw new ArgumentOutOfRangeException(nameof(maxSeconds));
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_historyGate)
        {
            if (_riffSave is { IsCompleted: false })
                throw new InvalidOperationException("上一段 Riff 仍在保存。");
            if (!_isCapturing || _history is null || _historyFormat is null || _history.Count == 0)
                throw new InvalidOperationException("请先开启回溯缓存并开始监听。");
            var bytes = _history.Snapshot();
            var format = _historyFormat;
            int keep = Math.Min(bytes.Length, (int)(format.SampleRate * maxSeconds) * format.BlockAlign);
            if (keep < bytes.Length)
            {
                var trimmed = bytes.AsSpan(bytes.Length - keep).ToArray();
                Array.Clear(bytes); bytes = trimmed;
            }
            var destination = Path.GetFullPath(path);
            // Snapshot is independent of capture. WAV is only made visible after header finalization.
            return _riffSave = Task.Run(() =>
            {
                try
                {
                    return RiffWaveFile.Write(destination, bytes, format);
                }
                finally
                {
                    Array.Clear(bytes);
                }
            });
        }
    }

    public void StartRecording(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_recordingGate)
        {
            if (!_isCapturing || _waveFormat is null)
                throw new InvalidOperationException("请先开始倾听已选择的音源。");
            if (_recording?.IsAccepting == true)
                throw new InvalidOperationException("已有录音正在进行，请先停止录音。");
            var source = _waveFormat;
            int channels = _selectedChannel < 0 ? source.Channels : 1;
            var recordingFormat = source.Encoding == WaveFormatEncoding.IeeeFloat
                ? WaveFormat.CreateIeeeFloatWaveFormat(source.SampleRate, channels)
                : new WaveFormat(source.SampleRate, source.BitsPerSample, channels);
            var recording = new BackgroundWaveRecording(path, recordingFormat);
            _recording = recording;
            _lastRecordingCompletion = recording.Completion;
            _ = ObserveRecordingAsync(recording);
        }
    }

    public async Task<string?> StopRecordingAsync()
    {
        Task<RecordingCompletedEventArgs>? completion = FinishRecording();
        if (completion is null) return null;
        var result = await completion.ConfigureAwait(false);
        if (result.Error is not null) throw new IOException(result.Error.Message, result.Error);
        return result.Path;
    }

    private Task<RecordingCompletedEventArgs>? FinishRecording()
    {
        lock (_recordingGate)
        {
            var recording = _recording;
            _recording = null;
            recording?.Complete();
            return recording?.Completion ?? _lastRecordingCompletion;
        }
    }

    private async Task ObserveRecordingAsync(BackgroundWaveRecording recording)
    {
        var result = await recording.Completion.ConfigureAwait(false);
        lock (_recordingGate)
        {
            if (ReferenceEquals(_recording, recording)) _recording = null;
        }
        if (!_disposed) RecordingCompleted?.Invoke(this, result);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        var format = _waveFormat;
        if (capture is null || !ReferenceEquals(sender, capture) || format is null || e.BytesRecorded <= 0) return;
        var recording = Volatile.Read(ref _recording);
        lock (_historyGate)
        {
            if (_history is not null || recording?.IsAccepting == true)
            {
                var bytes = PcmSamples.CopyRecordingData(e.Buffer, e.BytesRecorded, format, _selectedChannel);
                _history?.Write(bytes);
                if (recording?.IsAccepting == true) recording.TryEnqueue(bytes);
            }
        }
        var analysis = PcmSamples.Analyze(e.Buffer, e.BytesRecorded, format, _selectedChannel);
        analysis.SessionId = SessionId;
        analysis.SourceKind = SourceKind ?? AudioSourceKind.GuitarInput;
        // Process every captured sample before the lossy UI notification queue.
        analysis.Rhythm = _rhythm?.Process(analysis.MonoSamples, ActivityThresholdDb) ?? RhythmFrame.Empty;
        analysis.CapturedAtTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        _samples.Writer.TryWrite((capture, analysis));
    }

    private void OnCaptureStopped(object? sender, StoppedEventArgs e)
    {
        if (!ReferenceEquals(sender, _capture)) return;
        var sourceKind = SourceKind ?? AudioSourceKind.GuitarInput;
        _isCapturing = false;
        SourceKind = null;
        FinishRecording();
        ClearHistory();
        CaptureFailed?.Invoke(this, new(GetCaptureStoppedMessage(sourceKind), e.Exception));
    }

    internal static string GetCaptureStoppedMessage(AudioSourceKind sourceKind) => sourceKind switch
    {
        AudioSourceKind.GuitarInput => "输入设备已停止。请检查 USB 连接、设备占用和 Windows 麦克风权限后重新选择。",
        AudioSourceKind.ComputerPlayback => "电脑播放音源已停止。请检查所选播放设备是否可用，并重新选择；程序不会切换到吉他或麦克风输入。",
        _ => throw new ArgumentOutOfRangeException(nameof(sourceKind))
    };

    private async Task PumpSamplesAsync()
    {
        await foreach (var item in _samples.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            if (_isCapturing && ReferenceEquals(item.Source, _capture))
            {
                try { SamplesAvailable?.Invoke(this, item.Data); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Audio sample subscriber failed: {ex}"); }
            }
            // UI/analysis receives at most 20 notifications per second; no UI work on the capture callback.
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Stop(); }
        finally
        {
            _samples.Writer.TryComplete();
            // Finalize any outstanding WAV header before the process closes.
            _lastRecordingCompletion?.GetAwaiter().GetResult();
            try { _riffSave?.GetAwaiter().GetResult(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
    }
}
