using GuitarSoul.Core.Audio;

internal static class ProgressionChecks
{
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private static void Feed(ChordTimeline timeline, int root, ChordQuality quality, double from, double to)
    {
        for (double s = from; s <= to; s += .05)
            timeline.Observe(new HarmonyFrame(root, quality, .9, 0) { RootOnly = quality == ChordQuality.Unknown }, s);
    }

    public static void DegreesFollowTheKey()
    {
        var c = new KeySignature(0, false);
        int[] majorOffsets = [0, 2, 4, 5, 7, 9, 11];
        for (int i = 0; i < 7; i++)
            Check(c.DegreeOf(majorOffsets[i]).Label == (i + 1).ToString(),
                $"C major degree for interval {majorOffsets[i]} was {c.DegreeOf(majorOffsets[i]).Label}");
        Check(c.DegreeOf(10).Label == "b7", $"Bb in C major was {c.DegreeOf(10).Label}");
        Check(c.DegreeOf(6).Label == "#4", $"F# in C major was {c.DegreeOf(6).Label}");

        var a = new KeySignature(9, true);
        string[] minor = ["1", "2", "3", "4", "5", "6", "7"];
        int[] offsets = [0, 2, 3, 5, 7, 8, 10];
        for (int i = 0; i < 7; i++)
        {
            int root = (9 + offsets[i]) % 12;
            Check(a.DegreeOf(root).Label == minor[i], $"A minor degree for {root} was {a.DegreeOf(root).Label}");
        }
        // The same chord is a different number depending on the key; that is the whole point.
        Check(c.DegreeOf(9).Label == "6" && a.DegreeOf(9).Label == "1", "Am is not 6 in C and 1 in A minor");
    }

    public static void TimelineKeepsOrderAndDropsBlips()
    {
        var key = new KeySignature(0, false);
        var timeline = new ChordTimeline();
        Feed(timeline, 0, ChordQuality.Major, 0, 1);
        Feed(timeline, 7, ChordQuality.Major, 1.05, 2);
        Feed(timeline, 9, ChordQuality.Minor, 2.05, 3);
        Feed(timeline, 5, ChordQuality.Major, 3.05, 4);
        timeline.Close();
        Check(timeline.Count == 4, $"Expected four chords, got {timeline.Count}");
        Check(timeline.Describe(key) == "1 5 6 4", $"Progression was {timeline.Describe(key)}");

        // The same four chords read differently in A minor, without touching the recording.
        Check(timeline.Describe(new KeySignature(9, true)) == "3 7 1 6",
            $"A minor reading was {timeline.Describe(new KeySignature(9, true))}");

        // Repeated strums of one chord stay one entry.
        var repeats = new ChordTimeline();
        for (int i = 0; i < 8; i++) Feed(repeats, 0, ChordQuality.Major, i * .5, i * .5 + .4);
        repeats.Close();
        Check(repeats.Count == 1, $"Repeated strums became {repeats.Count} entries");

        // A wrong chord held for one analysis frame is not a chord change.
        var blip = new ChordTimeline();
        Feed(blip, 0, ChordQuality.Major, 0, 1);
        Feed(blip, 2, ChordQuality.Major, 1.05, 1.2);
        Feed(blip, 0, ChordQuality.Major, 1.25, 2.2);
        blip.Close();
        Check(blip.Count == 1 && blip.Segments[0].Root == 0, $"A 150 ms blip split the chord: {blip.Count}");

        // Silence longer than the gap ends the chord instead of extending it forever.
        var gapped = new ChordTimeline();
        Feed(gapped, 0, ChordQuality.Major, 0, 1);
        for (double s = 1.05; s < 3; s += .05) gapped.Observe(default, s);
        Feed(gapped, 0, ChordQuality.Major, 3, 3.5);
        gapped.Close();
        Check(gapped.Count == 2, $"A long silence did not end the chord: {gapped.Count}");
        Check(gapped.Segments[0].Duration < 1.2, $"Chord ran through silence: {gapped.Segments[0].Duration}");
    }

