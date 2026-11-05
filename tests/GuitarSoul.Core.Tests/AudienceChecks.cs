using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

internal static class AudienceChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static float[] Tone(int rate, double seconds, double gain, bool changing)
        => Enumerable.Range(0, (int)(rate * seconds)).Select(i =>
        {
            double t = i / (double)rate;
            double frequency = changing && (int)(t / .4) % 2 == 1 ? 880 : 110;
            return (float)(gain * Math.Tanh(3 * Math.Sin(2 * Math.PI * frequency * t)));
        }).ToArray();
    public static void CompressedTone()
    {
        foreach (int rate in new[] { 8000, 44100 })
        {
            var notes = Tone(rate, 10, .2, true);
            var whole = new RhythmTracker(rate).Process(notes);
            var tracker = new RhythmTracker(rate);
            RhythmFrame split = RhythmFrame.Empty;
            for (int i = 0; i < notes.Length; i += 137)
                split = tracker.Process(notes.AsSpan(i, Math.Min(137, notes.Length - i)));
            Check(whole == split, "Timbre features depend on callback chunks");
            Check(whole.OnsetCount >= 16 && whole.Drive > .3, $"Compressed changing notes were ignored at {rate}: {whole}");
            var quietGain = new RhythmTracker(rate).Process(Tone(rate, 10, .035, true));
            Check(Math.Abs(quietGain.OnsetCount - whole.OnsetCount) <= 1 && Math.Abs(quietGain.Drive - whole.Drive) < .02, "Gain changes interpreted as musical character");
            var sustain = new RhythmTracker(rate).Process(Tone(rate, 10, .2, false));
            Check(sustain.OnsetCount <= 2 && sustain.Drive == 0, $"Static distortion creates fake playing: {sustain}");
            var silence = tracker.Process(new float[rate * 3]);
            Check(silence.Drive == 0 && silence.TempoBpm is null, "Motion survives silence");
        }
    }
    public static void ExpressiveResponse()
    {
        var director = new CompanionDirector(18);
        int engages = 0, jams = 0;
        for (int i = 0; i < 600; i++)
        {
            var pose = director.Step(true, false, i % 8 == 0, .05, false, ExpressionStyle.Reserved, .7);
            if (pose.Action == CompanionAction.Engage && i < 180) engages++;
            if (pose.Action == CompanionAction.Jam && i < 500) jams++;
        }
        Check(engages > 10 && jams > 10, "Expressive free rhythm waits for a tempo lock");
        var silent = director.Step(false, false, false, .05, false, ExpressionStyle.Reserved, 0);
        Check(silent.Action != CompanionAction.Jam, "Cat keeps playing after stop");
        var quiet = director.Step(true, true, true, .05, true, ExpressionStyle.Curious, .9);
        Check(quiet.Weight == 0 && quiet.Action == CompanionAction.Listen, "Quiet lost priority");
    }
    public static void Selection()
    {
        var policy = new AudienceRiffPolicy();
        var picks = new List<(double Time, AudienceRiff Pick)>();
        for (int i = 1; i < 18000; i++)
        {
            double t = i * .05;
            bool active = t % 110 < 13;
            var frame = new RhythmFrame(t, active ? t : t - 2, (long)(t / .4), null, 0) { Drive = .7, Brightness = .4 };
            var pick = policy.Step(frame, active, true, false);
            if (pick is not null) picks.Add((t, pick));
        }
        Check(picks.Count == 6, "Per-session selection limit missing or never reachable");
        Check(picks[0].Time >= 14 && picks[0].Time < 16 && picks[0].Pick.Seconds is > 13 and < 17, "Phrase boundary selection failed");
        Check(picks.Zip(picks.Skip(1), (a,b) => b.Time-a.Time).All(t => t >= 90), "Save cooldown missing");
        foreach (var (enabled,inhibited) in new[] { (false,false), (true,true) })
        {
            policy.Reset();
            for (int i=1;i<1000;i++)
                Check(policy.Step(new(i*.05, i*.05, i, null, 0), true, enabled, inhibited) is null, "Opt-out or playback/calibration saves audio");
        }
        policy.Reset();
        for (int i=1;i<1000;i++)
            Check(policy.Step(new(i*.05, 0, 1, null, 0), true, true, false) is null, "Unchanging sound selected as a phrase");
    }
    public static void SelectionBoundaries()
    {
        var policy = new AudienceRiffPolicy();
        for (int i = 1; i <= 140; i++)
        {
            double t = i * .05;
            Check(policy.Step(new RhythmFrame(t, t, i, null, 0) { Drive = .7, Brightness = .4 }, true, true, false) is null,
                "A phrase was selected before its minimum active duration");
        }

        // A stale callback breaks the phrase so UI stalls cannot count as continuous playing.
        Check(policy.Step(new RhythmFrame(7.75, 7.75, 160, null, 0) { Drive = .7, Brightness = .4 }, true, true, false) is null,
            "A stale callback unexpectedly selected a phrase");
        for (int i = 156; i <= 295; i++)
        {
            double t = 7.8 + (i - 156) * .05;
            Check(policy.Step(new RhythmFrame(t, t, i, null, 0) { Drive = .7, Brightness = .4 }, true, true, false) is null,
                "A short post-stall phrase was combined with an earlier phrase");
        }
        for (int i = 1; i <= 24; i++)
        {
            double t = 14.8 + i * .05;
            Check(policy.Step(new RhythmFrame(t, t - 2, 300 + i, null, 0), false, true, false) is null,
                "A phrase was selected without enough active audio after a stale callback");
        }

        policy.Reset();
        AudienceRiff? longPick = null;
        for (int i = 1; i <= 700 && longPick is null; i++)
        {
            double t = i * .05;
            longPick = policy.Step(new RhythmFrame(t, t, (long)(t / .25), null, 0) { Drive = .8, Brightness = .5 }, true, true, false);
        }
        Check(longPick is not null && longPick.Seconds is > 24 and <= 28,
            "A continuously active phrase exceeded the bounded automatic clip duration");
    }
    public static void Pipeline()
    {
        const int rate = 8000;
        var samples = Tone(rate, 14, .2, true);
        var tracker = new RhythmTracker(rate); var director = new CompanionDirector(18); var policy = new AudienceRiffPolicy();
        long previous = 0; bool engaged = false; AudienceRiff? selected = null;
        for(int i=0;i<rate*17;i+=400)
        {
            bool active = i < samples.Length;
            var frame = tracker.Process(active ? samples.AsSpan(i,400) : new float[400]);
            var pose = director.Step(active, false, frame.OnsetCount > previous, .05, false, ExpressionStyle.Reserved, frame.Drive);
            previous = frame.OnsetCount;
            engaged |= pose.Action is CompanionAction.Engage or CompanionAction.Jam;
            selected ??= policy.Step(frame, active, true, false);
        }
        Check(engaged && selected is not null, "Compressed signal never reaches reaction and automatic selection");
    }
}
