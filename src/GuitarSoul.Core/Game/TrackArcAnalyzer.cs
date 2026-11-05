using System.Security.Cryptography;
using System.Text;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Game;

public sealed record TrackArcFrame(double Seconds, double EnergyDb, double RelativeEnergy,
    double OnsetsPerSecond, double SectionContrast, double? BeatBpm, double BeatConfidence, double BeatAnchorSeconds);

public sealed record TrackHighlight(string HighlightId, double BuildingStart, double Start,
    double End, double ReleaseEnd, string Evidence, double Salience);

public sealed record ArcCandidate(double Start, double End, double Salience, string Evidence, string Decision);

/// <summary>Immutable track-clock intervals. No chorus, style or quality labels.</summary>
public sealed class TrackArc
{
    public string TrackId { get; }
    public double DurationSeconds { get; }
    public IReadOnlyList<TrackArcFrame> Frames { get; }
    public IReadOnlyList<TrackHighlight> Highlights { get; }
    public IReadOnlyList<ArcCandidate> Candidates { get; }
    public TrackArc(string trackId, double durationSeconds, IEnumerable<TrackArcFrame> frames,
        IEnumerable<TrackHighlight> highlights, IEnumerable<ArcCandidate>? candidates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackId);
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        TrackId = trackId; DurationSeconds = durationSeconds;
        var ordered = highlights.OrderBy(h => h.Start).ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        double previousEnd = 0;
        foreach (var h in ordered)
        {
            if (string.IsNullOrWhiteSpace(h.HighlightId) || !ids.Add(h.HighlightId) ||
                !new[] { h.BuildingStart, h.Start, h.End, h.ReleaseEnd, h.Salience }.All(double.IsFinite) ||
                h.BuildingStart < previousEnd || h.Start < h.BuildingStart || h.End <= h.Start ||
                h.End - h.Start > 16 || h.ReleaseEnd < h.End || h.ReleaseEnd > durationSeconds)
                throw new ArgumentException("Invalid, duplicate or overlapping highlight intervals.", nameof(highlights));
            previousEnd = h.ReleaseEnd;
        }
        var sampled = frames.ToArray();
        if (sampled.Any(f => !new[] { f.Seconds, f.EnergyDb, f.RelativeEnergy, f.OnsetsPerSecond,
            f.SectionContrast, f.BeatConfidence, f.BeatAnchorSeconds }.All(double.IsFinite) ||
            f.Seconds < 0 || f.Seconds >= durationSeconds || f.BeatConfidence is < 0 or > 1 ||
            f.BeatBpm is double bpm && (!double.IsFinite(bpm) || bpm <= 0)) ||
            sampled.Zip(sampled.Skip(1)).Any(p => p.First.Seconds >= p.Second.Seconds))
            throw new ArgumentException("Invalid track feature sequence.", nameof(frames));
        Frames = Array.AsReadOnly(sampled); Highlights = Array.AsReadOnly(ordered);
        Candidates = Array.AsReadOnly((candidates ?? []).ToArray());
    }

    public TrackArcFrame? FrameAt(double position)
    {
        // Binary search also supports diagnostic timelines with nonuniform samples.
        int lo = 0, hi = Frames.Count - 1;
        while (lo <= hi) { int mid = (lo + hi) / 2; if (Frames[mid].Seconds <= position) lo = mid + 1; else hi = mid - 1; }
        return hi < 0 ? null : Frames[hi];
    }
}

