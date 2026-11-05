using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

/// <summary>Contrasts sound evidence on the same clock; a timer-driven gesture sequence cannot pass.</summary>
internal static class ResponsivenessChecks
{
    private const double Seconds = .05;
    private static readonly ListeningFeatures Dense = new(ToneColor.Bright, PlayingExpression.Animated, 1);
    private static readonly ListeningFeatures Held = new(ToneColor.Warm, PlayingExpression.Sustained, 1);
    private static readonly ListeningFeatures Sparse = new(ToneColor.Warm, PlayingExpression.None, 1);
    private static readonly ListeningFeatures Flowing = new(ToneColor.Warm, PlayingExpression.Flowing, 1);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static bool Moving(CompanionPose pose) =>
        pose.Action is CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam;
    private static CompanionPose Tick(CompanionDirector director, ListeningFeatures features, int tick,
        ExpressionStyle style = ExpressionStyle.Reserved, double drive = .8, bool active = true,
        bool quiet = false, bool steady = false, bool? onset = null) =>
        director.Step(active, steady, onset ?? (tick % 4 == 0), Seconds, quiet, style, drive, features);

    public static void SameClockDifferentSound()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var dense = new CompanionDirector(18);
            var held = new CompanionDirector(18);
            var sparse = new CompanionDirector(18);
            var silent = new CompanionDirector(18);
            double firstMovement = double.PositiveInfinity;
            int engaged = 0;
            for (int i = 0; i < 2400; i++)
            {
                var strong = Tick(dense, Dense, i, style);
                var single = Tick(held, Held, i, style, drive: .95, steady: true, onset: i == 0);
                var weak = Tick(sparse, Sparse, i, style, drive: 0, onset: i % 24 == 0);
                var stopped = Tick(silent, ListeningFeatures.Empty, i, style, drive: 0, active: false, onset: false);
                if (Moving(strong)) firstMovement = Math.Min(firstMovement, (i + 1) * Seconds);
                if (strong.Action == CompanionAction.Engage) engaged++;
                Check(!Moving(single) && !Moving(weak) && !Moving(stopped),
                    $"{style} treated a held note, sparse notes or silence as dense articulation at {(i + 1) * Seconds}");
                Check(strong.Engagement is >= 0 and <= 1 && single.Engagement == 0 && weak.Engagement == 0,
                    "Continuous musical response escaped its bounds or used stale drive on a held note");
                if (i >= 60)
                    Check(Moving(strong) && strong.HasMusicalEvidence && strong.Engagement >= .8,
                        $"{style} stopped responding to ongoing dense sound at {(i + 1) * Seconds}: {strong}");
            }
            Check(firstMovement <= 1.1 && engaged > 1800,
                $"{style} waits for a fixed admission timer or turns ongoing intensity into sparse clips: {firstMovement}, {engaged}");
            Check(silent.Current.Action == CompanionAction.Rest, "Silence did not reach the existing resting state");
        }
    }

    public static void PhraseChangesInterruptTheCurrentPose()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector(18);
            for (int i = 0; i < 60; i++) Tick(director, Dense, i, style);
            Check(director.Current.Action == CompanionAction.Engage, "Dense phrase fixture never engaged");

            // A real held-note observation overrides stale tempo and drive from the previous riff.
            for (int i = 0; i < 40; i++) Tick(director, Held, i, style, drive: .95, steady: true, onset: false);
            Check(director.Current.Action == CompanionAction.Listen && director.Current.Engagement < .02,
                $"{style} failed to withdraw movement when dense notes became one held note");

            for (int i = 0; i < 40; i++) Tick(director, Flowing, i, style, drive: .05, onset: i % 10 == 0);
            Check(director.Current.Action == CompanionAction.Groove && director.Current.Engagement is > .25 and < .45,
                $"{style} cannot respond gently to a flowing phrase after a held note: {director.Current}");

            double firstEngage = double.PositiveInfinity;
            for (int i = 0; i < 60; i++)
            {
                var pose = Tick(director, Dense, i, style);
                if (pose.Action == CompanionAction.Engage) firstEngage = Math.Min(firstEngage, (i + 1) * Seconds);
            }
            Check(firstEngage <= 1.1, $"{style} ignored a new dense phrase because an old gesture was cooling down");

            for (int i = 0; i < 40; i++) Tick(director, Sparse, i, style, drive: 0, onset: false);
            Check(director.Current.Action == CompanionAction.Listen && director.Current.Engagement < .02,
                $"{style} kept moving after articulation density was withdrawn while sound remained active");
        }
    }

    public static void RelativeIntensityAndPersonality()
    {
        var firstEngage = new Dictionary<ExpressionStyle, double>();
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector(18);
            for (int i = 0; i < 60; i++)
            {
                var pose = Tick(director, Dense, i, style, drive: .45);
                if (pose.Action == CompanionAction.Engage && !firstEngage.ContainsKey(style))
                    firstEngage[style] = (i + 1) * Seconds;
            }
            double moderate = director.Current.Engagement;
            for (int i = 0; i < 30; i++) Tick(director, Dense, i, style, drive: 1);
            double stronger = director.Current.Engagement;
            Check(stronger > moderate + .1, $"{style} repeated one fixed intensity despite denser articulation evidence");

            for (int i = 0; i < 30; i++)
                Tick(director, Dense with { Dynamics = DynamicContour.Easing }, i, style, drive: 1);
            double eased = director.Current.Engagement;
            for (int i = 0; i < 30; i++)
                Tick(director, Dense with { Dynamics = DynamicContour.Building }, i, style, drive: 1);
            Check(eased < stronger - .12 && director.Current.Engagement > eased + .12,
                $"{style} failed to follow relative easing and building during ongoing articulation");
        }
        Check(firstEngage.Count == 2 && firstEngage[ExpressionStyle.Curious] + .15 < firstEngage[ExpressionStyle.Reserved]
            && firstEngage.Values.All(time => time <= 1.5),
            "The same phrase must promptly reach both personalities while curiosity responds earlier");
    }

    public static void JoinedPhrasesNeedFreshEvidence()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector(18);
            var starts = new List<double>();
            var durations = new List<double>();
            CompanionAction previous = CompanionAction.Idle;
            double phraseSeconds = 0;
            // A brief change during cooldown expires; it cannot queue a clip for a later timer tick.
            for (int i = 0; i < 1800; i++)
            {
                var features = i is >= 300 and < 320 ? Dense with { Dynamics = DynamicContour.Building } : Dense;
                var pose = Tick(director, features, i, style);
                if (pose.Action == CompanionAction.Jam)
                {
                    if (previous != CompanionAction.Jam) starts.Add((i + 1) * Seconds);
                    phraseSeconds += Seconds;
                }
                else if (previous == CompanionAction.Jam)
                {
                    durations.Add(phraseSeconds);
                    phraseSeconds = 0;
                    Check(pose.Action == CompanionAction.Engage,
                        "Putting down the guitar forced a timed listening gap during continuing intense sound");
                }
                previous = pose.Action;
            }
            Check(starts.Count == 1 && starts[0] is >= 4 and <= 12 && durations.Count == 1,
                $"{style} joining ignored articulation evidence or looped on unchanged music: {string.Join(", ", starts)}");
            double expected = style == ExpressionStyle.Curious ? 6.4 : 4.8;
            Check(Math.Abs(durations[0] - expected) <= .1, "A joined phrase exceeded its bounded duration");

            bool joinedChangedPhrase = false;
            for (int i = 0; i < 60; i++)
                joinedChangedPhrase |= Tick(director, Dense with { Dynamics = DynamicContour.Building }, i, style).Action == CompanionAction.Jam;
            Check(joinedChangedPhrase, $"{style} cannot join a changed phrase once the guitar cooldown has elapsed");
            var stop = Tick(director, ListeningFeatures.Empty, 0, style, drive: 0, active: false, onset: false);
            Check(stop.Action == CompanionAction.Settle, "Stopping failed to immediately dismiss the joined phrase");
            for (int i = 0; i < 660; i++)
            {
                var pose = Tick(director, ListeningFeatures.Empty, i, style, drive: 0, active: false, onset: false);
                Check(!Moving(pose), "Silence restarted musical movement after putting the guitar down");
            }
            Check(director.Current.Action == CompanionAction.Rest, "Stopping a joined phrase failed to restore rest");
        }
    }

    public static void QuietAndLostEvidence()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector(18);
            for (int i = 0; i < 60; i++) Tick(director, Dense, i, style);
            for (int i = 0; i < 100; i++)
            {
                var pose = Tick(director, Dense, i, style, drive: 1, quiet: true, steady: true);
                Check(pose.Action == CompanionAction.Listen && pose.Weight == 0 && pose.Engagement == 0,
                    "Quiet did not immediately override continuous musical response");
            }
            director.Reset();
            for (int i = 0; i < 60; i++) Tick(director, Dense, i, style);
            for (int i = 0; i < 40; i++)
                Tick(director, Dense with { Confidence = double.NaN }, i, style, drive: 0, onset: false);
            Check(director.Current.Action == CompanionAction.Listen && director.Current.Engagement == 0
                && !director.Current.HasMusicalEvidence,
                "Losing reliable evidence retained a continuous musical response");
            director.Reset();
            Check(director.Current.Action == CompanionAction.Idle && director.Current.Engagement == 0
                && !director.Current.HasMusicalEvidence, "Reset retained musical response state");
        }
    }

    public static void MusicalReplay()
    {
        var director = new CompanionDirector(819);
        CompanionPose[] Run() => Enumerable.Range(0, 2400).Select(i =>
        {
            int phase = i % 800;
            var features = phase < 300 ? Dense : phase < 400 ? Held : phase < 600 ? Flowing : ListeningFeatures.Empty;
            return Tick(director, features, i, ExpressionStyle.Curious,
                drive: phase < 300 ? .8 : .05, active: phase < 600, onset: phase < 300 && i % 4 == 0);
        }).ToArray();
        var first = Run();
        director.Reset();
        var second = Run();
        Check(first.SequenceEqual(second), "The same actual listening evidence is not replayable after reset");
        Check(first.Any(pose => pose.Action == CompanionAction.Jam) && first.Any(pose => pose.Action == CompanionAction.Groove)
            && first.Any(pose => pose.Action == CompanionAction.Listen), "Replay fixture missed joined, gentle or held-note responses");
    }

    public static void MotionContinuity()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var director = new CompanionDirector(18);
            var actions = new HashSet<CompanionAction>();
            bool leftGuitar = false;
            CompanionAction previous = CompanionAction.Idle;
            // The phrase continues after the bounded guitar gesture. Its motion must not
            // fade to zero just because the character puts the guitar down or changes intensity.
            for (int i = 0; i < 700; i++)
            {
                bool flowing = i is >= 480 and < 560;
                var pose = Tick(director, flowing ? Flowing : Dense, i, style, drive: flowing ? .05 : .8);
                actions.Add(pose.Action);
                leftGuitar |= previous == CompanionAction.Jam && pose.Action != CompanionAction.Jam;
                if (i >= 40)
                    Check(Moving(pose) && pose.Weight >= .99,
                        $"{style} continuing phrase lost movement at {(i + 1) * Seconds:F2}s: {previous} -> {pose.Action}, weight={pose.Weight:F3}");
                previous = pose.Action;
            }
            Check(leftGuitar && actions.Contains(CompanionAction.Jam) && actions.Contains(CompanionAction.Groove)
                && actions.Contains(CompanionAction.Engage), "Continuity fixture missed a guitar or intensity transition");
            var quiet = Tick(director, Dense, 0, style, quiet: true);
            Check(quiet.Weight == 0 && quiet.Engagement == 0, "Motion continuity overrode quiet");
            var resumed = Tick(director, Dense, 0, style);
            Check(resumed.Weight < .5, "Returning from quiet reused an old full movement envelope");
            for (int i = 0; i < 40; i++) Tick(director, Held, i, style, onset: false);
            Check(director.Current.Action == CompanionAction.Listen && director.Current.Weight == 0,
                "Continuity carried movement into a held note");
            director.Reset();
            var restarted = Tick(director, Dense, 0, style);
            Check(restarted.Weight < .5, "Reset retained the previous musical movement envelope");

            for (int i = 0; i < 240 && director.Current.Action != CompanionAction.Jam; i++)
                Tick(director, Dense, i, style);
            Check(director.Current.Action == CompanionAction.Jam, "Gap fixture never joined");
            for (int i = 0; i < 10; i++) Tick(director, Dense, i, style);
            var gap = Tick(director, ListeningFeatures.Empty, 0, style, drive: 0, active: false, onset: false);
            Check(gap.Action == CompanionAction.Settle, "A gap must begin settling, not invent more playing");
            var recovered = Tick(director, Dense, 0, style);
            Check(recovered.Action == CompanionAction.Engage && recovered.Weight >= .99,
                "A single missing evidence tick threw away the still-visible movement on recovery");
            // A real stop still finishes, and the next phrase does not inherit stale motion.
            for (int i = 0; i < 80; i++)
                Tick(director, ListeningFeatures.Empty, i, style, drive: 0, active: false, onset: false);
            Check(!Moving(director.Current), "Continuity kept moving during a real stop");
            var freshPhrase = Tick(director, Dense, 0, style);
            Check(freshPhrase.Weight < .5, "A new phrase reused an expired settle envelope");
        }
    }
}
