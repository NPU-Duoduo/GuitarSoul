namespace GuitarSoul.Core.Audio;

public static class PitchNames
{
    private static readonly string[] Names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    public static string Of(int pitchClass) => Names[((pitchClass % 12) + 12) % 12];
}

/// <summary>
/// Tonic plus mode. Degrees are counted inside this scale: in A minor C is 3, not b3.
/// The key is a display transform over recorded roots, so changing it never re-analyses audio.
/// </summary>
public readonly record struct KeySignature(int Tonic, bool Minor)
{
    // Interval from tonic to the scale degree, per semitone.
    private static readonly (int Number, int Accidental)[] MajorTable =
        [(1, 0), (2, -1), (2, 0), (3, -1), (3, 0), (4, 0), (4, 1), (5, 0), (6, -1), (6, 0), (7, -1), (7, 0)];
    private static readonly (int Number, int Accidental)[] MinorTable =
        [(1, 0), (2, -1), (2, 0), (3, 0), (3, 1), (4, 0), (4, 1), (5, 0), (6, 0), (6, 1), (7, 0), (7, 1)];

    public static KeySignature Default => new(0, false);
    public int PitchClass => ((Tonic % 12) + 12) % 12;
    public string Name => PitchNames.Of(PitchClass) + (Minor ? " 小调" : " 大调");

    public ScaleDegree DegreeOf(int root)
    {
        int interval = (((root - PitchClass) % 12) + 12) % 12;
        var (number, accidental) = (Minor ? MinorTable : MajorTable)[interval];
        return new ScaleDegree(number, accidental);
    }
}

public readonly record struct ScaleDegree(int Number, int Accidental)
{
    public string Label => Accidental switch { < 0 => "b" + Number, > 0 => "#" + Number, _ => Number.ToString() };
}

/// <summary>
/// One held chord. Quality is Unknown when only the root and fifth were evidenced,
/// which is the normal case for a distorted power chord.
/// </summary>
public readonly record struct ChordSegment(int Root, ChordQuality Quality, double StartSeconds, double EndSeconds)
{
    public double Duration => Math.Max(0, EndSeconds - StartSeconds);
    public bool IsRootOnly => Quality == ChordQuality.Unknown;
    public string ChordName => PitchNames.Of(Root) + (Quality switch { ChordQuality.Minor => "m", ChordQuality.Major => "", _ => "5" });
    public string DegreeLabel(KeySignature key) => key.DegreeOf(Root).Label;
    public string QualityMark => Quality switch { ChordQuality.Minor => "m", ChordQuality.Major => "", _ => "5" };
}

/// <summary>
/// Turns stable harmony frames into an ordered chord list. Repeated strums of the same
/// chord stay one entry; a short blip is dropped; a gap closes the current chord.
/// Only scalar roots and times are kept, never audio.
/// </summary>
public sealed class ChordTimeline
{
    public const double MinimumSegmentSeconds = .35;
    public const double GapSeconds = 1.2;
    public const int Capacity = 512;
    public int TruncatedCount { get; private set; }
    public long Revision { get; private set; }
    private readonly List<ChordSegment> _segments = new();
    private int _root = -1;
    private ChordQuality _quality;
    private double _start, _last;
    private bool _open;

    public IReadOnlyList<ChordSegment> Segments => _segments;
    public ChordSegment? Current => _open ? new ChordSegment(_root, _quality, _start, _last) : null;
    public int Count => _segments.Count;

    public void Observe(HarmonyFrame frame, double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return;
        if (_open && seconds < _last) Close();
        if (!frame.HasRoot)
        {
            // Silence is not a chord change, but a long gap ends the held chord.
            if (_open && seconds - _last >= GapSeconds) Close();
            return;
        }
        int root = frame.PitchClass;
        if (_open && root == _root && frame.Quality == _quality)
        {
            _last = seconds;
            return;
        }
        Close();
        double start = seconds;
        // A brief misread between two statements of the same chord should not split it in two.
        if (_segments.Count > 0)
        {
            var last = _segments[^1];
            if (last.Root == root && last.Quality == frame.Quality && seconds >= last.EndSeconds && seconds - last.EndSeconds <= .5)
            {
                _segments.RemoveAt(_segments.Count - 1);
                Revision++;
                start = last.StartSeconds;
            }
        }
        _root = root;
        _quality = frame.Quality;
        _start = start;
        _last = seconds;
        _open = true;
    }

    /// <summary>Ends the held chord, dropping it when it was too brief to be played on purpose.</summary>
    public void Close()
    {
        if (!_open) return;
        if (_last - _start >= MinimumSegmentSeconds)
        {
            _segments.Add(new ChordSegment(_root, _quality, _start, _last));
            Revision++;
            if (_segments.Count > Capacity) { _segments.RemoveAt(0); TruncatedCount++; }
        }
        _open = false;
        _root = -1;
    }

    public void Clear()
    {
        Revision++;
        _segments.Clear();
        _open = false;
        _root = -1;
        _start = _last = 0;
        TruncatedCount = 0;
    }

    public void Restore(IReadOnlyList<ChordSegment> segments)
    {
        var copy = segments.Where(s => s.Root is >= 0 and < 12 && double.IsFinite(s.StartSeconds) &&
            double.IsFinite(s.EndSeconds) && s.Duration >= MinimumSegmentSeconds).TakeLast(Capacity).ToArray();
        Clear();
        _segments.AddRange(copy);
    }

