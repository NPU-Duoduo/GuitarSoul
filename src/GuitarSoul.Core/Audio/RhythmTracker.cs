namespace GuitarSoul.Core.Audio;

public readonly record struct RhythmFrame(double AudioSeconds, double LastOnsetSeconds,
    long OnsetCount, double? TempoBpm, double Confidence)
{
    public static RhythmFrame Empty => new(0, -100, 0, null, 0);
    public double Brightness { get; init; }
    public double Density { get; init; }
    public double Drive { get; init; }
    public ListeningFeatures Listening { get; init; }
    public double? BeatBpm { get; init; }
    public double BeatConfidence { get; init; }
    public double BeatAnchorSeconds { get; init; }
}

/// <summary>
/// Streaming articulation detector with 10 ms attacks and 50 ms acoustic contrasts.
/// Tempo describes repeated attacks, not a song's metrical downbeat.
/// Harmonic context uses a separate causal FFT window; no wall-clock or UI timing is required.
/// </summary>
public sealed class RhythmTracker
{
    private readonly int _sampleRate;
    private readonly int _windowSize;
    private readonly Queue<double> _intervals = new();
    private int _windowCount;
    private long _sampleCount;
    private double _windowEnergy;
    private double _previousRms;
    private double _baseline;
    private double _lastOnset = -100;
    private long _onsets;
    private double? _tempo;
    private double _confidence;
    private readonly double[] _lowPass = new double[5], _bands = new double[6], _previousBands = new double[6];
    private readonly double[] _changeBands = new double[6], _changeShares = new double[6];
    private readonly double[] _windowEnergies = new double[5], _referenceRms = new double[5];
    private readonly double[][] _windowBands = Enumerable.Range(0, 5).Select(_ => new double[6]).ToArray();
    private readonly double[][] _referenceShares = Enumerable.Range(0, 5).Select(_ => new double[6]).ToArray();
    private readonly double[] _coefficients;
    private readonly Queue<double> _recentAttacks = new();
    private double _peak, _brightness, _density, _drive;
    private double _changeRms, _spectralFloor = .01, _energyChangeFloor = .02;
    private int _windowIndex, _referenceIndex, _contrastWindows, _changeWindows;
    private readonly ListeningFeatureTracker _listening = new();
    private readonly MusicPulseTracker _music = new();
    private readonly HarmonyTracker _harmony;

