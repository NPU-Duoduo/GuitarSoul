using System.Numerics;

namespace GuitarSoul.Core.Audio;

public enum ChordQuality { Unknown, Major, Minor }

/// <summary>Conservative triad hypothesis, not instrument identity, key, emotion or correctness probability.</summary>
public readonly record struct HarmonyFrame(int Root, ChordQuality Quality, double Strength, long ChangeId)
{
    public bool IsKnown => Quality != ChordQuality.Unknown;
    /// <summary>Root inferred from root plus fifth alone. Quality is deliberately not claimed.</summary>
    public bool RootOnly { get; init; }
    /// <summary>True when at least the root is evidenced, with or without a third.</summary>
    public bool HasRoot => IsKnown || RootOnly;
    public int PitchClass => Root;
}

/// <summary>
/// Causal spectral-peak chroma and triad templates. A bounded sample ring is analysis memory only.
/// Stable changes, not framewise chord labels, are consumed by the companion.
/// </summary>
internal sealed class HarmonyTracker
{
    private const int Size = 4096;
    private readonly double[] _ring = new double[Size], _hann = new double[Size];
    private readonly Complex[] _fft = new Complex[Size];
    private readonly double[] _power = new double[Size / 2], _chroma = new double[12], _phrase = new double[12];
    private readonly List<(double Frequency, double Power)> _peaks = new(128);
    private readonly int _stride, _hop;
    private readonly double _rate, _filter, _phraseDecay;
    private int _decimation, _index, _count, _since, _candidate = -1, _stable = -1, _candidateFrames;
    private int _rootCandidate = -1, _rootStable = -1, _rootCandidateFrames;
    private int _phraseCandidate = -1, _phraseFrames;
    private double _low1, _low2, _invalidSeconds, _phraseSilence;
    private long _changeId;
    public HarmonyFrame Current { get; private set; }
    /// <summary>
    /// Chord outlined across a phrase rather than struck at once. A broken chord never has
    /// all three tones in one 512 ms window, so this accumulates pitch classes over ~1.2 s.
    /// </summary>
    public HarmonyFrame Phrase { get; private set; }

    public HarmonyTracker(int sampleRate)
    {
        _stride = Math.Max(1, sampleRate / 8000);
        _rate = sampleRate / (double)_stride;
        _hop = (int)Math.Round(_rate * .08);
        _phraseDecay = Math.Exp(-(_hop / _rate) / 1.2);
        _filter = 1 - Math.Exp(-2 * Math.PI * 1600 / sampleRate);
        for (int i = 0; i < Size; i++) _hann[i] = .5 - .5 * Math.Cos(2 * Math.PI * i / (Size - 1));
    }

    public void Process(float sample, double threshold)
    {
        _low1 += _filter * (sample - _low1);
        _low2 += _filter * (_low1 - _low2);
        if (++_decimation < _stride) return;
        _decimation = 0;
        _ring[_index] = _low2;
        _index = (_index + 1) % Size;
        _count = Math.Min(Size, _count + 1);
        if (++_since < _hop) return;
        _since = 0;
        if (_count < Size) return;
        Analyze(threshold);
    }

