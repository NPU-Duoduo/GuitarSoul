using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

internal static class CompanionDirectorChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static CompanionPose Tick(CompanionDirector director, bool active = true, bool steady = false,
        bool onset = false, bool quiet = false, ExpressionStyle style = ExpressionStyle.Reserved) =>
        director.Step(active, steady, onset, .05, quiet, style);
    public static void NoticeAndPause()
    {
        var director = new CompanionDirector(13);
        Check(Tick(director, onset: true).Action == CompanionAction.Notice, "Onset was not noticed");
        var weights = Enumerable.Range(0, 12).Select(_ => Tick(director).Weight).ToArray();
        Check(weights.Max() > .9 && weights[0] < weights[4], "Notice must enter gradually");
        for (int i = 0; i < 30; i++) Tick(director);
        Check(director.Current.Action == CompanionAction.Listen, "Notice did not finish");
        for (int i = 0; i < 8; i++) Check(Tick(director, false).Action == CompanionAction.Listen, "Short pause caused a twitch");
        Check(Tick(director, onset: true).Action == CompanionAction.Listen, "Repeated note restarted a notice");
    }
    public static void Cooldowns()
    {
        var director = new CompanionDirector(8);
        var starts = new List<double>(); int listen = 0, notices = 0;
        var previous = CompanionAction.Idle;
        for (int i = 0; i < 3600; i++)
        {
            var pose = Tick(director, steady: true, onset: i % 10 == 0);
            Check(pose.Weight is >= 0 and <= 1, "Envelope exceeded bounds");
            if (pose.Action == CompanionAction.Listen) listen++;
            if (pose.Action != previous && pose.Action == CompanionAction.Notice) notices++;
            if (pose.Action != previous && pose.Action == CompanionAction.Engage) starts.Add(i * .05);
            previous = pose.Action;
        }
        Check(starts.Count is >= 1 and <= 3, "Engagement must be sparse but reachable");
        Check(starts.Zip(starts.Skip(1), (a, b) => b - a).All(gap => gap >= 65), "Engagement cooldown broken");
        Check(notices == 1 && listen > 1800, "Continuous rhythm became constant animation");
    }
    public static void Sustain()
    {
        var director = new CompanionDirector();
        for (int i = 0; i < 3600; i++)
        {
            var pose = Tick(director, onset: i == 0);
            Check(pose.Action is not (CompanionAction.Groove or CompanionAction.Engage), "A long sound was treated as a beat or reward");
        }
        Check(director.Current.Action == CompanionAction.Listen, "Sustain should be quietly heard");
    }
    public static void Silence()
    {
        var director = new CompanionDirector();
        for (int i = 0; i < 150; i++) Tick(director, steady: true, onset: i % 10 == 0);
        bool settled = false; int settles = 0; var previous = director.Current.Action;
        for (int i = 0; i < 660; i++)
        {
            var pose = Tick(director, false);
            settled |= pose.Action == CompanionAction.Settle;
            if (pose.Action == CompanionAction.Settle && previous != pose.Action && i < 100) settles++;
            previous = pose.Action;
            Check(pose.Action != CompanionAction.Engage, "Stopping must not trigger applause");
        }
        Check(settled && director.Current.Action == CompanionAction.Rest, "Silence did not settle into rest");
        Check(settles == 1, "One stop should have exactly one settle, without briefly returning to listening");
    }
    public static void Quiet()
    {
        var director = new CompanionDirector();
        for (int i = 0; i < 100; i++) Tick(director, steady: true, onset: i % 10 == 0);
        Check(director.Current.Action == CompanionAction.Groove, "Fixture must interrupt a gesture");
        for (int i = 0; i < 1000; i++)
        {
            var pose = Tick(director, active: i < 100, steady: true, onset: true, quiet: true);
            Check(pose.Weight == 0 && pose.Action is CompanionAction.Listen or CompanionAction.Idle or CompanionAction.Rest, "Quiet leaked an autonomous gesture");
        }
        Check(director.Current.Action == CompanionAction.Rest, "Quiet must retain meaningful resting state");
    }
    public static void Replay()
    {
        var director = new CompanionDirector(819);
        CompanionPose[] Run() => Enumerable.Range(0, 2400).Select(i => Tick(director,
            active: i % 800 < 420, steady: i % 800 < 300, onset: i % 10 == 0)).ToArray();
        var first = Run(); director.Reset(); var second = Run();
        Check(first.SequenceEqual(second), "Replay changed after reset");
        Check(first.Any(p => p.Action == CompanionAction.Groom), "Fixture did not exercise idle behavior");
    }
    public static void Styles()
    {
        double Duration(ExpressionStyle style, CompanionAction action)
        {
            var director = new CompanionDirector(); int count = 0;
            for (int i = 0; i < 140; i++)
            {
                var pose = Tick(director, active: i < 50, onset: i == 0, style: style);
                if (pose.Action == action) count++;
            }
            return count * .05;
        }
        Check(Duration(ExpressionStyle.Curious, CompanionAction.Notice) > Duration(ExpressionStyle.Reserved, CompanionAction.Notice) + .3, "Attention duration should differ");
        Check(Duration(ExpressionStyle.Curious, CompanionAction.Settle) > Duration(ExpressionStyle.Reserved, CompanionAction.Settle) + .4, "Settling style should differ");
    }
    public static void Timing()
    {
        var director = new CompanionDirector();
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, -.1 })
        {
            bool rejected = false;
            try { director.Step(false, false, false, invalid, false, ExpressionStyle.Reserved); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Invalid time accepted");
        }
        var resumed = director.Step(false, false, false, 3600, false, ExpressionStyle.Reserved);
        Check(resumed.Action == CompanionAction.Idle, "Resume skipped into an old idle gesture");
    }
    public static void Gain()
    {
        CompanionAction[] Run(double gain)
        {
            var tracker = new RhythmTracker(8000);
            var director = new CompanionDirector(24);
            var frames = new List<CompanionAction>(); long previous = 0;
            for (int i = 0; i < 1200; i++)
            {
                var samples = new float[400];
                if (i % 10 == 0)
                    for (int sample = 0; sample < 240; sample++) samples[sample] = (float)(gain * Math.Sin(sample * 2 * Math.PI * 220 / 8000));
                var rhythm = tracker.Process(samples);
                bool newOnset = rhythm.OnsetCount > previous;
                bool active = rhythm.AudioSeconds - rhythm.LastOnsetSeconds < .55;
                frames.Add(Tick(director, active, rhythm.Confidence >= .7, newOnset).Action);
                previous = rhythm.OnsetCount;
            }
            return frames.ToArray();
        }
        var low = Run(.025); var high = Run(.3);
        Check(low.SequenceEqual(high), "Input gain changed chosen actions for equivalent detectable rhythms");
        Check(low.Contains(CompanionAction.Groove), "Fixture did not produce groove");
    }
}
