namespace GuitarSoul.Core.Audio;

/// <summary>Local pulse and section contrast, not instrument or chorus classification.</summary>
internal sealed class MusicPulseTracker
{
    private readonly double[] _flux = new double[800];
    private readonly double[] _broadFlux = new double[800];
    private readonly double[] _recentFlux = new double[5];
    private readonly double[] _recentBroadFlux = new double[5];
    private int _recentFluxIndex;
    private int _index, _count, _ticks, _candidateLag;
    private double _previous, _previousBroad, _slow, _fast, _history, _silence, _candidateSeconds;
    private double _liftSeconds, _releaseSeconds;
    private double _interleavedSeconds;
    private bool _lift;
    private double _seedEnergy;
    private double _pluckPeak, _pluckAge = 100, _pluckEvidence;
    public double PluckedPresence { get; private set; }
    public double? Bpm { get; private set; }
    public double Confidence { get; private set; }
    public double Anchor { get; private set; }
    public double Lift { get; private set; }
    public long Cue { get; private set; }

    public void Process(double time, double rms, double threshold, double bassEnergy, bool attack, double midShare, double integratedRms)
    {
        bool active = rms >= threshold;
        _silence = active ? 0 : _silence + .01;
        if (_silence > .65) { Bpm = null; Confidence = Lift = PluckedPresence = 0; }
        if (_silence > 1.5)
        {
            Array.Clear(_flux); _count = _index = 0; Bpm = null; Confidence = 0;
            Array.Clear(_broadFlux); Array.Clear(_recentBroadFlux); _interleavedSeconds = 0;
            Array.Clear(_recentFlux); _recentFluxIndex = 0;
            _slow = _fast = _history = _candidateSeconds = Lift = Anchor = 0;
            _seedEnergy = 0;
            _lift = false; _liftSeconds = _releaseSeconds = 0;
            _pluckEvidence = PluckedPresence = 0; _pluckAge = 100;
        }
        double envelope = Math.Sqrt(Math.Max(0, bassEnergy));
        double rawFlux = active ? Math.Max(0, envelope - _previous - Math.Max(threshold * .1, envelope * .025)) : 0;
        // Keep a separate broadband onset curve. The 50 ms integrated envelope
        // suppresses carrier ripple; it only resolves an existing bass pulse's octave.
        double broadFlux = active ? Math.Max(0, integratedRms - _previousBroad - Math.Max(threshold * .1, integratedRms * .025)) : 0;
        _previousBroad = integratedRms;
        _recentFlux[_recentFluxIndex] = rawFlux;
        _recentBroadFlux[_recentFluxIndex] = broadFlux;
        _recentFluxIndex = (_recentFluxIndex + 1) % _recentFlux.Length;
        // A mixed recording spreads an accent across several 10 ms windows. Integrate
        // with a short triangular kernel instead of requiring sample-bin coincidence.
        // The causal 1,2,3,2,1 kernel has 20 ms group delay; phase fitting removes it.
        _flux[_index] = (_recentFlux[_recentFluxIndex] +
            2 * _recentFlux[(_recentFluxIndex + 1) % 5] +
            3 * _recentFlux[(_recentFluxIndex + 2) % 5] +
            2 * _recentFlux[(_recentFluxIndex + 3) % 5] +
            _recentFlux[(_recentFluxIndex + 4) % 5]) / 9;
        _broadFlux[_index] = (_recentBroadFlux[_recentFluxIndex] +
            2 * _recentBroadFlux[(_recentFluxIndex + 1) % 5] +
            3 * _recentBroadFlux[(_recentFluxIndex + 2) % 5] +
            2 * _recentBroadFlux[(_recentFluxIndex + 3) % 5] +
            _recentBroadFlux[(_recentFluxIndex + 4) % 5]) / 9;
        _previous = envelope;
        _pluckAge += .01;
        if (attack) { _pluckPeak = rms; _pluckAge = 0; }
        // A struck/picked midrange envelope is a cue shared with other instruments.
        // This score must never be presented as a guitar classification probability.
        bool plucked = active && _pluckAge is > .05 and < .18 && midShare > .35 &&
            rms < _pluckPeak * .9 && rms > _pluckPeak * .15;
        _pluckEvidence += ((plucked ? 1 : 0) - _pluckEvidence) * .025;
        PluckedPresence = Math.Clamp(_pluckEvidence * 5, 0, 1);
        if (_silence > .65) _pluckEvidence = PluckedPresence = 0;
        _index = (_index + 1) % _flux.Length; _count = Math.Min(_count + 1, _flux.Length);
        if (active || _slow > 0 && _silence <= .65)
        {
            _history += .01;
            if (_history <= 2)
            {
                _seedEnergy += rms * .01;
                _slow = _fast = _seedEnergy / _history;
            }
            _fast += (rms - _fast) * (1 - Math.Exp(-.01 / 1.5));
            double ratio = _fast / Math.Max(threshold, _slow);
            // Freeze the preceding section while the new section is stronger.
            _slow += (rms - _slow) * (1 - Math.Exp(-.01 / (ratio > 1.4 ? 30 : 8)));
            bool rising = _history > 6 && ratio > 1.55;
            _liftSeconds = rising ? _liftSeconds + .01 : Math.Max(0, _liftSeconds - .02);
            if (!_lift && _liftSeconds >= .6 && attack) { _lift = true; Cue++; }
            _releaseSeconds = ratio < 1.2 ? _releaseSeconds + .01 : 0;
            if (_releaseSeconds >= 1.2) { _lift = false; _liftSeconds = 0; }
            Lift = _lift ? Math.Clamp((ratio - 1) / 1.2, 0, 1) : 0;
        }
        if (++_ticks % 25 == 0 && _count >= 400 && _silence < .2) Estimate(time);
    }

