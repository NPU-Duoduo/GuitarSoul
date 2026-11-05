namespace GuitarSoul.Core.Audio;

public enum ToneColor { Unknown, Warm, Bright }
public enum PlayingExpression { None, Sustained, Flowing, Animated }
public enum DynamicContour { Steady, Building, Easing }

/// <summary>
/// Short-lived acoustic observations, not genre, playing quality, or the performer's emotions.
/// Confidence describes accumulated above-gate evidence, not a classification probability.
/// </summary>
public readonly record struct ListeningFeatures(ToneColor Tone, PlayingExpression Expression, double Confidence)
{
    public static ListeningFeatures Empty => default;
    public DynamicContour Dynamics { get; init; }
    public double SectionLift { get; init; }
    public long SectionCue { get; init; }
    /// <summary>Decaying midrange articulation; may also come from piano or other struck instruments.</summary>
    public double PluckedPresence { get; init; }
    public HarmonyFrame Harmony { get; init; }
    /// <summary>Chord outlined across a phrase, including broken chords. Recorder only.</summary>
    public HarmonyFrame Phrase { get; init; }
}

/// <summary>
/// Consumes complete analysis windows only. Keeps scalar summaries, never audio samples.
/// All timing follows sample time so capture callback sizes cannot change the result.
/// </summary>
internal sealed class ListeningFeatureTracker
{
    private const double ReleaseSeconds = .65;
    private const double GapHoldSeconds = .45;
    private readonly Queue<double> _attacks = new();
    private readonly double[] _levelBuckets = new double[8];
    private int _bucketCount, _bucketIndex;
    private double _bucketSeconds, _bucketEnergy, _silence, _evidence, _continuous;
    private double _brightness, _toneCandidateSeconds, _expressionCandidateSeconds;
    private bool _hasSound;
    private ToneColor _tone, _toneCandidate;
    private PlayingExpression _expression, _expressionCandidate;
    private DynamicContour _dynamics, _dynamicsCandidate;
    private int _dynamicsCandidateBuckets;

    public ListeningFeatures Current { get; private set; }

    public void Process(double time, double seconds, double rms, double threshold, double brightness, bool attack)
    {
        bool active = rms >= threshold;
        if (attack) _attacks.Enqueue(time);
        while (_attacks.TryPeek(out var expiredAttack) && time - expiredAttack > 3) _attacks.Dequeue();

        if (!active)
        {
            _silence += seconds;
            _continuous = 0;
            if (_silence >= ReleaseSeconds)
            {
                Clear();
                return;
            }
        }
        else
        {
            _silence = 0;
            _continuous += seconds;
            _evidence = Math.Min(1, _evidence + seconds / .75);
            _brightness = _hasSound ? _brightness + (brightness - _brightness) * (1 - Math.Exp(-seconds / .18)) : brightness;
            _hasSound = true;

            // A neutral region and separate release thresholds prevent colour flutter.
            var tone = _brightness < .27 || (_tone == ToneColor.Warm && _brightness < .33)
                ? ToneColor.Warm
                : _brightness > .43 || (_tone == ToneColor.Bright && _brightness > .37)
                    ? ToneColor.Bright : ToneColor.Unknown;
            if (_toneCandidate != tone) { _toneCandidate = tone; _toneCandidateSeconds = 0; }
            _toneCandidateSeconds += seconds;
            if (_toneCandidateSeconds >= .18) _tone = tone;

            int recent = 0;
            double latest = -100;
            foreach (var onset in _attacks)
            {
                latest = onset;
                if (time - onset <= 2) recent++;
            }
            var expression = time - latest <= .75 && recent >= 5 ? PlayingExpression.Animated
                : time - latest <= .85 && recent >= 3 ? PlayingExpression.Flowing
                : _continuous >= .7 && time - latest >= .65 ? PlayingExpression.Sustained
                : PlayingExpression.None;
            if (_expressionCandidate != expression) { _expressionCandidate = expression; _expressionCandidateSeconds = 0; }
            _expressionCandidateSeconds += seconds;
            if (_expressionCandidateSeconds >= .12) _expression = expression;
        }

        if (_hasSound) TrackDynamics(time, seconds, rms, threshold);
        // Preserve established evidence across normal detached notes; a pause adds no evidence.
        double release = Math.Max(0, _silence - GapHoldSeconds) / (ReleaseSeconds - GapHoldSeconds);
        Current = new(_tone, _expression, Math.Clamp(_evidence * (1 - release), 0, 1))
            { Dynamics = _dynamics };
    }

    private void TrackDynamics(double time, double seconds, double rms, double threshold)
    {
        _bucketSeconds += seconds;
        _bucketEnergy += rms * seconds;
        if (_bucketSeconds < .25) return;
        _levelBuckets[_bucketIndex] = _bucketEnergy / _bucketSeconds;
        _bucketIndex = (_bucketIndex + 1) % _levelBuckets.Length;
        _bucketCount = Math.Min(_levelBuckets.Length, _bucketCount + 1);
        _bucketSeconds = _bucketEnergy = 0;

        var candidate = DynamicContour.Steady;
        // Require several articulations: a single pluck's natural decay is not a phrase contour.
        if (_bucketCount == 8 && _attacks.Count >= 4 && time - _attacks.Last() <= .75 && _evidence >= .6)
        {
            double earlier = 0, later = 0;
            for (int i = 0; i < 4; i++)
            {
                earlier += _levelBuckets[(_bucketIndex + i) % 8];
                later += _levelBuckets[(_bucketIndex + i + 4) % 8];
            }
            if (earlier > threshold && later > threshold)
            {
                double ratio = later / earlier;
                candidate = ratio > 1.5 ? DynamicContour.Building : ratio < 1 / 1.5 ? DynamicContour.Easing : DynamicContour.Steady;
            }
        }
        if (_dynamicsCandidate != candidate) { _dynamicsCandidate = candidate; _dynamicsCandidateBuckets = 0; }
        if (++_dynamicsCandidateBuckets >= 2) _dynamics = candidate;
    }

    private void Clear()
    {
        _attacks.Clear();
        Array.Clear(_levelBuckets);
        _bucketCount = _bucketIndex = _dynamicsCandidateBuckets = 0;
        _bucketSeconds = _bucketEnergy = _silence = _evidence = _continuous = _brightness = 0;
        _toneCandidateSeconds = _expressionCandidateSeconds = 0;
        _tone = _toneCandidate = ToneColor.Unknown;
        _expression = _expressionCandidate = PlayingExpression.None;
        _dynamics = _dynamicsCandidate = DynamicContour.Steady;
        _hasSound = false;
        Current = ListeningFeatures.Empty;
    }
}