    public RhythmTracker(int sampleRate)
    {
        if (sampleRate is < 8000 or > 384000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        _sampleRate = sampleRate;
        _harmony = new(sampleRate);
        _windowSize = sampleRate / 100;
        _coefficients = new[] { 160d, 400, 1000, 2400, 5000 }
            .Select(f => 1 - Math.Exp(-2 * Math.PI * Math.Min(f, sampleRate * .4) / sampleRate)).ToArray();
    }

    public RhythmFrame Process(ReadOnlySpan<float> samples, double activityThresholdDb = -45)
    {
        if (!double.IsFinite(activityThresholdDb) || activityThresholdDb is < -120 or > 0)
            throw new ArgumentOutOfRangeException(nameof(activityThresholdDb));
        double threshold = Math.Pow(10, activityThresholdDb / 20);
        foreach (var sample in samples)
        {
            var value = float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
            _harmony.Process(value, threshold);
            _sampleCount++;
            _windowEnergy += value * value;
            _peak = Math.Max(_peak, Math.Abs(value));
            double previous = 0;
            for (int band = 0; band < _lowPass.Length; band++)
            {
                _lowPass[band] += _coefficients[band] * (value - _lowPass[band]);
                double part = _lowPass[band] - previous;
                _bands[band] += part * part;
                previous = _lowPass[band];
            }
            _bands[5] += (value - previous) * (value - previous);
            if (++_windowCount < _windowSize) continue;
            var rms = Math.Sqrt(_windowEnergy / _windowCount);
            var time = _sampleCount / (double)_sampleRate;
            double total = _bands.Sum(), novelty = 0;
            for (int band = 0; band < _bands.Length; band++)
            {
                double share = total > 1e-15 ? _bands[band] / total : 0;
                novelty += Math.Max(0, share - _previousBands[band]);
                _previousBands[band] = share;
                _windowBands[_windowIndex][band] = _bands[band];
            }
            _brightness = total > 1e-15 ? (_bands[3] + _bands[4] + _bands[5]) / total : 0;
            _density = _peak > 0 ? Math.Clamp(rms / _peak, 0, 1) : 0;
            // Sustains do not retrigger. The local rise and baseline ratio must both pass.
            bool energyAttack = rms > _baseline * 1.55 &&
                rms - _previousRms > Math.Max(threshold * .3, _baseline * .25);
            // Band-share change detects compressed/legato notes without requiring a volume jump.
            // Normalized shares keep equivalent tones independent of input gain.
            bool toneAttack = novelty > .22 && rms > _baseline * .7 && _previousRms >= threshold;
            // A 50 ms window slides every 10 ms, so a compressed pick need not align
            // with a callback or analysis boundary. Integration reduces held-tone phase variation.
            _windowEnergies[_windowIndex] = _windowEnergy;
            _windowIndex = (_windowIndex + 1) % _windowEnergies.Length;
            _contrastWindows = Math.Min(_windowEnergies.Length, _contrastWindows + 1);
            bool subtleAttack = false;
            if (_contrastWindows == _windowEnergies.Length)
            {
                Array.Clear(_changeBands);
                foreach (var window in _windowBands)
                    for (int band = 0; band < _changeBands.Length; band++)
                        _changeBands[band] += window[band];
                double changeTotal = _changeBands.Sum();
                double changeRms = Math.Sqrt(_windowEnergies.Sum() / (_windowSize * _windowEnergies.Length));
                double referenceRms = _referenceRms[_referenceIndex];
                double stepSpectral = 0, spectralChange = 0;
                for (int band = 0; band < _bands.Length; band++)
                {
                    double share = changeTotal > 1e-15 ? _changeBands[band] / changeTotal : 0;
                    stepSpectral += Math.Max(0, share - _changeShares[band]);
                    spectralChange += Math.Max(0, share - _referenceShares[_referenceIndex][band]);
                    _changeShares[band] = share;
                    _referenceShares[_referenceIndex][band] = share;
                }
                double relativeRise = referenceRms >= threshold ? changeRms / referenceRms - 1 : 0;
                double relativeStep = _changeRms >= threshold ? changeRms / _changeRms - 1 : 0;
                bool comparable = referenceRms >= threshold && changeRms >= threshold && _changeWindows >= 75;
                subtleAttack = comparable &&
                    (relativeRise > Math.Max(.045, _energyChangeFloor * 8) ||
                        spectralChange > Math.Max(.01, _spectralFloor * 8));
                // Estimate background at the shorter 10 ms step, excluding a pick's
                // immediate tail. Dense 50 ms attacks must not become their own floor.
                if (time - _lastOnset > .1)
                {
                    _energyChangeFloor += (Math.Max(0, -relativeStep) - _energyChangeFloor) * .03;
                    if (relativeStep <= 0)
                        _spectralFloor += (stepSpectral - _spectralFloor) * .03;
                }
                _changeRms = changeRms;
                _referenceRms[_referenceIndex] = changeRms;
                _referenceIndex = (_referenceIndex + 1) % _referenceRms.Length;
                _changeWindows = Math.Min(75, _changeWindows + 1);
            }
            bool heardAttack = false;
            if (rms >= threshold && (energyAttack || toneAttack || subtleAttack) &&
                time - _lastOnset >= .18)
            {
                double interval = time - _lastOnset;
                if (interval is >= .25 and <= 1.5)
                {
                    _intervals.Enqueue(interval);
                    while (_intervals.Count > 7) _intervals.Dequeue();
                    var ordered = _intervals.Order().ToArray();
                    double median = ordered[ordered.Length / 2];
                    double deviation = ordered.Average(x => Math.Abs(x - median)) / median;
                    _confidence = Math.Clamp(1 - deviation * 5, 0, 1) * Math.Min(1, ordered.Length / 4d);
                    _tempo = ordered.Length >= 3 && _confidence >= .6 ? 60 / median : null;
                }
                else
                {
                    _intervals.Clear();
                    _confidence = 0;
                    _tempo = null;
                }
                _lastOnset = time;
                _onsets++;
                _recentAttacks.Enqueue(time);
                heardAttack = true;
            }
            _listening.Process(time, _windowCount / (double)_sampleRate, rms, threshold, _brightness, heardAttack);
            _music.Process(time, rms, threshold, (_changeBands[0] + _changeBands[1]) / (_windowCount * 5d), heardAttack,
                total > 1e-15 ? (_bands[1] + _bands[2] + _bands[3]) / total : 0, _changeRms);
            _baseline += (rms - _baseline) * .12;
            _previousRms = rms;
            _windowCount = 0;
            _windowEnergy = 0;
            _peak = 0;
            Array.Clear(_bands);
            while (_recentAttacks.TryPeek(out var attack) && time - attack > 3) _recentAttacks.Dequeue();
            // Timbre changes modulate an articulation-led response, never a volume score.
            _drive = rms < threshold || time - _lastOnset > .9 ? 0 :
                Math.Clamp((_recentAttacks.Count - 1) / 6d, 0, 1) * (.8 + .1 * _brightness + .1 * _density);
            if (time - _lastOnset > 2)
            {
                _intervals.Clear();
                _tempo = null;
                _confidence = 0;
            }
        }
        return new(_sampleCount / (double)_sampleRate, _lastOnset, _onsets, _tempo, _confidence)
            { Brightness = _brightness, Density = _density, Drive = _drive,
                BeatBpm = _music.Bpm, BeatConfidence = _music.Confidence, BeatAnchorSeconds = _music.Anchor,
                Listening = _listening.Current with { SectionLift = _music.Lift, SectionCue = _music.Cue,
                    PluckedPresence = _music.PluckedPresence, Harmony = _harmony.Current, Phrase = _harmony.Phrase } };
    }
}