    private double Value(int age, bool broad = false) => (broad ? _broadFlux : _flux)[(_index - 1 - age + _flux.Length) % _flux.Length];
    private double BroadbandCorrelation(int lag)
    {
        double cross = 0, a = 0, b = 0, sumX = 0, sumY = 0;
        int pairs = _count - lag;
        for (int i = 0; i < pairs; i++)
        {
            double x = Value(i, true), y = Value(i + lag, true);
            cross += x * y; a += x * x; b += y * y; sumX += x; sumY += y;
        }
        double variance = Math.Max(0, a - sumX * sumX / pairs) * Math.Max(0, b - sumY * sumY / pairs);
        double centered = variance > 1e-18 ? (cross - sumX * sumY / pairs) / Math.Sqrt(variance) : 0;
        return centered >= .25 && a * b > 1e-18 ? cross / Math.Sqrt(a * b) : 0;
    }
    private void Estimate(double time)
    {
        double best = 0; int bestLag = 0;
        Span<double> scores = stackalloc double[102];
        scores.Clear();
        for (int lag = 30; lag <= 100; lag++) // 60–200 BPM, with octave ambiguity.
        {
            double cross = 0, a = 0, b = 0, sumX = 0, sumY = 0;
            int pairs = _count - lag;
            for (int i = 0; i < _count - lag; i++)
            {
                double x = Value(i), y = Value(i + lag);
                cross += x * y; a += x * x; b += y * y;
                sumX += x; sumY += y;
            }
            // Smoothing raises a positive novelty floor even in unrelated noise.
            // Require periodic variation beyond that floor before admitting a beat.
            double centeredA = Math.Max(0, a - sumX * sumX / pairs);
            double centeredB = Math.Max(0, b - sumY * sumY / pairs);
            double centered = centeredA * centeredB > 1e-18
                ? (cross - sumX * sumY / pairs) / Math.Sqrt(centeredA * centeredB) : 0;
            if (centered < .25) continue;
            double score = a * b > 1e-18 ? cross / Math.Sqrt(a * b) : 0;
            // Prefer a plausible tactus when equally strong subdivisions exist.
            score *= .8 + .2 * Math.Exp(-Math.Pow(Math.Log((6000d / lag) / 110), 2) / .5);
            scores[lag] = score;
            if (score > best) { best = score; bestLag = lag; }
        }
        int bassLag = bestLag;
        bool interleaved = false;
        double fullScore = 0;
        if (best >= .6 && bestLag >= 60)
        {
            for (int lag = Math.Max(30, bestLag / 2 - 1); lag <= bestLag / 2 + 1; lag++)
            {
                double score = BroadbandCorrelation(lag);
                fullScore = Math.Max(fullScore, score);
            }
            // Equal interleaved accents support the shorter period. Weak subdivisions
            // correlate much better at the longer period and must not double it.
            bool established = Bpm is double bpm && Confidence >= .6 && Math.Abs(bestLag / 2d - 6000 / bpm) <= 2;
            interleaved = fullScore > 0 && fullScore >= BroadbandCorrelation(bestLag) * (established ? .8 : .9);
        }
        _interleavedSeconds = interleaved ? _interleavedSeconds + .25 : 0;
        bool useInterleavedBeat = interleaved && _interleavedSeconds >= 1;
        if (useInterleavedBeat)
        {
            bestLag = (int)Math.Round(bassLag / 2d);
        }
        if (!useInterleavedBeat && Bpm is double previousBpm && Confidence >= .6 && bestLag > 0)
        {
            double previousLag = 6000 / previousBpm;
            double ratio = bestLag / previousLag;
            if (ratio is > 1.85 and < 2.15 or > .46 and < .54)
            {
                // Alternating accent strengths can make every other beat briefly
                // win. Keep a still-strong established tactus through that octave
                // ambiguity; a fading or clearly weaker pulse is allowed to yield.
                int around = (int)Math.Round(previousLag), retainedLag = 0;
                double retainedScore = 0;
                for (int lag = Math.Max(30, around - 1); lag <= Math.Min(100, around + 1); lag++)
                    if (scores[lag] > retainedScore) { retainedScore = scores[lag]; retainedLag = lag; }
                if (retainedScore >= .6 && retainedScore >= best * .9)
                {
                    best = retainedScore;
                    bestLag = retainedLag;
                }
            }
        }
        if (best < .5) { Confidence *= .8; if (Confidence < .4) Bpm = null; return; }
        if (Math.Abs(bestLag - _candidateLag) > 2) { _candidateLag = bestLag; _candidateSeconds = 0; }
        else _candidateSeconds += .25;
        if (_candidateSeconds < .5) return;
        // Ten-millisecond integer lags are almost four BPM apart near150 BPM.
        // Refine the local correlation peak without widening the admitted range.
        // Broadband evidence selects the octave, while the established bass period
        // supplies timing precision. Incidental treble accents cannot detune the clock.
        int timingLag = useInterleavedBeat ? bassLag : bestLag;
        double refinedLag = timingLag;
        if (timingLag is > 30 and < 100 && scores[timingLag - 1] > 0 && scores[timingLag + 1] > 0)
        {
            double curvature = scores[timingLag - 1] - 2 * best + scores[timingLag + 1];
            if (curvature < -1e-12)
                refinedLag += Math.Clamp(.5 * (scores[timingLag - 1] - scores[timingLag + 1]) / curvature, -.5, .5);
        }
        if (useInterleavedBeat) refinedLag /= 2;
        double nextBpm = 6000d / refinedLag;
        bool continuing = Bpm.HasValue && Confidence >= .6;
        if (continuing)
        {
            // Changing period with an old absolute origin creates an ever-larger
            // phase jump late in a song. Preserve the current beat number first,
            // then let the bounded acoustic phase correction follow the accents.
            double beatNumber = (time - Anchor) * Bpm!.Value / 60;
            Anchor = time - beatNumber * 60 / nextBpm;
        }
        Bpm = nextBpm; Confidence = best;
        // Fit phase to all recent accents, not the last incidental guitar pluck.
        int phase = 0; double strength = -1;
        for (int offset = 0; offset < bestLag; offset++)
        {
            double sum = 0;
            for (int age = offset; age < Math.Min(_count, 400); age += bestLag)
                sum += Value(age) + (age > 0 ? Value(age - 1) * .5 : 0);
            if (sum > strength) { strength = sum; phase = offset; }
        }
        double fitted = time - phase * .01 - .02;
        if (!continuing) Anchor = fitted;
        else
        {
            double period = 60 / Bpm.Value;
            double predicted = Anchor + Math.Round((fitted - Anchor) / period) * period;
            // Keep the original beat-number origin so left/right paws alternate across updates.
            Anchor += Math.Clamp(fitted - predicted, -.03, .03);
        }
    }
}