    public static void PowerChordsKeepTheRootOnly()
    {
        const int rate = 48000;
        var tracker = new RhythmTracker(rate);
        var frame = tracker.Process(HarmonyChecks.Chord(rate, 1.6, [48, 55], .3, true)).Listening.Harmony;
        Check(!frame.IsKnown, "A power chord must not claim major or minor");
        Check(frame.HasRoot && frame.RootOnly && frame.Root == 0, $"Power chord root was not C: {frame}");

        var timeline = new ChordTimeline();
        timeline.Observe(frame, 1);
        timeline.Observe(frame, 1.6);
        timeline.Close();
        Check(timeline.Count == 1 && timeline.Segments[0].DegreeLabel(new KeySignature(0, false)) == "1",
            "Power chord did not record as degree 1 in C");
        Check(timeline.Segments[0].IsRootOnly, "Power chord was recorded as a full triad");
    }

    public static void TriadsKeepQuality()
    {
        const int rate = 48000;
        var major = new RhythmTracker(rate).Process(HarmonyChecks.Chord(rate, 1.6, [48, 52, 55], .2, true)).Listening.Harmony;
        var minor = new RhythmTracker(rate).Process(HarmonyChecks.Chord(rate, 1.6, [45, 48, 52], .2, true)).Listening.Harmony;
        Check(major.IsKnown && major.Quality == ChordQuality.Major, $"C major triad read as {major}");
        Check(minor.IsKnown && minor.Quality == ChordQuality.Minor, $"A minor triad read as {minor}");
        var timeline = new ChordTimeline();
        for (double s = 1; s <= 1.9; s += .05) timeline.Observe(major, s);
        for (double s = 2; s <= 2.9; s += .05) timeline.Observe(minor, s);
        timeline.Close();
        Check(timeline.Count == 2 && timeline.Segments[0].Quality == ChordQuality.Major &&
            timeline.Segments[1].Quality == ChordQuality.Minor, "Recorded qualities were lost");
        Check(timeline.Describe(new KeySignature(0, false)) == "1 6", $"C then Am was {timeline.Describe(new KeySignature(0, false))}");
    }

    /// <summary>
    /// A broken chord arrives one note at a time, so no single 512 ms window ever holds all
    /// three tones. Without the phrase layer a slow roll reads as nothing at all, which is
    /// exactly the kind of rigidity the companion must not have.
    /// </summary>
    public static void BrokenChordsStillNameTheChord()
    {
        const int rate = 48000, block = 2400;
        var key = new KeySignature(0, false);

        var rolled = Arpeggio(rate, 5.0, [60, 64, 67, 72], .42, .50);
        var tracker = new RhythmTracker(rate);
        var timeline = new ChordTimeline();
        double clock = 0;
        int frames = 0, named = 0;
        for (int i = 0; i + block <= rolled.Length; i += block)
        {
            var phrase = tracker.Process(rolled.AsSpan(i, block)).Listening.Phrase;
            clock += block / (double)rate;
            frames++;
            if (phrase.IsKnown) named++;
            timeline.Observe(phrase, clock);
        }
        timeline.Close();
        Check(timeline.Count == 1, $"A slow C roll became {timeline.Count} chords");
        Check(timeline.Describe(key) == "1", $"A slow C roll was recorded as {timeline.Describe(key)}");
        Check(named > frames / 2, $"Only {named}/{frames} frames named the rolled chord");

        // One note at a time is not a chord, however wide the phrase window is.
        var line = Arpeggio(rate, 4.2, [60, 62, 64, 65, 67, 69, 71, 72], .35, .40);
        var melodic = new ChordTimeline();
        tracker = new RhythmTracker(rate);
        clock = 0;
        for (int i = 0; i + block <= line.Length; i += block)
        {
            clock += block / (double)rate;
            melodic.Observe(tracker.Process(line.AsSpan(i, block)).Listening.Phrase, clock);
        }
        melodic.Close();
        Check(melodic.Count == 0, $"A single-note line invented {melodic.Count} chords");

        // The phrase tail is exponential and never reaches zero on its own.
        var lingering = new RhythmTracker(rate);
        for (int i = 0; i + block <= rolled.Length; i += block) lingering.Process(rolled.AsSpan(i, block));
        var silence = new float[rate * 2];
        HarmonyFrame last = default;
        for (int i = 0; i + block <= silence.Length; i += block)
            last = lingering.Process(silence.AsSpan(i, block)).Listening.Phrase;
        Check(!last.HasRoot, $"The phrase outlived the player: {last}");
    }

