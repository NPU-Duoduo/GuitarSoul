using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

internal static class MusicPulseChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static float[] Music(int rate, double bpm, double seconds, bool lift = false)
    {
        var samples = new float[(int)(rate * seconds)];
        double period = 60 / bpm;
        for (int i = 0; i < samples.Length; i++)
        {
            double time = i / (double)rate;
            double beat = time % period, eighth = time % (period / 2);
            double gain = lift && time >= 9 ? .35 : .1;
            samples[i] = (float)(gain * (Math.Sin(2 * Math.PI * 110 * time) * Math.Exp(-beat / .05) +
                .18 * Math.Sin(2 * Math.PI * 440 * time) * Math.Exp(-eighth / .08)));
        }
        return samples;
    }
    public static void TactusAndSilence()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double bpm in new[] { 90d, 120, 150 })
        {
            var tracker = new RhythmTracker(rate);
            var frame = tracker.Process(Music(rate, bpm, 12));
            Check(frame.BeatBpm.HasValue && Math.Abs(frame.BeatBpm.Value - bpm) < 3 && frame.BeatConfidence >= .6,
                $"Bass beat with eighth-note subdivisions did not find {bpm} BPM at {rate}: {frame.BeatBpm}/{frame.BeatConfidence}");
            var silent = tracker.Process(new float[rate]);
            Check(silent.BeatBpm is null && silent.Listening.SectionLift == 0 && silent.Listening.PluckedPresence == 0,
                "Silent input retained musical cues");
        }
        MixedAccentsRetainBeatAndPhase();
        AperiodicSoundDoesNotLock();
        TempoUpdatesPreservePhase();
    }

    public static void AlternatingBassAndTrebleAccentsKeepTheFullBeat()
    {
        foreach (int rate in new[] { 8000, 48000 })
        foreach (double bpm in new[] { 132d, 156, 176 })
        {
            var samples = new float[rate * 20];
            double period = 60 / bpm;
            for (int beat = 0; beat * period < 20; beat++)
            {
                // Both accents are marked beats: bass and treble alternate. A bass-only
                // novelty curve hears only every other beat, even with clear interleaving.
                double gain = beat % 2 == 1 && beat * period is > 8 and < 15 ? .12 : .2;
                AddAccent(samples, rate, beat * period, gain, beat % 2 == 0 ? 95 : 1800, .055);
                AddAccent(samples, rate, beat * period + period / 2, .015, 880, .035);
            }
            var tracker = new RhythmTracker(rate);
            int measured = 0, correct = 0;
            var phaseErrors = new List<double>();
            RhythmFrame final = default;
            for (int offset = 0; offset < samples.Length; offset += rate / 20)
            {
                var frame = tracker.Process(samples.AsSpan(offset, rate / 20));
                final = frame;
                if (frame.AudioSeconds < 8) continue;
                measured++;
                if (frame.BeatBpm is double found && frame.BeatConfidence >= .6 && Math.Abs(found - bpm) < 3)
                {
                    correct++;
                    double foundPeriod = 60 / found;
                    double predicted = frame.BeatAnchorSeconds + Math.Round((frame.AudioSeconds - frame.BeatAnchorSeconds) / foundPeriod) * foundPeriod;
                    phaseErrors.Add(Math.Abs(predicted / period - Math.Round(predicted / period)) * period);
                }
            }
            Check(correct >= measured * .75,
                $"Alternating bass/treble at {bpm} BPM, {rate} Hz kept the full marked beat in only {correct}/{measured} frames");
            var errors = phaseErrors.Order().ToArray();
            Check(errors[(int)(errors.Length * .95)] < .12, "Alternating accents found the tempo but missed the marked beat phase");
            if (rate == 8000 && bpm == 156)
            {
                var split = new RhythmTracker(rate); RhythmFrame splitFrame = default;
                for (int offset = 0; offset < samples.Length; offset += 173)
                    splitFrame = split.Process(samples.AsSpan(offset, Math.Min(173, samples.Length - offset)));
                Check(final.BeatBpm == splitFrame.BeatBpm && final.BeatAnchorSeconds == splitFrame.BeatAnchorSeconds,
                    "Alternating accents changed pulse with callback boundaries");
            }
        }
    }

    private static void MixedAccentsRetainBeatAndPhase()
    {
        const int rate = 8000;
        foreach (double bpm in new[] { 90d, 120, 150 })
        {
            var random = new Random(731);
            var samples = new float[rate * 28];
            for (int i = 0; i < samples.Length; i++)
            {
                double time = i / (double)rate;
                samples[i] = (float)(.035 * Math.Sin(2 * Math.PI * 196 * time) +
                    .025 * Math.Sin(2 * Math.PI * 329.63 * time) + .008 * (random.NextDouble() * 2 - 1));
            }
            double period = 60 / bpm;
            for (int beat = 0; beat * period < 28; beat++)
            {
                // A stable grid with human timing spread and changing accent strength.
                double start = Math.Max(0, beat * period + (random.NextDouble() - .5) * .04);
                AddAccent(samples, rate, start, .13 + random.NextDouble() * .09, 95, .055);
                AddAccent(samples, rate, beat * period + period / 2, .025, 880, .04);
            }
            var tracker = new RhythmTracker(rate);
            int eligible = 0, locked = 0;
            var phaseErrors = new List<double>();
            for (int i = 0; i < samples.Length; i += rate / 20)
            {
                var frame = tracker.Process(samples.AsSpan(i, rate / 20));
                if (frame.AudioSeconds < 6) continue;
                eligible++;
                if (frame.BeatBpm is not double found || frame.BeatConfidence < .6) continue;
                locked++;
                Check(Math.Abs(found - bpm) < 3,
                    $"Timing-spread mixture selected {found} instead of known synthetic {bpm} BPM at {frame.AudioSeconds:F2} s");
                // Anchor retains a beat-number origin while the estimated period can
                // change. Assess the beat currently rendered, not that old origin
                // modulo a different (ground-truth) period.
                double foundPeriod = 60 / found;
                double predictedBeat = frame.BeatAnchorSeconds +
                    Math.Round((frame.AudioSeconds - frame.BeatAnchorSeconds) / foundPeriod) * foundPeriod;
                double phase = predictedBeat / period;
                phaseErrors.Add(Math.Abs(phase - Math.Round(phase)) * period);
            }
            Check(locked > eligible * .65, $"Timing-spread mixture at {bpm} lost reliable pulse: {locked}/{eligible}");
            var errors = phaseErrors.Order().ToArray();
            Check(errors[(int)(errors.Length * .95)] < .12,
                $"Timing-spread mixture at {bpm} has >120 ms phase displacement at p95: {errors[(int)(errors.Length * .95)]:F4} s");
        }
    }

    private static void AperiodicSoundDoesNotLock()
    {
        const int rate = 8000;
        foreach (bool bursts in new[] { false, true })
        {
            var random = new Random(4921);
            var samples = Enumerable.Range(0, rate * 28)
                .Select(_ => (float)((random.NextDouble() * 2 - 1) * .16)).ToArray();
            if (bursts)
            {
                for (double time = .1; time < 28; time += .12 + random.NextDouble() * .81)
                    AddAccent(samples, rate, time, .08 + random.NextDouble() * .2, 70 + random.NextDouble() * 250, .025 + random.NextDouble() * .08);
            }
            var tracker = new RhythmTracker(rate);
            int locks = 0;
            for (int i = 0; i < samples.Length; i += rate / 20)
            {
                var frame = tracker.Process(samples.AsSpan(i, rate / 20));
                if (frame.BeatBpm.HasValue && frame.BeatConfidence >= .6) locks++;
            }
            Check(locks == 0, $"{(bursts ? "Irregular accents with noise" : "Stationary noise")} fabricated {locks} reliable beat frames");
        }
    }

    private static void AddAccent(float[] samples, int rate, double start, double gain, double frequency, double decay)
    {
        int first = (int)(start * rate);
        for (int i = first; i < Math.Min(samples.Length, first + rate / 2); i++)
        {
            double age = (i - first) / (double)rate;
            samples[i] += (float)(gain * Math.Sin(2 * Math.PI * frequency * age) * Math.Exp(-age / decay));
        }
    }

    private static void TempoUpdatesPreservePhase()
    {
        const int rate = 8000;
        var samples = Music(rate, 90, 12).Concat(Music(rate, 120, 16)).ToArray();
        var tracker = new RhythmTracker(rate);
        var previous = RhythmFrame.Empty;
        bool firstTempo = false, secondTempo = false;
        int periodChanges = 0;
        for (int i = 0; i < samples.Length; i += rate / 20)
        {
            var frame = tracker.Process(samples.AsSpan(i, rate / 20));
            bool locked = frame.BeatBpm.HasValue && frame.BeatConfidence >= .6;
            bool wasLocked = previous.BeatBpm.HasValue && previous.BeatConfidence >= .6;
            if (locked)
            {
                firstTempo |= frame.AudioSeconds < 12 && Math.Abs(frame.BeatBpm!.Value - 90) < 3;
                secondTempo |= frame.AudioSeconds > 22 && Math.Abs(frame.BeatBpm!.Value - 120) < 3;
            }
            if (locked && wasLocked)
            {
                double currentPosition = (frame.AudioSeconds - frame.BeatAnchorSeconds) * frame.BeatBpm!.Value / 60;
                double priorPrediction = (frame.AudioSeconds - previous.BeatAnchorSeconds) * previous.BeatBpm!.Value / 60;
                double difference = currentPosition - priorPrediction;
                difference -= Math.Round(difference);
                double phaseSeconds = Math.Abs(difference) * 60 / frame.BeatBpm.Value;
                Check(phaseSeconds <= .030001,
                    $"Tempo update shifted the rendered phase by {phaseSeconds:F4} s instead of at most30 ms");
                if (frame.BeatBpm != previous.BeatBpm) periodChanges++;
            }
            previous = frame;
        }
        Check(firstTempo && secondTempo, "Known90-to120 BPM fixture failed to acquire both periods");
        Check(periodChanges > 0, "Tempo-transition fixture did not exercise any continuously locked period changes");
    }
    public static void SectionContrast()
    {
        var stable = new RhythmTracker(8000).Process(Music(8000, 120, 13));
        var lifted = new RhythmTracker(8000).Process(Music(8000, 120, 13, true));
        Check(stable.Listening.SectionCue == 0 && stable.Listening.SectionLift == 0, "Stable music manufactured a section change");
        Check(lifted.Listening.SectionCue == 1 && lifted.Listening.SectionLift > .35, $"Stronger rhythmic section failed to create one cue: {lifted.Listening}");
        var held = new RhythmTracker(8000).Process(Enumerable.Range(0, 104000).Select(i => (float)(.2 * Math.Sin(2 * Math.PI * 220 * i / 8000))).ToArray());
        Check(held.Listening.PluckedPresence < .1 && held.Listening.SectionCue == 0, "Held tone manufactured picked articulation or section events");
        Check(held.BeatBpm is null, "Carrier-frequency window ripple manufactured a song BPM");
    }
    public static void HighlightsJoinAndYield()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector();
            var music = new ListeningFeatures(ToneColor.Bright, PlayingExpression.Flowing, 1);
            bool joined = false; int admissions = 0;
            var previous = CompanionAction.Idle;
            for (int tick = 0; tick < 600; tick++)
            {
                var cue = music with { SectionLift = tick >= 60 ? .8 : 0, SectionCue = tick >= 60 ? 1 : 0 };
                var pose = director.Step(true, true, tick % 5 == 0, .05, false, style, .5, cue);
                if (tick >= 60 && pose.Action == CompanionAction.Jam) joined = true;
                if (pose.Action == CompanionAction.Jam && previous != CompanionAction.Jam) admissions++;
                previous = pose.Action;
            }
            Check(joined, $"{style} missed a new stronger section");
            Check(admissions == 1, $"{style} replayed the unchanged highlight: {admissions}");
            var quiet = director.Step(true, true, true, .05, true, style, 1, music with { SectionLift = 1, SectionCue = 2 });
            Check(quiet.Action == CompanionAction.Listen && quiet.Weight == 0, "Highlight ignored quiet mode");
        }
    }
    public static void CallbackIndependence()
    {
        var samples = Music(8000, 120, 13, true);
        var whole = new RhythmTracker(8000).Process(samples);
        var tracker = new RhythmTracker(8000); RhythmFrame split = default;
        for (int i = 0; i < samples.Length; i += 173) split = tracker.Process(samples.AsSpan(i, Math.Min(173, samples.Length - i)));
        Check(whole.BeatBpm == split.BeatBpm && whole.BeatAnchorSeconds == split.BeatAnchorSeconds &&
            whole.Listening.SectionCue == split.Listening.SectionCue && whole.Listening.SectionLift == split.Listening.SectionLift,
            "Callback boundaries altered beat or section evidence");
    }
}