/// <summary>
/// Offline, cancellable relative-energy / articulation proposal. Versioned deterministic rules,
/// not a learned song-structure model. Call on a background worker; no device or UI dependencies.
/// </summary>
public sealed class TrackArcAnalyzer
{
    public const string AlgorithmVersion = "relative-arc-002";
    public TrackArc Analyze(float[] mono, int sampleRate, string trackId, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(mono);
        if (sampleRate is < 8000 or > 192000 || mono.Length == 0 || mono.Length / (double)sampleRate > 900)
            throw new ArgumentException("Expected nonempty mono PCM, 8–192 kHz, at most 15 minutes.");
        if (mono.Any(s => !float.IsFinite(s))) throw new ArgumentException("Non-finite PCM.", nameof(mono));
        cancellation.ThrowIfCancellationRequested();
        double duration = mono.Length / (double)sampleRate;
        double peak = mono.Max(s => Math.Abs((double)s));
        double threshold = Math.Clamp(20 * Math.Log10(Math.Max(1e-6, peak * .003)), -100, -20);
        var rhythm = new RhythmTracker(sampleRate);
        var raw = new List<(double Time, double Db, double Density, RhythmFrame Rhythm)>();
        int window = sampleRate / 2;
        long previousOnsets = 0;
        for (int offset = 0; offset < mono.Length; offset += window)
        {
            cancellation.ThrowIfCancellationRequested();
            int count = Math.Min(window, mono.Length - offset);
            double square = 0;
            for (int i = offset; i < offset + count; i++) square += mono[i] * (double)mono[i];
            var r = rhythm.Process(mono.AsSpan(offset, count), threshold);
            raw.Add((offset / (double)sampleRate, 10 * Math.Log10(Math.Max(1e-12, square / count)),
                (r.OnsetCount - previousOnsets) * sampleRate / (double)count, r));
            previousOnsets = r.OnsetCount;
        }
        double[] rawEnergy = raw.Select(x => x.Db).ToArray(), rawDensity = raw.Select(x => x.Density).ToArray();
        double[] energy = raw.Select((_, i) => Mean(rawEnergy, i - 1, i + 2)).ToArray();
        double[] density = raw.Select((_, i) => Mean(rawDensity, i - 2, i + 3)).ToArray();
        double low = Quantile(energy, .1), high = Quantile(energy, .9), span = high - low;
        double dl = Quantile(density, .1), dh = Quantile(density, .9), ds = dh - dl;
        double[] relative = energy.Select(e => span < 3 ? 0 : Math.Clamp((e - low) / span, 0, 1)).ToArray();
        double[] articulation = density.Select(d => ds < .75 ? 0 : Math.Clamp((d - dl) / ds, 0, 1)).ToArray();
        var frames = raw.Select((r, i) => new TrackArcFrame(r.Time, energy[i], relative[i], density[i],
            Math.Max(relative[i] - Mean(relative, i - 16, i - 4),
                     articulation[i] - Mean(articulation, i - 16, i - 4)),
            r.Rhythm.BeatBpm, r.Rhythm.BeatConfidence, r.Rhythm.BeatAnchorSeconds)).ToArray();
        var proposed = new List<ArcCandidate>();
        for (int i = 6; i < frames.Length - 12; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            // Require a within-song contrast; a constant loud track has no candidate.
            double contrast = frames[i].SectionContrast;
            bool energyLift = relative[i] >= .65 && contrast >= .25 && energy[i] - low >= 3;
            bool densityLift = articulation[i] >= .8 && contrast >= .4 && ds >= 1;
            if (!energyLift && !densityLift) continue;
            // Look ahead within the complete recording to separate a rising ramp from its arrival.
            bool stillBuilding = energyLift && !densityLift && Mean(relative, i + 1, i + 5) - relative[i] > .04;
            int end = i + 1;
            while (end < frames.Length && end - i < 32 &&
                (energyLift ? relative[end] >= .45 : articulation[end] >= .5)) end++;
            double seconds = (end - i) * .5;
            string reason = densityLift && !energyLift ? "articulation-contrast" :
                relative[i] - relative[Math.Max(0, i - 4)] >= .35 ? "sudden-energy-lift" : "gradual-energy-lift";
            double score = .6 * contrast + .25 * relative[i] + .15 * articulation[i];
            proposed.Add(new(raw[i].Time, Math.Min(duration, raw[i].Time + seconds), score, reason,
                stillBuilding ? "rejected-still-building" : seconds < 6 ? "rejected-shorter-than-6s" : "eligible"));
        }
        int budget = Math.Clamp((int)Math.Ceiling(duration / 60), 1, 4);
        var chosen = new List<ArcCandidate>();
        foreach (var candidate in proposed.Where(c => c.Decision == "eligible").OrderByDescending(c => c.Salience).ThenBy(c => c.Start))
        {
            if (chosen.Count >= budget || chosen.Any(c => candidate.Start < c.End + 12 && candidate.End > c.Start - 12)) continue;
            chosen.Add(candidate);
        }
        var highlights = chosen.OrderBy(c => c.Start).Select(c => new TrackHighlight(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(FormattableString.Invariant(
                $"{AlgorithmVersion}|{trackId}|{c.Start:F3}|{c.End:F3}"))))[..16],
            Math.Max(0, c.Start - 3), c.Start, c.End, Math.Min(duration, c.End + 1), c.Evidence, c.Salience));
        return new TrackArc(trackId, duration, frames, highlights,
            proposed.Select(c => c with { Decision = chosen.Contains(c) ? "selected" : c.Decision == "eligible" ? "rejected-spacing-or-budget" : c.Decision }));
    }

    private static double Mean(double[] values, int start, int end)
    {
        start = Math.Clamp(start, 0, values.Length); end = Math.Clamp(end, 0, values.Length);
        double sum = 0;
        for (int i = start; i < end; i++) sum += values[i];
        return end > start ? sum / (end - start) : 0;
    }
    private static double Quantile(double[] values, double q)
    {
        var ordered = values.Order().ToArray();
        return ordered[(int)Math.Round((ordered.Length - 1) * q)];
    }
}