    /// <summary>Progression text such as "1 5 6 4" for the current key.</summary>
    public string Describe(KeySignature key) =>
        string.Join(" ", _segments.Select(s => s.DegreeLabel(key)));

    /// <summary>Absolute names such as "C G Am F", which is what was actually recorded.</summary>
    public string DescribeAbsolute() => string.Join(" ", _segments.Select(s => s.ChordName));
}

/// <summary>
/// The key a recorded progression implies. Chords alone cannot separate a key from its
/// relative major or minor, so that pair is reported together rather than one being invented.
/// </summary>
public readonly record struct KeyGuess(KeySignature Key, KeySignature? Alternative, double Fit)
{
    public bool IsConfident => Fit >= KeyEstimator.ConfidentFit;
    public string Label => Alternative is KeySignature other ? $"{Key.Name}（也可能 {other.Name}）" : Key.Name;
}

/// <summary>
/// Infers a key from the chords that were played, so the player never has to declare one first.
/// This is a display decision only: it is computed after the fact from recorded roots and
/// never reaches the detector, which stays blind to key.
/// </summary>
public static class KeyEstimator
{
    public const int MinimumChords = 3;
    public const double ConfidentFit = .55;
    private const double AmbiguityBand = .12;

    // Accepted triad qualities per scale degree. An empty set means the triad we cannot hear
    // (the diminished seventh degree) and any quality is neutral there.
    private static readonly ChordQuality[][] MajorTriads =
    [
        [ChordQuality.Major], [ChordQuality.Minor], [ChordQuality.Minor], [ChordQuality.Major],
        [ChordQuality.Major], [ChordQuality.Minor], []
    ];
    // The fifth degree accepts a major triad: raising the seventh is the norm, not a borrowing.
    private static readonly ChordQuality[][] MinorTriads =
    [
        [ChordQuality.Minor], [], [ChordQuality.Major], [ChordQuality.Minor],
        [ChordQuality.Minor, ChordQuality.Major], [ChordQuality.Major], [ChordQuality.Major]
    ];

    /// <summary>Null when the recording is too short or too repetitive to imply anything.</summary>
    public static KeyGuess? Estimate(IReadOnlyList<ChordSegment> segments)
    {
        if (segments is null || segments.Count < MinimumChords) return null;
        if (segments.Select(s => s.Root).Distinct().Count() < 2) return null;

        var fit = new double[24];
        var ranking = new double[24];
        for (int candidate = 0; candidate < 24; candidate++)
        {
            var key = new KeySignature(candidate % 12, candidate >= 12);
            var (inKey, bonus) = Score(key, segments);
            fit[candidate] = inKey;
            ranking[candidate] = inKey + bonus;
        }
        int best = 0;
        for (int i = 1; i < 24; i++) if (ranking[i] > ranking[best]) best = i;
        var winner = new KeySignature(best % 12, best >= 12);

        // The relative key contains the same seven pitch classes, so it is a structural tie
        // rather than an estimation error. Say both instead of pretending to pick one.
        // With no third anywhere in the recording the mode is unknowable, so the pair stands
        // even when the cadence would otherwise break the tie.
        bool modeEvidenced = segments.Any(s => s.Quality != ChordQuality.Unknown);
        int relative = (winner.PitchClass + (winner.Minor ? 3 : 9)) % 12 + (winner.Minor ? 0 : 12);
        KeySignature? alternative = !modeEvidenced || relative != best && ranking[relative] >= ranking[best] - AmbiguityBand
            ? new KeySignature(relative % 12, relative >= 12)
            : null;
        return new KeyGuess(winner, alternative, Math.Clamp(fit[best], 0, 1));
    }

    private static (double InKey, double Bonus) Score(KeySignature key, IReadOnlyList<ChordSegment> segments)
    {
        int[] steps = key.Minor ? [0, 2, 3, 5, 7, 8, 10] : [0, 2, 4, 5, 7, 9, 11];
        var scale = new int[7];
        for (int i = 0; i < 7; i++) scale[i] = (key.PitchClass + steps[i]) % 12;
        var triads = key.Minor ? MinorTriads : MajorTriads;

        double credits = 0, weights = 0;
        foreach (var segment in segments)
        {
            double weight = Math.Clamp(segment.Duration, .5, 4);
            weights += weight;
            int degree = Array.IndexOf(scale, segment.Root);
            if (degree < 0) { credits -= .45 * weight; continue; }
            var accepted = triads[degree];
            // A power chord carries root evidence only, so it is credited without a third.
            credits += weight * (segment.Quality == ChordQuality.Unknown ? .85
                : accepted.Length == 0 || accepted.Contains(segment.Quality) ? 1
                : .35);
        }
        // Only ranking uses position: a loop usually starts on the tonic, and a cadence
        // usually ends on it. The first chord is the stronger cue of the two in practice.
        double bonus = 0;
        if (segments[0].Root == key.PitchClass) bonus += .45;
        if (segments[^1].Root == key.PitchClass) bonus += .25;
        return (weights <= 0 ? 0 : Math.Clamp(credits / weights, 0, 1), bonus);
    }
}
