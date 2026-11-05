using GuitarSoul.Core.Audio;

internal static class ListeningFeatureChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static void ToneAndGain()
    {
        foreach (int rate in new[] { 8000, 44100, 48000 })
        foreach (double gain in new[] { .035, .35 })
        {
            var low = new RhythmTracker(rate).Process(Tone(rate, 3, gain, 110));
            var high = new RhythmTracker(rate).Process(Tone(rate, 3, gain, 2200));
            Check(low.Listening.Tone == ToneColor.Warm, $"Low tone lost warm observation at {rate}/{gain}: {low.Listening}");
            Check(high.Listening.Tone == ToneColor.Bright, $"High tone lost bright observation at {rate}/{gain}: {high.Listening}");
            Check(low.Listening.Confidence > .9 && high.Listening.Confidence > .9, "Stable audible evidence never becomes confident");
            Check(low.Listening.Expression == PlayingExpression.Sustained && high.Listening.Expression == PlayingExpression.Sustained,
                "Pitch or gain alone is interpreted as animated playing");
            Check(low.OnsetCount == 1 && high.OnsetCount == 1, "A steady tone repeatedly triggers attacks");
        }
    }

    public static void Articulation()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double gain in new[] { .035, .3 })
        {
            var flowing = new RhythmTracker(rate).Process(ChangingNotes(rate, 6, .6, _ => gain));
            var animated = new RhythmTracker(rate).Process(ChangingNotes(rate, 6, .25, _ => gain));
            Check(flowing.Listening.Expression == PlayingExpression.Flowing,
                $"Spaced connected notes not distinguished from a sustain at {rate}/{gain}: {flowing.Listening}");
            Check(animated.Listening.Expression == PlayingExpression.Animated,
                $"Dense attacks not distinguished from spaced notes at {rate}/{gain}: {animated.Listening}");
            Check(flowing.Listening.Dynamics == DynamicContour.Steady && animated.Listening.Dynamics == DynamicContour.Steady,
                "Constant relative level fabricates a phrase contour");
            Check(animated.OnsetCount > flowing.OnsetCount + 8, "Expression result lacks real articulation evidence");
        }
    }

    public static void GateReleaseAndHold()
    {
        const int rate = 8000;
        var tracker = new RhythmTracker(rate);
        var heard = tracker.Process(Tone(rate, 2, .2, 110));
        Check(heard.Listening.Tone == ToneColor.Warm && heard.Listening.Expression == PlayingExpression.Sustained, "Precondition: stable sound was not heard");
        var brief = tracker.Process(new float[rate / 10]);
        Check(brief.Listening.Tone == heard.Listening.Tone && brief.Listening.Expression == heard.Listening.Expression,
            "A 100 ms musical gap discards the held observation");
        Check(brief.Listening.Confidence == heard.Listening.Confidence,
            "A short musical gap loses already established evidence");
        var releasing = tracker.Process(new float[(int)(rate * .45)]);
        Check(releasing.Listening.Confidence < .6 && releasing.Listening.Confidence > 0,
            "A longer pause does not release the held observation");
        var silence = tracker.Process(new float[rate]);
        Check(silence.Listening == ListeningFeatures.Empty, "Silence leaves a stale timbre or expression");
        var noise = tracker.Process(Tone(rate, 2, .001, 2200));
        Check(noise.Listening == ListeningFeatures.Empty, "Below-gate noise is given a personality-driving tone");
        var restart = tracker.Process(Tone(rate, .1, .2, 2200));
        Check(restart.Listening.Tone == ToneColor.Unknown && restart.Listening.Confidence < .2,
            "A new short signal inherited earlier evidence or immediately classified colour");
        var bright = tracker.Process(Tone(rate, 2, .2, 2200));
        Check(bright.Listening.Tone == ToneColor.Bright && bright.Listening.Expression == PlayingExpression.Sustained,
            "A fresh phrase failed to establish its own observations");

        var transient = new RhythmTracker(rate);
        transient.Process(Tone(rate, 2, .2, 110));
        var flash = transient.Process(Tone(rate, .04, .2, 2200));
        Check(flash.Listening.Tone == ToneColor.Warm, "A 40 ms timbre flash changes the stable observation");
        var changed = transient.Process(Tone(rate, 1, .2, 2200));
        Check(changed.Listening.Tone == ToneColor.Bright, "Hysteresis prevents a real sustained timbre change");

        var detached = new RhythmTracker(rate);
        for (int beat = 0; beat < 12; beat++)
        {
            detached.Process(Tone(rate, .1, .2, 220));
            var betweenNotes = detached.Process(new float[(int)(rate * .4)]);
            if (beat >= 7)
                Check(betweenNotes.Listening.Expression == PlayingExpression.Flowing && betweenNotes.Listening.Confidence >= .6,
                    $"Ordinary 100 ms notes / 400 ms gaps never establish a stable phrase: {betweenNotes.Listening}");
        }
    }

    public static void RelativeDynamics()
    {
        foreach (int rate in new[] { 8000, 44100 })
        foreach (double gain in new[] { .3, 1.0 })
        {
            var building = new RhythmTracker(rate).Process(ChangingNotes(rate, 4, .25, t => gain * .08 * Math.Pow(8, t / 4)));
            var easing = new RhythmTracker(rate).Process(ChangingNotes(rate, 4, .25, t => gain * .64 * Math.Pow(8, -t / 4)));
            Check(building.Listening.Dynamics == DynamicContour.Building, $"Gradual relative rise ignored at {rate}/{gain}: {building.Listening}");
            Check(easing.Listening.Dynamics == DynamicContour.Easing, $"Gradual relative fall ignored at {rate}/{gain}: {easing.Listening}");
            Check(building.Listening.Confidence > .9 && easing.Listening.Confidence > .9,
                "Supported contours did not retain audible evidence");
        }
        var pluck = new RhythmTracker(8000);
        bool falseContour = false;
        var samples = Enumerable.Range(0, 32000).Select(i => (float)(.4 * Math.Exp(-i / 8000d * .65) * Math.Sin(2 * Math.PI * 220 * i / 8000))).ToArray();
        RhythmFrame frame = RhythmFrame.Empty;
        for (int i = 0; i < samples.Length; i += 400)
        {
            frame = pluck.Process(samples.AsSpan(i, 400));
            falseContour |= frame.Listening.Dynamics != DynamicContour.Steady;
        }
        Check(!falseContour && frame.OnsetCount == 1, "A single decaying pluck fabricates phrase dynamics or fresh attacks");
    }

    public static void ChunkIndependence()
    {
        const int rate = 44100;
        var notes = ChangingNotes(rate, 4, .25, t => .08 * Math.Pow(8, t / 4));
        var samples = notes.Concat(new float[rate / 3]).Concat(Tone(rate, 1.137, .15, 110)).ToArray();
        var whole = new RhythmTracker(rate).Process(samples);
        var tracker = new RhythmTracker(rate);
        var random = new Random(312);
        RhythmFrame split = RhythmFrame.Empty;
        for (int i = 0; i < samples.Length;)
        {
            int count = Math.Min(random.Next(1, 1501), samples.Length - i);
            split = tracker.Process(samples.AsSpan(i, count));
            i += count;
        }
        Check(whole == split, $"Perception depends on capture callback size: whole {whole.Listening}, split {split.Listening}");
        Check(whole.Listening.Expression == PlayingExpression.Sustained && whole.Listening.Tone == ToneColor.Warm,
            "Final sustained phrase did not replace the earlier articulated phrase");
        Check(tracker.Process([]) == split, "An empty callback changes accumulated observations");
        var invalid = new RhythmTracker(rate).Process(Enumerable.Repeat(float.NaN, rate).ToArray());
        Check(invalid.Listening == ListeningFeatures.Empty, "Invalid driver input produces an observation");
    }

    private static float[] Tone(int rate, double seconds, double gain, double frequency)
        => Enumerable.Range(0, (int)(rate * seconds)).Select(i => (float)(gain * Math.Sin(2 * Math.PI * frequency * i / rate))).ToArray();

    private static float[] ChangingNotes(int rate, double seconds, double period, Func<double, double> gain)
        => Enumerable.Range(0, (int)(rate * seconds)).Select(i =>
        {
            double t = i / (double)rate;
            double frequency = (int)(t / period) % 2 == 0 ? 110 : 880;
            return (float)(gain(t) * Math.Tanh(3 * Math.Sin(2 * Math.PI * frequency * t)));
        }).ToArray();
}