    private static float[] Arpeggio(int rate, double seconds, int[] notes, double spacing, double decay)
    {
        var events = new List<(int Note, double Start)>();
        for (int cycle = 0; cycle * notes.Length * spacing < seconds; cycle++)
            for (int n = 0; n < notes.Length; n++)
                events.Add((notes[n], (cycle * notes.Length + n) * spacing));
        var samples = new float[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate, value = 0;
            foreach (var (note, start) in events)
            {
                if (t < start) continue;
                double age = t - start;
                double env = Math.Exp(-age / decay) * Math.Min(1, age / .01);
                double f = 440 * Math.Pow(2, (note - 69) / 12d);
                value += env * (Math.Sin(2 * Math.PI * f * t) + .35 * Math.Sin(4 * Math.PI * f * t) + .2 * Math.Sin(6 * Math.PI * f * t));
            }
            samples[i] = (float)(.25 * value);
        }
        return samples;
    }

    /// <summary>
    /// The recording exists before any key does. A key only decides which numbers are printed
    /// over it, so the same audio reads as different degrees without a single chord moving.
    /// </summary>
    public static void KeyIsOnlyALabelOnTopOfTheRecording()
    {
        var timeline = Sequence([(0, ChordQuality.Major), (7, ChordQuality.Major), (9, ChordQuality.Minor), (5, ChordQuality.Major)]);
        Check(timeline.DescribeAbsolute() == "C G Am F", $"Recorded as {timeline.DescribeAbsolute()}");
        Check(timeline.Describe(new KeySignature(0, false)) == "1 5 6 4", "C major reading was wrong");
        Check(timeline.Describe(new KeySignature(7, false)) == "4 1 2 b7", $"G major reading was {timeline.Describe(new KeySignature(7, false))}");
        Check(timeline.Describe(new KeySignature(9, true)) == "3 7 1 6", $"A minor reading was {timeline.Describe(new KeySignature(9, true))}");
        // Three readings, one recording: the chords never move when the key does.
        Check(timeline.DescribeAbsolute() == "C G Am F", "Changing the key rewrote the recording");

        // The same holds for audio: the detector is never told a key, so it cannot be fitting one.
        const int rate = 48000, block = 2400;
        int[][] power = [[48, 55], [55, 62], [57, 64], [53, 60]];
        var samples = power.SelectMany(notes => HarmonyChecks.Chord(rate, 1.4, notes, .3, true)).ToArray();
        var tracker = new RhythmTracker(rate);
        var recorded = new ChordTimeline();
        double seconds = 0;
        for (int i = 0; i + block <= samples.Length; i += block)
        {
            var frame = tracker.Process(samples.AsSpan(i, block));
            seconds += block / (double)rate;
            recorded.Observe(frame.Listening.Harmony, seconds);
        }
        recorded.Close();
        Check(recorded.DescribeAbsolute() == "C5 G5 A5 F5", $"Audio recorded as {recorded.DescribeAbsolute()}");
        Check(recorded.Describe(new KeySignature(0, false)) == "1 5 6 4" &&
            recorded.Describe(new KeySignature(7, false)) == "4 1 2 b7",
            "One recording did not read as two keys");
    }

