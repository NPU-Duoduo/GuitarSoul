namespace GuitarSoul.Audio.Windows;

/// <summary>The user explicitly chooses an input stream or a playback endpoint to hear.</summary>
public enum AudioSourceKind { GuitarInput, ComputerPlayback }

public sealed record AudioDeviceInfo(string Id, string Name, int Channels, int SampleRate, int BitsPerSample)
{
    public override string ToString() => Name;
}

/// <summary>The actual Windows shared-mode stream format, not the USB hardware specification.</summary>
public sealed record AudioCaptureFormat(int SampleRate, int Channels, int BitsPerSample, string Encoding,
    int SelectedChannel, int RecordingChannels, AudioSourceKind SourceKind = AudioSourceKind.GuitarInput);

public sealed class AudioSamplesEventArgs(float[] monoSamples, int sampleRate, float peak, float rms) : EventArgs
{
    public AudioSourceKind SourceKind { get; internal set; }
    public GuitarSoul.Core.Audio.RhythmFrame Rhythm { get; internal set; }
    public long CapturedAtTimestamp { get; internal set; }
    public long SessionId { get; internal set; }
    /// <summary>Selected channel, or the highest-energy channel when recording all channels.</summary>
    public float[] MonoSamples { get; } = monoSamples;
    public int SampleRate { get; } = sampleRate;
    public float Peak { get; } = peak;
    public float Rms { get; } = rms;
}

public sealed class AudioErrorEventArgs(string message, Exception? exception = null) : EventArgs
{
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}

public sealed class RecordingCompletedEventArgs(string path, Exception? error) : EventArgs
{
    public string Path { get; } = path;
    /// <summary>A non-null error means a partial WAV may have been saved.</summary>
    public Exception? Error { get; } = error;
}

public sealed class AudioDeviceChangedEventArgs(string? deviceId, string reason, bool isUnavailable = false) : EventArgs
{
    public string? DeviceId { get; } = deviceId;
    public string Reason { get; } = reason;
    public bool IsUnavailable { get; } = isUnavailable;
}
