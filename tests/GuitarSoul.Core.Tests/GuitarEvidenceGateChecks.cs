using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

internal static class GuitarEvidenceGateChecks
{
    private const int Rate = 16000;
    private const int Block = 800;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    private static (GuitarEvidenceFrame Evidence, List<CompanionAction> Actions, int ConfirmedTicks) Run(
        Func<double, double> sample, double duration, bool gateBehavior = true)
    {
        var analyzer = new SignalAnalyzer { ActivityThresholdDb = -45, NoiseFloorDb = -90 };
        var rhythm = new RhythmTracker(Rate);
        var gate = new GuitarEvidenceGate();
        var director = new CompanionDirector(417);
        var actions = new List<CompanionAction>();
        int confirmedTicks = 0;
        long previousOnsets = 0;
        var pcm = new float[Block];
        for (int offset = 0; offset < duration * Rate; offset += Block)
        {
            int count = Math.Min(Block, (int)(duration * Rate) - offset);
            for (int i = 0; i < count; i++) pcm[i] = (float)sample((offset + i) / (double)Rate);
            var signal = analyzer.Process(pcm.AsSpan(0, count));
            var frame = rhythm.Process(pcm.AsSpan(0, count), -45);
            var evidence = gate.Observe(frame, signal);
            if (evidence.Decision == GuitarEvidenceDecision.Confirmed) confirmedTicks++;
            double age = frame.AudioSeconds - frame.LastOnsetSeconds;
            bool groove = frame.TempoBpm is double tempo && frame.Confidence >= .7 && age < Math.Min(1.5, 90 / tempo);
            bool onset = frame.OnsetCount > previousOnsets && age < .6;
            bool active = signal.IsActive || age < .35 || groove;
            var pose = director.Step(active, groove, onset, count / (double)Rate, false,
                ExpressionStyle.Curious, frame.Drive, active ? frame.Listening : ListeningFeatures.Empty,
                gateBehavior ? evidence.Decision : GuitarEvidenceDecision.Confirmed);
            actions.Add(pose.Action);
            previousOnsets = frame.OnsetCount;
        }
        return (gate.Current, actions, confirmedTicks);
    }

    private static double TriadProgression(double time)
    {
        // Two-second C/G changes with four picked attacks per second. This is a synthetic
        // acoustic control, not a claim that the gate recognizes either chord or a player.
        int root = new[] { 0, 7, 9, 5 }[(int)(time / 2) % 4];
        double fundamental = 130.81278265 * Math.Pow(2, root / 12d);
        double age = time % .25;
        double envelope = Math.Exp(-age * 5);
        return .075 * envelope * (Math.Sin(2 * Math.PI * fundamental * time) +
            Math.Sin(2 * Math.PI * fundamental * Math.Pow(2, 4 / 12d) * time) +
            Math.Sin(2 * Math.PI * fundamental * Math.Pow(2, 7 / 12d) * time));
    }

    public static void ConservativeEvidence()
    {
        var guitar = Run(TriadProgression, 12);
        Check(guitar.Actions.Contains(CompanionAction.Jam), "A changing picked triad control never admitted Jam.");
        Check(guitar.Evidence.Decision == GuitarEvidenceDecision.Confirmed,
            "A changing picked triad control did not leave positive acoustic evidence.");

        double SpeechLike(double time)
        {
            double f0 = 115 + 22 * Math.Sin(2 * Math.PI * .7 * time);
            double syllable = .25 + .75 * Math.Pow(Math.Sin(2 * Math.PI * 2.3 * time), 2);
            return .08 * syllable * (Math.Sin(2 * Math.PI * f0 * time) +
                .35 * Math.Sin(2 * Math.PI * 720 * time) + .2 * Math.Sin(2 * Math.PI * 1180 * time));
        }
        double Typing(double time)
        {
            double age = time % .19;
            if (age >= .025) return 0;
            int n = (int)(time * Rate);
            double noise = Math.Sin(n * 12.9898) * 43758.5453;
            return .2 * (noise - Math.Floor(noise) - .5) * Math.Exp(-age * 120);
        }
        var random = new Random(1907);
        var noise = Enumerable.Range(0, Rate * 12).Select(_ => (random.NextDouble() - .5) * .08).ToArray();
        var speech = Run(SpeechLike, 12);
        var keyboard = Run(Typing, 12);
        var environment = Run(time => noise[Math.Min(noise.Length - 1, (int)(time * Rate))], 12);
        var silence = Run(_ => 0, 12);
        foreach (var (name, result) in new[] { ("speech", speech), ("keyboard", keyboard),
                     ("environment", environment), ("silence", silence) })
        {
            Check(result.Evidence.Decision == GuitarEvidenceDecision.Unknown, name + " acquired guitar evidence: " + result.Evidence);
            Check(!result.Actions.Contains(CompanionAction.Jam), name + " entered Jam; confirmed ticks: " +
                result.ConfirmedTicks + "; final evidence: " + result.Evidence);
        }
    }

