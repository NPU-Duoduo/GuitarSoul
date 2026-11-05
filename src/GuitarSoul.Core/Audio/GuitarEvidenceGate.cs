namespace GuitarSoul.Core.Audio;

/// <summary>
/// Positive acoustic evidence for guitar-specific behavior. Unknown is the safe default:
/// it does not mean that the sound is not a guitar, only that this short causal window
/// has not supplied enough evidence to claim one.
/// </summary>
public enum GuitarEvidenceDecision { Unknown, Confirmed }

public readonly record struct GuitarEvidenceFrame(
    GuitarEvidenceDecision Decision,
    double HarmonicSeconds,
    double ArticulatedSeconds,
    long Onsets,
    int DistinctRoots,
    double WindowSeconds)
{
    public static GuitarEvidenceFrame Unknown => new(GuitarEvidenceDecision.Unknown, 0, 0, 0, 0, 0);
}

/// <summary>
/// Conservative, causal guitar-evidence gate built only from production acoustic observations.
/// It never consumes a selected input kind, filename, corpus label, model response or self-reported
/// confidence. A short window must contain repeated articulations and more than one stable harmonic
/// root; otherwise the result remains <see cref="GuitarEvidenceDecision.Unknown"/>.
/// </summary>
public sealed class GuitarEvidenceGate
{
    private const double WindowLengthSeconds = 8;
    private readonly Queue<Observation> _recent = new();
    private double _lastTime = -1;
    private double _candidateSeconds;
    private long _lastOnsets;
    private readonly record struct Observation(double Time, double Seconds, long Onsets,
        bool Harmonic, bool Articulated, int Root);

    public GuitarEvidenceFrame Current { get; private set; } = GuitarEvidenceFrame.Unknown;

    public void Reset()
    {
        _recent.Clear();
        _lastTime = -1;
        _candidateSeconds = 0;
        _lastOnsets = 0;
        Current = GuitarEvidenceFrame.Unknown;
    }

    /// <param name="eligible">
    /// Whether this is a fresh, unclipped production callback outside calibration and review.
    /// Eligibility can reject evidence, but can never confirm it.
    /// </param>
    public GuitarEvidenceFrame Observe(RhythmFrame rhythm, SignalFrame signal, bool eligible = true)
    {
        if (!eligible || signal.IsClipping || !double.IsFinite(rhythm.AudioSeconds) || rhythm.AudioSeconds < 0 ||
            rhythm.OnsetCount < 0)
        {
            Reset();
            return Current;
        }
        if (_lastTime < 0)
        {
            _lastTime = rhythm.AudioSeconds;
            _lastOnsets = rhythm.OnsetCount;
            return Current;
        }

        double seconds = rhythm.AudioSeconds - _lastTime;
        if (seconds == 0) return Current;
        if (seconds < 0 || seconds > .6 || rhythm.OnsetCount < _lastOnsets)
        {
            Reset();
            _lastTime = rhythm.AudioSeconds;
            _lastOnsets = rhythm.OnsetCount;
            return Current;
        }

        long onsets = rhythm.OnsetCount - _lastOnsets;
        _lastTime = rhythm.AudioSeconds;
        _lastOnsets = rhythm.OnsetCount;
        var listening = rhythm.Listening;
        bool confidentSound = signal.IsActive && double.IsFinite(listening.Confidence) && listening.Confidence >= .65;
        bool pickedEnvelope = confidentSound && double.IsFinite(listening.PluckedPresence) &&
            listening.PluckedPresence >= .35;
        bool shortHarmony = confidentSound && listening.Harmony.IsKnown &&
            double.IsFinite(listening.Harmony.Strength) && listening.Harmony.Strength >= .86 && pickedEnvelope;
        // Phrase harmony is admitted only with a strong picked-envelope cue. The cue alone is
        // explicitly not instrument identity, and cannot confirm anything without harmony.
        bool phraseHarmony = confidentSound && listening.Phrase.IsKnown &&
            double.IsFinite(listening.Phrase.Strength) && listening.Phrase.Strength >= .86 &&
            double.IsFinite(listening.PluckedPresence) && listening.PluckedPresence >= .55;
        bool harmonic = shortHarmony || phraseHarmony;
        int root = shortHarmony ? listening.Harmony.Root : phraseHarmony ? listening.Phrase.Root : -1;
        bool articulated = confidentSound && listening.Expression is PlayingExpression.Flowing or PlayingExpression.Animated;
        _recent.Enqueue(new(rhythm.AudioSeconds, seconds, confidentSound ? onsets : 0, harmonic, articulated, root));
        while (_recent.TryPeek(out var old) && rhythm.AudioSeconds - old.Time >= WindowLengthSeconds) _recent.Dequeue();
        while (_recent.Count > 1600) _recent.Dequeue();

        if (!signal.IsActive)
        {
            _candidateSeconds = 0;
            Current = GuitarEvidenceFrame.Unknown;
            return Current;
        }

        double harmonicSeconds = _recent.Where(x => x.Harmonic).Sum(x => x.Seconds);
        double articulatedSeconds = _recent.Where(x => x.Articulated).Sum(x => x.Seconds);
        long windowOnsets = _recent.Sum(x => x.Onsets);
        int roots = _recent.Where(x => x.Root >= 0).GroupBy(x => x.Root)
            .Count(group => group.Sum(x => x.Seconds) >= .25);
        double windowSeconds = _recent.Sum(x => x.Seconds);
        // Confirmation is deliberately strict. In particular, one stable pitch, a periodic
        // transient stream, or an input chosen by the user can never satisfy this rule.
        bool qualifies = harmonic && harmonicSeconds >= 1.2 && articulatedSeconds >= 1.2 &&
            windowOnsets >= 8 && roots >= 2;
        _candidateSeconds = qualifies ? _candidateSeconds + seconds : 0;
        bool confirmed = _candidateSeconds >= 1;
        Current = new(confirmed ? GuitarEvidenceDecision.Confirmed : GuitarEvidenceDecision.Unknown,
            harmonicSeconds, articulatedSeconds, windowOnsets, roots, windowSeconds);
        return Current;
    }
}
