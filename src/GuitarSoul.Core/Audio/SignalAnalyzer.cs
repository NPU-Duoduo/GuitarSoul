namespace GuitarSoul.Core.Audio;

/// <summary>Levels use normalized digital full scale. Decibel values have a -60 dB display floor.</summary>
public readonly record struct SignalFrame(
    double Peak,
    double Rms,
    double PeakDb,
    double RmsDb,
    bool IsActive,
    bool IsClipping)
{
    public static SignalFrame Silence => new(0, 0, SignalAnalyzer.DisplayFloorDb,
        SignalAnalyzer.DisplayFloorDb, false, false);
}

/// <summary>
/// Analyzes normalized, interleaved samples from the selected input channels.
/// This class is used by one capture worker at a time; callers synchronize setting changes.
/// It detects input activity, not musical intent or the source of a mixed loopback stream.
/// </summary>
public sealed class SignalAnalyzer
{
    public const double DisplayFloorDb = -60;
    private double _noiseFloorDb = -60;
    private double _activityThresholdDb = -45;

    public double NoiseFloorDb
    {
        get => _noiseFloorDb;
        set => _noiseFloorDb = ValidateThreshold(value);
    }

    public double ActivityThresholdDb
    {
        get => _activityThresholdDb;
        set => _activityThresholdDb = ValidateThreshold(value);
    }

    public SignalFrame CurrentFrame { get; private set; } = SignalFrame.Silence;

    public SignalFrame Process(ReadOnlySpan<float> interleavedSamples, int channels = 1)
    {
        ValidateChannels(interleavedSamples.Length, channels);
        if (interleavedSamples.IsEmpty)
            return CurrentFrame = SignalFrame.Silence;

        double sumSquares = 0;
        double peak = 0;
        var clipped = false;
        foreach (var sample in interleavedSamples)
        {
            // Invalid driver data must never propagate NaNs to UI/state calculations.
            if (!float.IsFinite(sample))
                continue;
            var magnitude = Math.Abs((double)sample);
            clipped |= magnitude >= 0.999;
            magnitude = Math.Min(1, magnitude);
            peak = Math.Max(peak, magnitude);
            sumSquares += magnitude * magnitude;
        }

        var rms = Math.Sqrt(sumSquares / interleavedSamples.Length);
        // Compare the real level, before applying the visual floor.
        var active = rms > 0 && ToDb(rms) >= Math.Max(_noiseFloorDb, _activityThresholdDb);
        return CurrentFrame = new SignalFrame(peak, rms,
            Math.Max(DisplayFloorDb, ToDb(peak)), Math.Max(DisplayFloorDb, ToDb(rms)), active, clipped);
    }

    public void Reset() => CurrentFrame = SignalFrame.Silence;

    internal static double ToDb(double amplitude) => amplitude > 0 ? 20 * Math.Log10(amplitude) : -120;

    internal static void ValidateChannels(int sampleCount, int channels)
    {
        if (channels is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(channels), "Channel count must be between 1 and 32.");
        if (sampleCount % channels != 0)
            throw new ArgumentException("The buffer must contain whole interleaved sample frames.", nameof(sampleCount));
    }

    private static double ValidateThreshold(double value)
    {
        if (!double.IsFinite(value) || value is < -120 or > 0)
            throw new ArgumentOutOfRangeException(nameof(value), "A digital level must be finite and between -120 and 0 dBFS.");
        return value;
    }
}