    public static void InvalidAndStaleEvidence()
    {
        var analyzer = new SignalAnalyzer { ActivityThresholdDb = -45, NoiseFloorDb = -90 };
        var rhythm = new RhythmTracker(Rate);
        var gate = new GuitarEvidenceGate();
        var pcm = new float[Block];
        RhythmFrame frame = RhythmFrame.Empty;
        SignalFrame signal = SignalFrame.Silence;
        for (int offset = 0; offset < 12 * Rate; offset += Block)
        {
            for (int i = 0; i < pcm.Length; i++) pcm[i] = (float)TriadProgression((offset + i) / (double)Rate);
            signal = analyzer.Process(pcm);
            frame = rhythm.Process(pcm);
            gate.Observe(frame, signal);
        }
        Check(gate.Current.Decision == GuitarEvidenceDecision.Confirmed, "Fixture never confirmed evidence.");

        var stale = frame with { AudioSeconds = frame.AudioSeconds + .61 };
        Check(gate.Observe(stale, signal).Decision == GuitarEvidenceDecision.Unknown, "A stale callback retained evidence.");
        gate.Observe(frame with { AudioSeconds = stale.AudioSeconds + .05 }, signal);
        Check(gate.Observe(frame with { AudioSeconds = stale.AudioSeconds + .10 }, signal with { IsClipping = true }).Decision ==
            GuitarEvidenceDecision.Unknown, "Clipping retained evidence.");
        Check(gate.Observe(frame with { AudioSeconds = stale.AudioSeconds + .15 }, signal, eligible: false).Decision ==
            GuitarEvidenceDecision.Unknown, "Calibration or review eligibility manufactured evidence.");
        gate.Reset();
        Check(gate.Observe(RhythmFrame.Empty, SignalFrame.Silence).Decision == GuitarEvidenceDecision.Unknown,
            "No-data input acquired evidence.");
    }

    public static void OnlyGuitarSpecificOutputsAreBlocked()
    {
        var feature = new ListeningFeatures(ToneColor.Warm, PlayingExpression.Animated, 1);
        var director = new CompanionDirector(417);
        var actions = new List<CompanionAction>();
        for (int i = 0; i < 400; i++)
            actions.Add(director.Step(true, true, i % 5 == 0, .05, false, ExpressionStyle.Curious, .8, feature,
                GuitarEvidenceDecision.Unknown).Action);
        Check(actions.Any(x => x is CompanionAction.Groove or CompanionAction.Engage),
            "Unknown evidence suppressed ordinary musical movement.");
        Check(actions.All(x => x != CompanionAction.Jam), "Unknown evidence allowed Jam.");

        var voice = new CompanionVoice();
        for (int i = 0; i < 200; i++)
            Check(voice.Step(feature, true, false, ExpressionStyle.Curious, .05, GuitarEvidenceDecision.Unknown) is null,
                "Unknown evidence produced a guitar-specific sentence.");

        director.Reset();
        bool jammed = false;
        for (int i = 0; i < 400; i++)
            jammed |= director.Step(true, true, i % 5 == 0, .05, false, ExpressionStyle.Curious, .8, feature,
                GuitarEvidenceDecision.Confirmed).Action == CompanionAction.Jam;
        Check(jammed, "Explicitly confirmed evidence killed the existing computer-playback response policy.");
    }

    public static void AchievementAdmission()
    {
        static List<StageTrigger> Feed(GuitarEvidenceDecision evidence)
        {
            var detector = new StageAchievementDetector();
            var earned = new List<StageTrigger>();
            for (int i = 0; i <= 140; i++)
            {
                double time = i / 10d;
                var expression = time % 7 < 2.5 ? PlayingExpression.Animated : PlayingExpression.Sustained;
                var frame = new RhythmFrame(time, time, i * 4, null, 0)
                {
                    Listening = new(ToneColor.Warm, expression, 1)
                };
                earned.AddRange(detector.Observe(frame, true, true, null, evidence));
            }
            return earned;
        }

        Check(Feed(GuitarEvidenceDecision.Unknown).Count == 0,
            "Unknown guitar evidence admitted a synthetic achievement sequence.");
        var confirmed = Feed(GuitarEvidenceDecision.Confirmed);
        Check(confirmed.Count == 1 && confirmed[0].Style == StageStyle.HardRock,
            "Confirmed guitar evidence killed the existing synthetic achievement check.");
    }
}
