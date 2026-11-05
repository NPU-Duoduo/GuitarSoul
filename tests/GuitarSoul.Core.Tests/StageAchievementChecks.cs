using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Memories;

internal static class StageAchievementChecks
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(8));
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static RhythmFrame Frame(double t, PlayingExpression expression = PlayingExpression.Animated, double confidence = 1,
        long? onsets = null, DynamicContour dynamics = DynamicContour.Steady, double pulseConfidence = 0) =>
        new(t, t, onsets ?? (long)(t * 4), pulseConfidence >= .6 ? 120 : null, pulseConfidence)
        {
            BeatConfidence = pulseConfidence,
            Listening = new(ToneColor.Warm, expression, confidence) { Dynamics = dynamics }
        };
    private static List<StageTrigger> Feed(StageAchievementDetector detector, double from, double until,
        Func<double, PlayingExpression>? expression = null, bool eligible = true, bool active = true, double confidence = 1,
        Func<double, long>? onsets = null, Func<double, bool>? activity = null,
        Func<double, DynamicContour>? dynamics = null, Func<double, double>? pulseConfidence = null)
    {
        var events = new List<StageTrigger>();
        for (int i = (int)Math.Round(from * 10); i <= (int)Math.Round(until * 10); i++)
        {
            double time = i / 10d;
            events.AddRange(detector.Observe(Frame(time, expression?.Invoke(time) ?? PlayingExpression.Animated, confidence,
                onsets?.Invoke(time), dynamics?.Invoke(time) ?? DynamicContour.Steady, pulseConfidence?.Invoke(time) ?? 0),
                eligible, activity?.Invoke(time) ?? active));
        }
        return events;
    }
    public static void EvidenceBoundaries()
    {
        var detector = new StageAchievementDetector();
        Check(Feed(detector, 0, 6).Count == 0, "A brief burst must not unlock.");
        var stale = Frame(6);
        for (int i = 0; i < 1000; i++) Check(detector.Observe(stale, true, true).Count == 0, "Repeated frame earned time.");
        Check(Feed(detector, 6.1, 12).Count == 0, "Continuous density alone must not impersonate a melodic lead encounter.");
        detector.Reset();
        Check(Feed(detector, 0, 14, t => t % 7 < 2.5 ? PlayingExpression.Animated : PlayingExpression.Sustained)
            .Any(x => x.Style == StageStyle.HardRock), "Two fast phrases resolving to held notes should unlock the lead encounter.");
        detector.Reset();
        var ignored = new List<StageTrigger>();
        for (int i = 0; i <= 140; i++)
        {
            double time = i / 10d;
            ignored.AddRange(detector.Observe(Frame(time, time % 7 < 2.5 ? PlayingExpression.Animated : PlayingExpression.Sustained),
                true, true, style => style == StageStyle.HardRock));
        }
        Check(ignored.Count == 0, "An already unlocked encounter must not consume the passage or notify again.");
        foreach (var caseId in new[] { "excluded", "silent", "uncertain" })
        {
            detector.Reset();
            Check(Feed(detector, 0, 50, eligible: caseId != "excluded", active: caseId != "silent", confidence: caseId == "uncertain" ? .2 : 1).Count == 0,
                "Nonqualifying input unlocked: " + caseId);
        }
        detector.Reset(); Feed(detector, 0, 6);
        Check(Feed(detector, 20, 25).Count == 0, "A missing callback gap must reset rather than bridge evidence.");
        detector.Reset(); Feed(detector, 0, 6);
        Check(Feed(detector, 0, 5).Count == 0, "A reset sample clock must not keep previous evidence.");
        detector.Reset(); Feed(detector, 0, 6);
        Feed(detector, 6.1, 53, active: false);
        Check(Feed(detector, 53.1, 58).Count == 0, "Old evidence must expire from the bounded window.");
        foreach (bool silent in new[] { true, false })
        {
            detector.Reset();
            for (int i = 0; i <= 550; i++)
            {
                double time = i / 10d;
                bool playing = time <= 20;
                var frame = Frame(time, time <= 5 ? PlayingExpression.Animated : PlayingExpression.Sustained,
                    playing || silent ? 1 : .2) with { OnsetCount = (long)Math.Floor(Math.Min(time, 20) * 4) };
                var events = detector.Observe(frame, true, playing || !silent);
                if (!playing)
                    Check(events.Count == 0,
                        $"Expiring old dense-playing evidence unlocked a new achievement at {time:F1}s during {(silent ? "silence" : "uncertain input")}.");
            }
        }
        detector.Reset();
        Check(detector.Observe(Frame(double.NaN), true, true).Count == 0, "Invalid clock must be rejected.");
    }

    public static void DistinctMoments()
    {
        var detector = new StageAchievementDetector();
        var sustained = Feed(detector, 0, 14, _ => PlayingExpression.Sustained,
            onsets: t => (long)(t / 3.4) + 1, activity: t => t % 3.4 < 3);
        Check(sustained.Any(x => x.Style == StageStyle.Mellow) && sustained.All(x => x.Style == StageStyle.Mellow), "Long-note moments should have a distinct reward.");
        detector.Reset();
        var varied = Feed(detector, 0, 14, t => t < 2.5 ? PlayingExpression.Sustained : t < 5 ? PlayingExpression.Flowing
            : t < 7.5 ? PlayingExpression.Animated : t < 10 ? PlayingExpression.Flowing : PlayingExpression.Sustained);
        Check(varied.Any(x => x.Style == StageStyle.Psychedelic) && varied.All(x => x.Style == StageStyle.Psychedelic), "Stable phrase alternation should reveal the expressive shape, not dense playing.");
        detector.Reset();
        Check(Feed(detector, 0, 20, t => (int)(t * 10) % 2 == 0 ? PlayingExpression.Sustained : PlayingExpression.Flowing).Count == 0,
            "Frame-level feature flicker is not deliberate phrase alternation.");
        detector.Reset();
        var constant = new RhythmFrame(0, 0, 1, null, 0) { Listening = new(ToneColor.Warm, PlayingExpression.Sustained, 1) };
        for (int i = 0; i < 300; i++) Check(detector.Observe(constant with { AudioSeconds = i / 10d }, true, true).Count == 0,
            "One endless tone must not count as several played notes.");
    }

    public static void SyntheticAudioPipeline()
    {
        Dictionary<StageStyle, double> Run(string name, int callbackMs, double duration, Func<double, double> signal)
        {
            var rhythm = new RhythmTracker(8000);
            var signalAnalyzer = new SignalAnalyzer();
            var detector = new StageAchievementDetector();
            var earned = new Dictionary<StageStyle, double>();
            int blockSize = callbackMs * 8;
            var pcm = new float[blockSize];
            long onsets = 0, unclassifiedAttacks = 0;
            var expressionSeconds = new Dictionary<PlayingExpression, double>();
            var dynamicSeconds = new Dictionary<DynamicContour, double>();
            for (int offset = 0; offset < duration * 8000; offset += blockSize)
            {
                for (int i = 0; i < pcm.Length; i++)
                    pcm[i] = (float)signal((offset + i) / 8000d);
                var frame = rhythm.Process(pcm);
                var signalFrame = signalAnalyzer.Process(pcm);
                bool active = signalFrame.IsActive;
                expressionSeconds[frame.Listening.Expression] = expressionSeconds.GetValueOrDefault(frame.Listening.Expression) + callbackMs / 1000d;
                dynamicSeconds[frame.Listening.Dynamics] = dynamicSeconds.GetValueOrDefault(frame.Listening.Dynamics) + callbackMs / 1000d;
                if (active && frame.Listening.Confidence >= .65 && frame.Listening.Expression == PlayingExpression.None)
                    unclassifiedAttacks += frame.OnsetCount - onsets;
                onsets = frame.OnsetCount;
                foreach (var trigger in detector.Observe(frame, true, active)) earned.TryAdd(trigger.Style, frame.AudioSeconds);
            }
            Console.WriteLine($"Achievement PCM {name}, {callbackMs} ms: " +
                $"{string.Join(", ", earned.Select(x => $"{x.Key}@{x.Value:F2}s"))}; attacks={onsets}, unclassifiedAttacks={unclassifiedAttacks}; " +
                string.Join(", ", expressionSeconds.Select(x => $"{x.Key}={x.Value:F2}s")) + "; " +
                string.Join(", ", dynamicSeconds.Select(x => $"{x.Key}={x.Value:F2}s")));
            return earned;
        }
        static double Tone(double age, double decay) => .18 * Math.Exp(-age * decay) *
            (Math.Sin(2 * Math.PI * 196 * age) + .25 * Math.Sin(2 * Math.PI * 392 * age));
        static double HeldNotes(double time)
        {
            // Eight separately struck notes, each held 3.2 s with a 400 ms gap.
            // The gap preserves listening confidence, but an attack still precedes its expression label.
            double age = time % 3.6;
            return age < 3.2 ? Tone(age, .2) : 0;
        }
        static double AlternatingPhrases(double time)
        {
            // Sustained, connected, dense, connected and sustained phrases weave rhythm and lead.
            return time < 4 ? Tone(time, .2) : time < 8 ? Tone((time - 4) % .6, 3)
                : time < 11 ? Tone((time - 8) % .25, 14) : time < 15 ? Tone((time - 11) % .6, 3)
                : Tone(time - 15, .2);
        }
        static double LeadResolves(double time)
        {
            double phrase = time % 7;
            return phrase < 2.5 ? Tone(phrase % .25, 14) : phrase < 6.5 ? Tone(phrase - 2.5, .2) : 0;
        }
        static double PageArc(double time)
        {
            return time < 7 ? Tone(time % .25, 14) : time < 14 ? Tone((time - 7) % .6, 3) : Tone(time - 14, .2);
        }
        static double QuietLoudQuiet(double time)
        {
            if (time < 6) return Tone(time, .2) * .35;
            if (time < 14)
            {
                double age = time - 6;
                double envelope = .2 + .8 * Math.Pow(Math.Sin(Math.PI * age / 8), 2);
                return Tone(age % .25, 10) * envelope;
            }
            return Tone(time - 14, .2) * .35;
        }
        var failures = new List<string>();
        foreach (int callbackMs in new[] { 10, 100, 200 })
        {
            var dense = Run("dense plucks", callbackMs, 20, t => Tone(t % .25, 14));
            if (dense.Count != 0)
                failures.Add($"Continuous dense plucks manufactured an artist encounter at {callbackMs} ms.");
            var lead = Run("fast phrases resolving to held notes", callbackMs, 20, LeadResolves);
            if (!lead.ContainsKey(StageStyle.HardRock) || lead.Count != 1)
                failures.Add($"Lead phrases did not exclusively unlock HardRock at {callbackMs} ms.");
            if (Run("constant oscillator", callbackMs, 20, t => Tone(t, 0)).Count != 0)
                failures.Add($"A constant oscillator unlocked an achievement at {callbackMs} ms.");
            var held = Run("separated held notes", callbackMs, 28.8, HeldNotes);
            if (!held.ContainsKey(StageStyle.Mellow) || held.Count != 1)
                failures.Add($"Separated held notes did not exclusively unlock Mellow at {callbackMs} ms.");
            var alternating = Run("woven held/flowing/dense phrases", callbackMs, 20, AlternatingPhrases);
            if (!alternating.ContainsKey(StageStyle.Psychedelic) || alternating.Count != 1)
                failures.Add($"Alternating phrases did not exclusively unlock Psychedelic at {callbackMs} ms.");
            void Artist(string name, StageStyle expected, double duration, Func<double, double> signal)
            {
                var result = Run(name, callbackMs, duration, signal);
                if (!result.ContainsKey(expected) || result.Keys.Any(x => (int)x >= 4 && x != expected))
                    failures.Add($"{name} did not reveal the intended new artist exclusively at {callbackMs} ms.");
            }
            Artist("riff transition and held resolution", StageStyle.JimmyPage, 22, PageArc);
            Artist("extended held notes", StageStyle.DavidGilmour, 38.4, t => t % 6.4 < 6 ? Tone(t % 6.4, .2) : 0);
            Artist("phrases with breathing room", StageStyle.BBKing, 20, t => t % 4 < 3.2 ? Tone(t % 4, .2) : 0);
            Artist("riff relays", StageStyle.AngusYoung, 24, t => t % 5 < 4.2 ? Tone((t % 5) % .25, 14) : 0);
            Artist("quiet loud quiet dynamic arc", StageStyle.KurtCobain, 22, QuietLoudQuiet);
            Artist("connected rhythm strums", StageStyle.NileRodgers, 24, t => Tone(t % .6, 3));
        }
        Check(failures.Count == 0, string.Join(" ", failures));
    }

    public static void ExpandedArtistBoundaries()
    {
        var detector = new StageAchievementDetector();
        // Uncertain labels during continuous sound must not masquerade as rests between phrases.
        for (int i = 0; i <= 240; i++)
        {
            double t = i / 10d;
            var events = detector.Observe(Frame(t, t % 4 < 3 ? PlayingExpression.Sustained : PlayingExpression.None), true, true);
            Check(events.All(x => x.Style != StageStyle.BBKing), "Label gaps impersonated musical rests.");
        }
        detector.Reset();
        for (int i = 0; i <= 360; i++)
        {
            double t = i / 10d;
            bool active = t % 9 < 4;
            Check(detector.Observe(Frame(t, PlayingExpression.Sustained), true, active).All(x => x.Style != StageStyle.BBKing),
                "Long disconnected pauses counted as call-and-response.");
        }
        detector.Reset();
        Check(Feed(detector, 0, 40, _ => PlayingExpression.Animated).Count == 0,
            "Continuous dense playing must not unlock an artist without a phrase shape.");
        detector.Reset();
        Check(Feed(detector, 0, 40, t => (int)(t / 4) % 2 == 0 ? PlayingExpression.Sustained : PlayingExpression.Animated)
            .All(x => x.Style != StageStyle.KurtCobain && x.Style != StageStyle.JimmyPage),
            "Density changes without dynamics or flowing phrases unlocked unrelated artists.");
    }

    public static void PersistenceAndFailures()
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var folder = Path.GetFullPath(Path.Combine(root, "GuitarSoul-achievement-" + Guid.NewGuid().ToString("N")));
        if (!folder.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture is outside the temporary root.");
        try
        {
            var path = Path.Combine(folder, "achievements.json");
            var detector = new StageAchievementDetector();
            var hard = Feed(detector, 0, 14, t => t % 7 < 2.5 ? PlayingExpression.Animated : PlayingExpression.Sustained)
                .First(x => x.Style == StageStyle.HardRock);
            detector.Reset();
            var mellow = Feed(detector, 0, 14, _ => PlayingExpression.Sustained,
                onsets: t => (long)(t / 3.4) + 1, activity: t => t % 3.4 < 3).First(x => x.Style == StageStyle.Mellow);
            var store = new StageAchievements(path);
            Check(!File.Exists(path), "Opening the collection must not write.");
            try { store.Select(StageStyle.HardRock); throw new Exception("Locked cosmetic selected."); } catch (InvalidOperationException) { }
            Check(store.Award(hard, Now), "First observed event should award.");
            string original = File.ReadAllText(path);
            Check(!store.Award(hard, Now.AddDays(1)) && File.ReadAllText(path) == original, "Repeated event must be a no-op.");
            store.Select(StageStyle.HardRock);
            var other = new StageAchievements(path);
            Check(other.Selected == StageStyle.HardRock && other.Earned.Single().UnlockedAt == Now, "Award or equip lost after reopening.");
            other.Award(mellow, Now.AddSeconds(1));
            string newest = File.ReadAllText(path);
            try { store.Award(mellow, Now); throw new Exception("Stale write accepted."); } catch (IOException) { }
            Check(File.ReadAllText(path) == newest && !store.IsUnlocked(StageStyle.Mellow), "Failed save changed disk or memory.");
            other.Select(StageStyle.Everyday);
            Check(new StageAchievements(path).Earned.Count == 2, "Restoring ordinary appearance must preserve achievements.");
            // Read the exact numeric v1 format shipped before the roster expansion.
            File.WriteAllText(path, "{\"SchemaVersion\":1,\"Selected\":1,\"Earned\":[{\"Style\":1,\"UnlockedAt\":\"2026-10-21T12:00:00+08:00\",\"RuleVersion\":1}]}");
            var legacy = new StageAchievements(path);
            detector.Reset();
            var funk = Feed(detector, 0, 20, _ => PlayingExpression.Flowing,
                onsets: t => (long)(t * 2), pulseConfidence: _ => .9).First(x => x.Style == StageStyle.NileRodgers);
            legacy.Award(funk, Now.AddDays(1)); legacy.Select(StageStyle.NileRodgers);
            var expanded = new StageAchievements(path);
            Check(expanded.Earned.Count == 2 && expanded.IsUnlocked(StageStyle.HardRock) &&
                expanded.Selected == StageStyle.NileRodgers && expanded.Earned.First(x => x.Style == StageStyle.HardRock).UnlockedAt == Now &&
                expanded.Earned.First(x => x.Style == StageStyle.HardRock).RuleVersion == 1 &&
                expanded.Earned.First(x => x.Style == StageStyle.NileRodgers).RuleVersion == 2,
                "Roster expansion changed a previous achievement or failed to persist the new artist.");
            foreach (var broken in new[] { "{broken", "{\"SchemaVersion\":9}", "{\"SchemaVersion\":1,\"Selected\":1,\"Earned\":[]}",
                         "{\"SchemaVersion\":1,\"Selected\":0,\"Earned\":[null]}",
                         "{\"SchemaVersion\":1,\"Selected\":0,\"Earned\":[{\"Style\":1,\"UnlockedAt\":\"2026-10-21T12:00:00+08:00\",\"RuleVersion\":3}]}" })
            {
                File.WriteAllText(path, broken);
                try { _ = new StageAchievements(path); throw new Exception("Invalid achievement file accepted."); } catch (InvalidDataException) { }
                Check(File.ReadAllText(path) == broken, "Invalid file was overwritten.");
            }
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
