using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

internal static class HarmonyChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static float[] Chord(int rate, double seconds, int[] notes, double gain = .2, bool rich = false)
    {
        var samples = new float[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate, value = 0;
            foreach (int note in notes)
            {
                double phase = 2 * Math.PI * 440 * Math.Pow(2, (note - 69) / 12d) * t;
                value += Math.Sin(phase);
                if (rich) for (int h = 2; h <= 6; h++) value += Math.Sin(phase * h) * .5 / h;
            }
            samples[i] = (float)(gain * value / notes.Length * Math.Min(1, t / .015));
        }
        return samples;
    }

    public static void TriadsAndUnknown()
    {
        foreach (int rate in new[] { 8000, 44100, 48000 })
        foreach (double gain in new[] { .04, .3 })
        foreach (int root in Enumerable.Range(0, 12))
        foreach (bool minor in new[] { false, true })
        {
            var frame = new RhythmTracker(rate).Process(Chord(rate, 1.6, [48 + root, 48 + root + (minor ? 3 : 4), 55 + root], gain, true)).Listening.Harmony;
            Check(frame.Root == root && frame.Quality == (minor ? ChordQuality.Minor : ChordQuality.Major),
                $"Triad {root}/{minor} rate={rate}, gain={gain}: {frame}");
            Check(frame.ChangeId == 0, "First chord falsely became a change");
        }
        foreach (var notes in new[] { new[] { 40 }, new[] { 48, 55 }, new[] { 48, 49, 50, 54, 58 } })
        {
            var h = new RhythmTracker(48000).Process(Chord(48000, 2, notes, .3, true)).Listening.Harmony;
            Check(!h.IsKnown, $"Single tone, power chord or cluster forced into a triad: {h}");
        }
        var noise = new Random(77);
        var samples = Enumerable.Range(0, 48000 * 3).Select(_ => (float)((noise.NextDouble() - .5) * .4)).ToArray();
        var noiseTracker = new RhythmTracker(48000);
        for (int i = 0; i < samples.Length; i += 2400)
            Check(!noiseTracker.Process(samples.AsSpan(i, 2400)).Listening.Harmony.IsKnown, "Noise acquired a chord");
    }

    public static void ChangesAndChunking()
    {
        var c = Chord(44100, 2, [48, 52, 55]);
        var am = Chord(44100, 2, [45, 52, 57, 60]);
        var samples = c.Concat(am).ToArray();
        var whole = new RhythmTracker(44100).Process(samples);
        var tracker = new RhythmTracker(44100); RhythmFrame chunk = default;
        for (int i = 0; i < samples.Length; i += 173) chunk = tracker.Process(samples.AsSpan(i, Math.Min(173, samples.Length - i)));
        Check(whole == chunk, "Harmonic context depends on driver callback sizes");
        Check(chunk.Listening.Harmony is { Root: 9, Quality: ChordQuality.Minor, ChangeId: 1 }, $"C to Am not a single stable transition: {chunk.Listening.Harmony}");
        var silent = tracker.Process(new float[44100 * 2]);
        Check(!silent.Listening.Harmony.IsKnown, "Silence retained chord evidence");
        var resumed = tracker.Process(am);
        Check(resumed.Listening.Harmony.ChangeId == 1, "Silence gap manufactured a harmonic transition");
        var transient = new RhythmTracker(44100);
        transient.Process(c);
        transient.Process(Chord(44100, .08, [49, 53, 56]));
        Check(transient.Process(c).Listening.Harmony.ChangeId == 0, "Brief interference became a stable chord turn");
    }

    public static void PhraseRejectsConflictingThirds()
    {
        foreach (int rate in new[] { 8000, 44100, 48000 })
        foreach (double gain in new[] { .04, .3 })
        foreach (int root in Enumerable.Range(0, 12))
        {
            // Both thirds are present at equal source amplitudes. Neither major nor minor
            // explains this input uniquely; the longer window must preserve that ambiguity.
            var samples = Chord(rate, 2, [48 + root, 51 + root, 52 + root, 55 + root], gain, true);
            var tracker = new RhythmTracker(rate);
            for (int offset = 0; offset < samples.Length; offset += rate / 20)
            {
                var frame = tracker.Process(samples.AsSpan(offset, Math.Min(rate / 20, samples.Length - offset))).Listening;
                Check(!frame.Phrase.HasRoot,
                    $"Conflicting thirds became a phrase chord at {rate} Hz, gain={gain}, root={root}, t={offset / (double)rate:F2}: {frame.Phrase}");
            }
        }
    }

    public static void MusicalContextChangesBehavior()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var fixedPhrase = new CompanionDirector(18); var changing = new CompanionDirector(18);
            double firstJam = -1, fixedDuration = 0, changedDuration = 0;
            long change = 0;
            for (int i = 0; i < 480; i++)
            {
                double time = i * .05;
                if (firstJam >= 0 && time - firstJam > 3) change = (long)((time - firstJam - 3) / 1.2) + 1;
                var features = new ListeningFeatures(ToneColor.Warm, PlayingExpression.Animated, 1)
                    { Harmony = new(0, ChordQuality.Major, .98, 0) };
                var a = fixedPhrase.Step(true, true, i % 4 == 0, .05, false, style, .8, features);
                var b = changing.Step(true, true, i % 4 == 0, .05, false, style, .8,
                    features with { Harmony = new(9, ChordQuality.Minor, .98, change) });
                if (a.Action == CompanionAction.Jam) { if (firstJam < 0) firstJam = time; fixedDuration += .05; }
                if (b.Action == CompanionAction.Jam) changedDuration += .05;
            }
            Check(firstJam >= 0 && changedDuration > fixedDuration + 1 && changedDuration <= 12.1,
                $"{style} ignores phrase continuation or jams forever: {fixedDuration} / {changedDuration}");
            var quiet = changing.Step(true, true, true, .05, true, style, .8,
                new(ToneColor.Warm, PlayingExpression.Animated, 1) { Harmony = new(0, ChordQuality.Major, 1, 100) });
            Check(quiet.Action == CompanionAction.Listen && quiet.Engagement == 0, "Harmony overrode quiet");

            var held = new CompanionDirector(18); int notices = 0;
            CompanionAction previous = CompanionAction.Idle;
            for (int i = 0; i < 240; i++)
            {
                var features = new ListeningFeatures(ToneColor.Warm, PlayingExpression.Sustained, 1)
                    { Harmony = new(i < 100 ? 0 : 9, ChordQuality.Minor, .98, i < 100 ? 0 : 1) };
                var pose = held.Step(true, false, i == 0 || i == 100, .05, false, style, 0, features);
                if (i >= 100 && pose.Action == CompanionAction.Notice && previous != pose.Action) notices++;
                Check(pose.Action != CompanionAction.Jam, "Changing held harmony alone caused guitar playing");
                previous = pose.Action;
            }
            Check(notices == 1, $"{style} ignored or repeated the same harmonic change: {notices}");
        }
    }

    public static object PcmReport(bool verify = false)
    {
        const int rate = 48000, block = 2400;
        int[][] notes = [[48, 52, 55], [45, 52, 57, 60], [43, 50, 55, 59], [41, 48, 53, 57]];
        var samples = Enumerable.Range(0, 12).SelectMany(i => Chord(rate, 2, notes[i % 4], .2, true)).ToArray();
        // Repeated plucks with continuous decay, at matched gain and timing for both directors.
        for (int i = 0; i < samples.Length; i++) samples[i] *= (float)(.25 + .75 * Math.Exp(-(i % 12000) / 2500d));
        var rows = new List<object>();
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var rhythm = new RhythmTracker(rate);
            var baseline = new CompanionDirector(18); var withHarmony = new CompanionDirector(18);
            long previousOnset = 0;
            double beforeJam = 0, afterJam = 0;
            long changes = 0; int known = 0;
            var costs = new List<double>();
            var trajectory = new List<object>();
            for (int i = 0; i < samples.Length; i += block)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                var frame = rhythm.Process(samples.AsSpan(i, block));
                costs.Add(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                var f = frame.Listening;
                bool onset = frame.OnsetCount > previousOnset;
                bool steady = frame.TempoBpm.HasValue && frame.Confidence >= .7;
                var a = baseline.Step(true, steady, onset, .05, false, style, frame.Drive, f with { Harmony = default });
                var b = withHarmony.Step(true, steady, onset, .05, false, style, frame.Drive, f);
                if (a.Action == CompanionAction.Jam) beforeJam += .05;
                if (b.Action == CompanionAction.Jam) afterJam += .05;
                if (f.Harmony.IsKnown) known++;
                changes = Math.Max(changes, f.Harmony.ChangeId);
                trajectory.Add(new { seconds = frame.AudioSeconds, harmony = f.Harmony,
                    before = a.Action.ToString(), after = b.Action.ToString() });
                previousOnset = frame.OnsetCount;
            }
            if (verify)
            {
                Check(changes >= 8 && known >= 120, $"Plucked PCM context was not established: {changes}, {known}");
                Check(afterJam > beforeJam + .5, $"{style} actual PCM did not change the response: {beforeJam}/{afterJam}");
            }
            var stop = withHarmony.Step(false, false, false, .05, false, style);
            for (int tail = 0; tail < 13; tail++) stop = withHarmony.Step(false, false, false, .05, false, style);
            if (verify) Check(stop.Action is not (CompanionAction.Jam or CompanionAction.Engage or CompanionAction.Groove), "PCM response survived 0.7 seconds of silence");
            costs.Sort();
            rows.Add(new { style = style.ToString(), knownFrames = known, changes, beforeJam, afterJam,
                analysisBlockMsP95 = costs[(int)(costs.Count * .95)], analysisBlockMsMax = costs[^1], trajectory });
        }
        return new { scope = "Synthetic harmonically rich plucked triads; same PCM with harmony evidence removed as ablation. Not real guitar or user acceptance.", sampleRate = rate, seconds = 24, rows };
    }

    public static void PcmToBehavior() => _ = PcmReport(verify: true);
}
