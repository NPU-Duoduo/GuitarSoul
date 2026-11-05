using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

/// <summary>File playback to an explicitly selected endpoint. Public commands are intended for the UI thread.</summary>
public sealed class AudioPlaybackService : IDisposable
{
    private AudioFileReader? _reader;
    private WasapiOut? _output;
    private MMDevice? _device;
    private float _volume = 0.5f;
    private bool _disposed;
    private bool _requestedStop;

    public event EventHandler<AudioErrorEventArgs>? PlaybackFailed;
    public event EventHandler? PlaybackEnded;
    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;
    public bool IsLoaded => _reader is not null && _output is not null;
    public string? FilePath { get; private set; }
    public string? DeviceId { get; private set; }
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public float Volume
    {
        get => _volume;
        set
        {
            if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _volume = Math.Clamp(value, 0, 1);
            if (_reader is not null) _reader.Volume = _volume;
        }
    }

    /// <summary>Decoder position; the audible position can trail by the shared-mode output buffer.</summary>
    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is null) return;
            _reader.CurrentTime = TimeSpan.FromTicks(Math.Clamp(value.Ticks, 0, Duration.Ticks));
        }
    }

    public void Load(string path, string outputDeviceId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path)) throw new FileNotFoundException("找不到伴奏文件。", path);
        Unload();
        try
        {
            _device = AudioDevices.Open(outputDeviceId, DataFlow.Render);
            // NAudio uses WaveFileReader for WAV and MediaFoundationReader for MP3/MP4 audio.
            // MP4 support depends on installed Windows Media Foundation codecs and a valid audio track.
            _reader = new AudioFileReader(path) { Volume = _volume };
            _output = new WasapiOut(_device, AudioClientShareMode.Shared, true, 100);
            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_reader);
            FilePath = System.IO.Path.GetFullPath(path);
            DeviceId = outputDeviceId;
        }
        catch
        {
            Unload();
            throw;
        }
    }

    public void Play()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_output is null || _reader is null) throw new InvalidOperationException("请先选择输出设备并载入伴奏。");
        if (_reader.Position >= _reader.Length) _reader.Position = 0;
        _requestedStop = false;
        _output.Play();
    }

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        _requestedStop = true;
        _output?.Stop();
        if (_reader is not null) _reader.Position = 0;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (!ReferenceEquals(sender, _output) || _disposed) return;
        if (e.Exception is not null)
            PlaybackFailed?.Invoke(this, new("伴奏播放停止。请检查所选输出设备及文件音频编码。", e.Exception));
        else if (!_requestedStop)
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    public void Unload()
    {
        _requestedStop = true;
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Dispose();
            _output = null;
        }
        _reader?.Dispose();
        _reader = null;
        _device?.Dispose();
        _device = null;
        FilePath = null;
        DeviceId = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Unload();
    }
}
