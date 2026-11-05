using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

/// <summary>Exercises actual sample analysis through the same activity bridge used by the app.</summary>
internal static class ListeningPipelineChecks
{
    private const int Rate = 8000;
    private const double StepSeconds = .05;
    private sealed record Observation(double Time, CompanionPose Pose, string? Words, ListeningFeatures Evidence,
        double SpeechEvidenceSeconds);

    private sealed class Session(ExpressionStyle style)
    {
        private readonly RhythmTracker _rhythm = new(Rate);
        private readonly SignalAnalyzer _signal = new() { NoiseFloorDb = -90 };
        private readonly CompanionDirector _director = new(18);
        private readonly CompanionVoice _voice = new();
        private long _previousOnsets;
        private double _speechEvidenceSeconds;
        public RhythmFrame Latest { get; private set; }

        public Observation Tick(float[] samples, bool quiet = false)
        {
            Latest = _rhythm.Process(samples);
            var signal = _signal.Process(samples);
            double attackAge = Latest.AudioSeconds - Latest.LastOnsetSeconds;
            bool groove = Latest.TempoBpm is double tempo && Latest.Confidence >= .7 &&
                attackAge < Math.Min(1.5, 90 / tempo);
            bool onset = Latest.OnsetCount > _previousOnsets && attackAge < .6;
            _previousOnsets = Latest.OnsetCount;
            bool active = signal.IsActive || attackAge < .35 || groove;
            var features = active ? Latest.Listening : ListeningFeatures.Empty;
            // Motion may bridge a beat; speech confirmation only counts actual sound.
            if (signal.IsActive && features.Confidence >= .6) _speechEvidenceSeconds += StepSeconds;
            var words = _voice.Step(features, signal.IsActive, quiet, style, StepSeconds);
            var pose = _director.Step(active, groove, onset, StepSeconds, quiet, style, Latest.Drive, features);
            return new(Latest.AudioSeconds, pose, words, _voice.Source, _speechEvidenceSeconds);
        }
    }

    private static float[] Samples(int tick, Func<double, double> signal)
    {
        var result = new float[400];
        for (int sample = 0; sample < result.Length; sample++)
            result[sample] = (float)signal((tick * result.Length + sample) / (double)Rate);
        return result;
    }

    private static double Plucks(double time)
    {
        double age = time % .5;
        return age < .1 ? .2 * Math.Sin(2 * Math.PI * 196 * time) : 0;
    }

    private static double CompressedNotes(double time)
    {
        double frequency = (int)(time / .4) % 2 == 0 ? 110 : 880;
        return .15 * Math.Tanh(3 * Math.Sin(2 * Math.PI * frequency * time));
    }

    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }

    public static void ShortPlucksReachBothPersonalities()
    {
        var firstWords = new Dictionary<ExpressionStyle, string>();
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var session = new Session(style);
            var observations = Enumerable.Range(0, 900).Select(i => session.Tick(Samples(i, Plucks))).ToArray();
            Check(session.Latest.OnsetCount >= 85 && session.Latest.Confidence >= .7,
                $"Short-pluck fixture lost its real 120/min articulation: {session.Latest}");
            var first = observations.FirstOrDefault(o => o.Words is not null);
            Check(first is not null, $"{style} never responds to 45 seconds of 100ms plucks every 500ms; per-note confidence gaps must not erase the phrase");
            Check(first!.SpeechEvidenceSeconds >= (style == ExpressionStyle.Curious ? 4 : 6),
                $"{style} counted the silent parts of detached notes as new speech evidence");
            Check(first.Evidence.Confidence >= .6 && first.Evidence.Expression != PlayingExpression.Sustained,
                $"{style} spoke without reliable evidence, or called separate short notes a sustain");
            Check(observations.Count(o => o.Words is not null) >= 80,
                $"{style} erased its readable observation during detached-note gaps");
            firstWords[style] = first.Words!;
            Check(observations.Any(o => o.Pose.Action is CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam),
                $"{style} failed to visibly acknowledge real repeated articulation");
            Check(observations.Count(o => o.Words is not null) < observations.Length / 2,
                $"{style} made words a permanent overlay instead of an occasional response");
        }
        Check(firstWords[ExpressionStyle.Curious] != firstWords[ExpressionStyle.Reserved],
            "The same actual plucked phrase lost the two personalities' different words");
    }

    public static void ChangingNotesReachVoiceAndDistinctBehavior()
    {
        var firstEngage = new Dictionary<ExpressionStyle, double>();
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var session = new Session(style);
            var observations = Enumerable.Range(0, 600).Select(i => session.Tick(Samples(i, CompressedNotes))).ToArray();
            Check(session.Latest.OnsetCount >= 65, "Compressed-note fixture must retain the real changing notes");
            Check(observations.Any(o => o.Words is not null),
                $"{style} hears dense changing notes but never speaks; changing tone must not reset a stable articulation observation forever");
            var engage = observations.FirstOrDefault(o => o.Pose.Action == CompanionAction.Engage);
            Check(engage is not null, $"{style} never engages with real changing compressed notes");
            firstEngage[style] = engage!.Time;
        }
        Check(firstEngage[ExpressionStyle.Curious] < firstEngage[ExpressionStyle.Reserved],
            $"Real samples erased personality timing: curious={firstEngage[ExpressionStyle.Curious]}, reserved={firstEngage[ExpressionStyle.Reserved]}");
    }

    public static void SilenceClearsAndSustainNeverJams()
    {
        foreach (var style in Enum.GetValues<ExpressionStyle>())
        {
            var session = new Session(style);
            for (int i = 0; i < 600; i++) session.Tick(Samples(i, CompressedNotes));
            Observation? quiet = null;
            for (int i = 0; i < 60; i++)
            {
                quiet = session.Tick(new float[400]);
                if (i >= 20) Check(quiet.Words is null && quiet.Pose.Action != CompanionAction.Jam,
                    $"{style} continued an acoustic observation or Jam after silence");
            }
            Check(session.Latest.Listening == ListeningFeatures.Empty && quiet!.Evidence == ListeningFeatures.Empty,
                $"{style} retained tone evidence after three seconds of silence");

            session = new Session(style);
            bool heardWords = false;
            for (int i = 0; i < 800; i++)
            {
                var frame = session.Tick(Samples(i, t => .15 * Math.Sin(2 * Math.PI * 196 * t)));
                heardWords |= frame.Words is not null;
                Check(frame.Pose.Action is not (CompanionAction.Jam or CompanionAction.Groove or CompanionAction.Engage),
                    $"{style} manufactured a rhythmic performance from a synthetic held note");
            }
            Check(heardWords && session.Latest.Listening.Expression == PlayingExpression.Sustained,
                $"{style} must have an actual quiet response to a held note");
            var suppressed = session.Tick(Samples(800, t => .15 * Math.Sin(2 * Math.PI * 196 * t)), quiet: true);
            Check(suppressed.Words is null && suppressed.Evidence == ListeningFeatures.Empty && suppressed.Pose.Weight == 0,
                $"{style} ignored quiet mode in the complete sample-to-response pipeline");
        }
    }
}
