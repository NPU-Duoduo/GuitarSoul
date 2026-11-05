using GuitarSoul.Core.Audio;

/// <summary>Acoustic contrasts that do not depend on genre labels or an input device.</summary>
internal static class AcousticContrastChecks
{
    private sealed record Result(RhythmFrame Frame, double SustainedShare, bool HeardSound);

    public static string DiagnosticReport()
    {
        var rows = new List<string>();
        foreach (int rate in new[] { 8000, 44100 })
        {
            rows.Add($"{rate} compressed: {Describe(Measure(rate, 12, .2, CompressedPlucks))}");
            rows.Add($"{rate} adjacent: {Describe(Measure(rate, 12, .2, AdjacentPitch))}");
            rows.Add($"{rate} mixed: {Describe(Measure(rate, 12, .3, MixedHits))}");
            foreach (double interval in new[] { .2, .25, .3, 1d / 3 })
            {
                rows.Add($"{rate} {1 / interval:F2}Hz compressed: {Describe(Measure(rate, 12, .2, t => CompressedPlucks(t, interval)))}");
                rows.Add($"{rate} {1 / interval:F2}Hz adjacent: {Describe(Measure(rate, 12, .2, t => AdjacentPitch(t, interval)))}");
                rows.Add($"{rate} {1 / interval:F2}Hz mixed: {Describe(Measure(rate, 12, .3, t => MixedHits(t, interval)))}");
            }
            rows.Add($"{rate} chord: {Describe(Measure(rate, 12, .3, t => Math.Tanh(2 *
                (.26 * Math.Sin(2 * Math.PI * 220 * t) + .24 * Math.Sin(2 * Math.PI * 247 * t) +
                    .15 * Math.Sin(2 * Math.PI * 329.63 * t)))))}");
        }
        return string.Join(Environment.NewLine, rows);
    }

    public static void CompressedSameNoteRepeats()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double gain in new[] { .07, .3 })
        {
            var result = Measure(rate, 12, gain, CompressedPlucks);
            Require(result.HeardSound && result.Frame.OnsetCount >= 18 && result.SustainedShare < .15,
                $"Compressed same-note repeats disappeared at {rate}/{gain}: {Describe(result)}");
        }
    }

    public static void AdjacentNotes()
    {
        foreach (int rate in new[] { 8000, 44100 })
        {
            var result = Measure(rate, 12, .2, AdjacentPitch);
            Require(result.HeardSound && result.Frame.OnsetCount >= 20 && result.SustainedShare < .15,
                $"Adjacent equal-level notes became a held note at {rate}: {Describe(result)}");
        }
    }

    public static void BackingWithTransientHits()
    {
        foreach (int rate in new[] { 8000, 44100 })
        {
            var result = Measure(rate, 12, .3, MixedHits);
            Require(result.HeardSound && result.Frame.OnsetCount >= 18 && result.SustainedShare < .15,
                $"Short hits under continuous backing disappeared at {rate}: {Describe(result)}");
        }
    }

    public static void HighDensityArticulation()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double gain in new[] { .07, .3 })
        foreach (double interval in new[] { .2, .25, .3, 1d / 3 })
        foreach (var signal in new (string Name, Func<double, double> Wave)[]
        {
            ("compressed same-note", t => CompressedPlucks(t, interval)),
            ("adjacent equal-level", t => AdjacentPitch(t, interval)),
            ("backing with short hits", t => MixedHits(t, interval))
        })
        {
            var result = Measure(rate, 12, gain, signal.Wave);
            long minimumAttacks = (long)Math.Floor(12 / interval * .75);
            Require(result.HeardSound && result.Frame.OnsetCount >= minimumAttacks &&
                result.SustainedShare < .15 && result.Frame.Listening.Expression == PlayingExpression.Animated,
                $"Dense {signal.Name} became background at {rate}/{gain}/{interval}s: {Describe(result)}");
        }
    }

    public static void HeldTonesStayStill()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double frequency in new[] { 82.41, 110, 196, 220, 247, 880, 2200 })
        {
            var result = Measure(rate, 12, .2, t => Math.Tanh(4 * Math.Sin(2 * Math.PI * frequency * t)));
            Require(result.Frame.OnsetCount == 1 && result.SustainedShare > .95,
                $"A constant compressed held tone fabricates repeated articulation at {rate}/{frequency}: {Describe(result)}");
        }
        foreach (int rate in new[] { 8000, 44100 })
        {
            var result = Measure(rate, 12, .3, t => Math.Tanh(2 * Backing(t)));
            Require(result.Frame.OnsetCount == 1 && result.SustainedShare > .95,
                $"Unchanged harmonic backing fabricates hits at {rate}: {Describe(result)}");
            foreach (var pitches in new[]
            {
                new[] { 220d, 247, 329.63 }, new[] { 196d, 246.94, 293.66 },
                new[] { 110d, 164.81, 220 }, new[] { 220d, 277.18, 329.63 },
                new[] { 174.61, 220, 261.63 }
            })
            {
                var chord = Measure(rate, 12, .3, t => Math.Tanh(2 *
                    (.26 * Math.Sin(2 * Math.PI * pitches[0] * t) +
                        .24 * Math.Sin(2 * Math.PI * pitches[1] * t) +
                        .15 * Math.Sin(2 * Math.PI * pitches[2] * t))));
                Require(chord.Frame.OnsetCount == 1 && chord.SustainedShare > .95,
                    $"A stationary chord's beating fabricates pick events at {rate}/{string.Join(',', pitches)}: {Describe(chord)}");
            }
        }
    }

    public static void ChunkAndGainIndependence()
    {
        const int rate = 44100;
        var samples = Enumerable.Range(0, rate * 12).Select(i => (float)(.2 * MixedHits(i / (double)rate))).ToArray();
        var whole = new RhythmTracker(rate).Process(samples);
        var splitTracker = new RhythmTracker(rate);
        var random = new Random(82);
        RhythmFrame split = RhythmFrame.Empty;
        for (int offset = 0; offset < samples.Length;)
        {
            int count = Math.Min(random.Next(1, 1801), samples.Length - offset);
            split = splitTracker.Process(samples.AsSpan(offset, count));
            offset += count;
        }
        Require(whole == split && whole.OnsetCount >= 18,
            $"Mixed-signal analysis changed with callback blocks: whole={whole}, split={split}");
        var lowerGain = new RhythmTracker(rate).Process(samples.Select(x => x * .35f).ToArray());
        Require(Math.Abs(lowerGain.OnsetCount - whole.OnsetCount) <= 1 &&
            lowerGain.Listening.Expression == whole.Listening.Expression,
            $"Above-gate input gain changed repeated articulation: normal={whole}, lower={lowerGain}");
    }

    // Repeated pick envelopes have been compressed until their overall RMS varies only slightly.
    private static double CompressedPlucks(double t) => CompressedPlucks(t, .5);

    private static double CompressedPlucks(double t, double interval)
    {
        double envelope = .45 + .55 * Math.Exp(-(t % interval) / .035);
        return Math.Tanh(5 * envelope * Math.Sin(2 * Math.PI * 220 * t));
    }

    private static double AdjacentPitch(double t) => AdjacentPitch(t, .4);

    private static double AdjacentPitch(double t, double interval)
    {
        int note = (int)(t / interval);
        double frequency = note % 2 == 0 ? 220 : 247;
        double completedPhase = (note / 2) * (220 + 247) * interval + (note % 2 == 0 ? 0 : 220 * interval);
        return Math.Tanh(3 * Math.Sin(2 * Math.PI * (completedPhase + frequency * (t - note * interval))));
    }

    private static double Backing(double t) => .36 * Math.Sin(2 * Math.PI * 110 * t) +
        .24 * Math.Sin(2 * Math.PI * 330 * t) + .18 * Math.Sin(2 * Math.PI * 550 * t);

    private static double MixedHits(double t) => MixedHits(t, .5);

    private static double MixedHits(double t, double interval)
    {
        double hit = .22 * Math.Exp(-(t % interval) / .035) * Math.Sin(2 * Math.PI * 2200 * t);
        return Math.Tanh(2 * (Backing(t) + hit));
    }

    private static Result Measure(int rate, double seconds, double gain, Func<double, double> signal)
    {
        var tracker = new RhythmTracker(rate);
        var analyzer = new SignalAnalyzer();
        int count = (int)(rate * seconds), blockSize = rate / 20;
        int eligible = 0, held = 0;
        bool heard = false;
        RhythmFrame latest = RhythmFrame.Empty;
        for (int offset = 0; offset < count; offset += blockSize)
        {
            var samples = Enumerable.Range(offset, Math.Min(blockSize, count - offset))
                .Select(i => (float)(gain * signal(i / (double)rate))).ToArray();
            latest = tracker.Process(samples);
            bool active = analyzer.Process(samples).IsActive;
            heard |= active;
            if (latest.AudioSeconds >= 2 && active && latest.Listening.Confidence >= .6)
            {
                eligible++;
                if (latest.Listening.Expression == PlayingExpression.Sustained) held++;
            }
        }
        return new(latest, eligible == 0 ? 0 : held / (double)eligible, heard);
    }

    private static string Describe(Result result) => $"onsets={result.Frame.OnsetCount}, " +
        $"expression={result.Frame.Listening.Expression}, held={result.SustainedShare:P1}, audible={result.HeardSound}";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