    private void Analyze(double threshold)
    {
        double energy = 0;
        for (int i = 0; i < Size; i++)
        {
            double x = _ring[(_index + i) % Size];
            energy += x * x;
            _fft[i] = new(x * _hann[i], 0);
        }
        if (energy / Size < threshold * threshold) { DecayPhrase(); Observe(-1, 0, -1); return; }
        Transform(_fft);
        for (int i = 0; i < _power.Length; i++) _power[i] = _fft[i].Magnitude * _fft[i].Magnitude;
        int low = Math.Max(2, (int)(75 * Size / _rate)), high = (int)(1500 * Size / _rate);
        double total = 0, peakTotal = 0, maximum = 0;
        for (int i = low; i <= high; i++) { total += _power[i]; maximum = Math.Max(maximum, _power[i]); }
        _peaks.Clear();
        Array.Clear(_chroma);
        for (int i = low; i <= high; i++)
        {
            if (_power[i] < maximum * .012 || _power[i] <= _power[i - 1] || _power[i] <= _power[i + 1]) continue;
            double a = Math.Log(Math.Max(1e-30, _power[i - 1])), b = Math.Log(Math.Max(1e-30, _power[i])), c = Math.Log(Math.Max(1e-30, _power[i + 1]));
            double offset = Math.Clamp(.5 * (a - c) / (a - 2 * b + c), -.5, .5);
            double f = (i + offset) * _rate / Size;
            peakTotal += _power[i - 1] + _power[i] + _power[i + 1];
            _peaks.Add((f, _power[i]));
        }
        // Broadband transients must not become a three-note explanation of noise.
        if (total <= 1e-20 || peakTotal / total < .50) { DecayPhrase(); Observe(-1, 0, -1); return; }
        foreach (var peak in _peaks)
        {
            // A single rich guitar note has fifth/third harmonics too. Remove peaks
            // explained by a stronger lower fundamental before fitting a triad.
            bool overtone = _peaks.Any(lower => lower.Frequency < peak.Frequency * .7 &&
                lower.Power >= peak.Power * .7 && Math.Round(peak.Frequency / lower.Frequency) is >= 2 and <= 8 &&
                Math.Abs(12 * Math.Log2(peak.Frequency / lower.Frequency / Math.Round(peak.Frequency / lower.Frequency))) < .28);
            if (overtone) continue;
            double midi = 69 + 12 * Math.Log2(peak.Frequency / 440);
            int note = (int)Math.Round(midi);
            if (Math.Abs(midi - note) <= .32) _chroma[(note % 12 + 12) % 12] += Math.Sqrt(peak.Power);
        }
        double norm = Math.Sqrt(_chroma.Sum(x => x * x)), max = _chroma.Max();
        AccumulatePhrase();
        double best = 0, runner = 0; int winner = -1;
        for (int chord = 0; chord < 24; chord++)
        {
            int root = chord % 12, third = (root + (chord < 12 ? 4 : 3)) % 12, fifth = (root + 7) % 12;
            if (max <= 0 || Math.Min(_chroma[root], Math.Min(_chroma[third], _chroma[fifth])) < max * .22) continue;
            double score = (_chroma[root] + _chroma[third] + _chroma[fifth]) / (Math.Sqrt(3) * norm);
            if (score > best) { runner = best; best = score; winner = chord; }
            else runner = Math.Max(runner, score);
        }
        Observe(best >= .86 && best - runner >= .08 ? winner : -1, best,
            best >= .86 && best - runner >= .08 ? -1 : PowerRoot(norm));
    }

    /// <summary>
    /// Root plus fifth with no usable third. A single note cannot reach this: its own third
    /// harmonic is removed as an overtone before chroma is summed.
    /// </summary>
    private int PowerRoot(double norm)
    {
        if (norm <= 0) return -1;
        double max = _chroma.Max();
        double best = 0, runner = 0;
        int winner = -1;
        for (int root = 0; root < 12; root++)
        {
            double rootPower = _chroma[root], fifthPower = _chroma[(root + 7) % 12];
            if (rootPower < max * .40 || fifthPower < max * .30) continue;
            double score = (rootPower + fifthPower) / (Math.Sqrt(2) * norm);
            if (score > best) { runner = best; best = score; winner = root; }
            else runner = Math.Max(runner, score);
        }
        return best >= .78 && best - runner >= .05 ? winner : -1;
    }

    private void DecayPhrase()
    {
        for (int i = 0; i < 12; i++) _phrase[i] *= _phraseDecay;
        _phraseSilence += _hop / _rate;
        // An exponential tail never reaches zero; without this the last chord outlives the player.
        if (_phraseSilence >= 1.5)
        {
            Array.Clear(_phrase);
            _phraseCandidate = -1; _phraseFrames = 0;
            Phrase = default;
            return;
        }
        TrackPhrase();
    }

    private void AccumulatePhrase()
    {
        _phraseSilence = 0;
        for (int i = 0; i < 12; i++) _phrase[i] = _phrase[i] * _phraseDecay + _chroma[i];
        TrackPhrase();
    }