    /// <summary>
    /// A key inferred from the chords that were played, so the player never declares one first.
    /// </summary>
    public static void KeyIsInferredFromWhatWasPlayed()
    {
        var c = KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major), (7, ChordQuality.Major), (9, ChordQuality.Minor), (5, ChordQuality.Major)]).Segments);
        Check(c is { IsConfident: true }, "A plain 1-5-6-4 in C was not recognised as a key");
        Check(c!.Value.Key == new KeySignature(0, false), $"1-5-6-4 read as {c.Value.Key.Name}");

        var g = KeyEstimator.Estimate(Sequence([(7, ChordQuality.Major), (2, ChordQuality.Major), (4, ChordQuality.Minor), (0, ChordQuality.Major)]).Segments);
        Check(g is { IsConfident: true } && g!.Value.Key == new KeySignature(7, false), $"The same shape in G read as {g?.Key.Name}");

        // Power chords carry no third, so the mode is not knowable: report the pair, not one guess.
        var power = KeyEstimator.Estimate(Sequence([(0, ChordQuality.Unknown), (7, ChordQuality.Unknown), (9, ChordQuality.Unknown), (5, ChordQuality.Unknown)]).Segments);
        Check(power is { IsConfident: true }, "Power chords did not imply a key");
        Check(power!.Value.Key == new KeySignature(0, false) && power.Value.Alternative == new KeySignature(9, true),
            $"C5-G5-Am5-F5 should read as C major or A minor, got {power.Value.Key.Name} / {power.Value.Alternative?.Name ?? "none"}");

        // A cadence is the one hint chords give about the tonic, and it must break the tie.
        var cadence = KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major), (7, ChordQuality.Major), (9, ChordQuality.Minor), (5, ChordQuality.Major), (0, ChordQuality.Major)]).Segments);
        Check(cadence is { IsConfident: true } && cadence!.Value.Key == new KeySignature(0, false) && cadence.Value.Alternative is null,
            $"Ending on C did not settle the key: {cadence?.Key.Name} / {cadence?.Alternative?.Name ?? "none"}");

        // The same shape with and without thirds must agree on the tonic; only the mode hedge differs.
        var triads = KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major), (7, ChordQuality.Major), (9, ChordQuality.Minor), (5, ChordQuality.Major)]).Segments);
        Check(triads is { IsConfident: true } && triads!.Value.Key == new KeySignature(0, false) && triads.Value.Alternative is null,
            $"A heard third should settle the mode, got {triads?.Key.Name} / {triads?.Alternative?.Name ?? "none"}");

        // Too little evidence must produce no claim at all rather than a default.
        Check(KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major)]).Segments) is null, "One chord produced a key");
        Check(KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major), (7, ChordQuality.Major)]).Segments) is null, "Two chords produced a key");

        // Something that fits no key well must not be dressed up as one. Four chords a semitone
        // apart cannot share a scale, so no key explains more than half of them.
        var chromatic = KeyEstimator.Estimate(Sequence([(0, ChordQuality.Major), (1, ChordQuality.Major), (2, ChordQuality.Major), (3, ChordQuality.Major)]).Segments);
        Check(chromatic is null || !chromatic.Value.IsConfident, $"A chromatic chord run claimed {chromatic?.Key.Name} confidently");
    }

    private static ChordTimeline Sequence(params (int Root, ChordQuality Quality)[] chords)
    {
        var timeline = new ChordTimeline();
        double t = 0;
        foreach (var (root, quality) in chords) { Feed(timeline, root, quality, t, t + 1); t += 1.05; }
        timeline.Close();
        return timeline;
    }

    public static void RecordedProgressionRunsOnRealPcm()
    {
        const int rate = 48000, block = 2400;
        int[][] powerChords = [[48, 55], [55, 62], [57, 64], [53, 60]]; // C5 G5 A5 F5
        var samples = powerChords.SelectMany(notes => HarmonyChecks.Chord(rate, 1.4, notes, .3, true)).ToArray();
        var tracker = new RhythmTracker(rate);
        var timeline = new ChordTimeline();
        double seconds = 0;
        for (int i = 0; i + block <= samples.Length; i += block)
        {
            var frame = tracker.Process(samples.AsSpan(i, block));
            seconds += block / (double)rate;
            timeline.Observe(frame.Listening.Harmony, seconds);
        }
        timeline.Close();
        var key = new KeySignature(0, false);
        Check(timeline.Describe(key) == "1 5 6 4", $"Power-chord PCM recorded as {timeline.Describe(key)} ({timeline.Count} chords)");
        Check(timeline.Segments.All(s => s.IsRootOnly), "Synthetic power chords claimed a third");
    }
}