    /// <summary>
    /// Fits a triad to pitch classes gathered across the phrase. A scale run does not fit
    /// three bins well, so it is rejected rather than reported as a chord.
    /// </summary>
    private void TrackPhrase()
    {
        double total = 0, sum = 0;
        for (int i = 0; i < 12; i++) { total += _phrase[i] * _phrase[i]; sum += _phrase[i]; }
        if (total <= 1e-9 || sum <= 0)
        {
            _phraseCandidate = -1; _phraseFrames = 0; Phrase = default;
            return;
        }
        double norm = Math.Sqrt(total);
        double best = 0, runner = 0;
        int winner = -1;
        for (int chord = 0; chord < 24; chord++)
        {
            int root = chord % 12, third = (root + (chord < 12 ? 4 : 3)) % 12, fifth = (root + 7) % 12;
            double score = (_phrase[root] + _phrase[third] + _phrase[fifth]) / (Math.Sqrt(3) * norm);
            if (score > best) { runner = best; best = score; winner = chord; }
            else runner = Math.Max(runner, score);
        }
        double explained = 0;
        if (winner >= 0)
        {
            int root = winner % 12, third = (root + (winner < 12 ? 4 : 3)) % 12, fifth = (root + 7) % 12;
            explained = (_phrase[root] + _phrase[third] + _phrase[fifth]) / sum;
        }
        // Accumulating more time does not resolve competing chord explanations. Keep the
        // same separation requirement as the short window instead of overriding its rejection.
        int candidate = best >= .82 && explained >= .55 && best - runner >= .08 ? winner : -1;
        if (candidate != _phraseCandidate) { _phraseCandidate = candidate; _phraseFrames = 0; }
        if (++_phraseFrames < 2) return;
        Phrase = candidate < 0
            ? default
            : new(candidate % 12, candidate < 12 ? ChordQuality.Major : ChordQuality.Minor, best, 0);
    }

    private void Observe(int chord, double strength, int rootOnly)
    {
        if (chord < 0 && rootOnly < 0)
        {
            _candidate = -1; _candidateFrames = 0;
            _rootCandidate = -1; _rootCandidateFrames = 0;
            _invalidSeconds += _hop / _rate;
            Current = new(0, ChordQuality.Unknown, 0, _changeId);
            if (_invalidSeconds >= .65) { _stable = -1; _rootStable = -1; }
            return;
        }
        _invalidSeconds = 0;
        if (chord >= 0)
        {
            _rootCandidate = -1; _rootCandidateFrames = 0; _rootStable = -1;
            if (_candidate != chord) { _candidate = chord; _candidateFrames = 0; }
            if (++_candidateFrames < 3)
            {
                Current = new(0, ChordQuality.Unknown, 0, _changeId);
                return;
            }
            if (_stable != chord)
            {
                if (_stable >= 0) _changeId++;
                _stable = chord;
            }
            Current = new(chord % 12, chord < 12 ? ChordQuality.Major : ChordQuality.Minor, strength, _changeId);
            return;
        }
        // Root-only evidence. The companion keeps ignoring it because IsKnown stays false.
        _candidate = -1; _candidateFrames = 0;
        if (_rootCandidate != rootOnly) { _rootCandidate = rootOnly; _rootCandidateFrames = 0; }
        if (++_rootCandidateFrames < 3)
        {
            Current = new(0, ChordQuality.Unknown, 0, _changeId);
            return;
        }
        _rootStable = rootOnly;
        Current = new(rootOnly, ChordQuality.Unknown, strength, _changeId) { RootOnly = true };
    }

    private static void Transform(Complex[] values)
    {
        for (int i = 1, j = 0; i < values.Length; i++)
        {
            int bit = values.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (values[i], values[j]) = (values[j], values[i]);
        }
        for (int width = 2; width <= values.Length; width <<= 1)
        {
            var rotation = Complex.FromPolarCoordinates(1, -2 * Math.PI / width);
            for (int start = 0; start < values.Length; start += width)
            {
                Complex w = Complex.One;
                for (int i = 0; i < width / 2; i++)
                {
                    var even = values[start + i]; var odd = values[start + i + width / 2] * w;
                    values[start + i] = even + odd; values[start + i + width / 2] = even - odd;
                    w *= rotation;
                }
            }
        }
    }
}
