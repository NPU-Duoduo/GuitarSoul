using GuitarSoul.Core.Audio;

/// <summary>Diagnostic only: how the current triad detector behaves on a broken chord.</summary>
internal static class ArpeggioProbe
{
    private static float[] Build(int rate, double seconds, (int Note, double Start)[] notes, double decay, double gain)
    {
        var samples = new float[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate, value = 0;
            foreach (var (note, start) in notes)
            {
                if (t < start) continue;
                double age = t - start;
                double env = Math.Exp(-age / decay) * Math.Min(1, age / .01);
                double f = 440 * Math.Pow(2, (note - 69) / 12d);
                value += env * (Math.Sin(2 * Math.PI * f * t) + .35 * Math.Sin(4 * Math.PI * f * t) + .2 * Math.Sin(6 * Math.PI * f * t));
            }
            samples[i] = (float)(gain * value);
        }
        return samples;
    }

    internal static string Report()
    {
        var cases = new (string Name, int[] Notes, double Spacing, double Decay, double Seconds)[]
        {
            ("fast C arpeggio 160ms", [60, 64, 67, 72], .16, .60, 4.5),
            ("medium C arpeggio 280ms", [60, 64, 67, 72], .28, .55, 4.5),
            ("slow C arpeggio 420ms", [60, 64, 67, 72], .42, .50, 5.0),
            ("slow Am arpeggio 420ms", [57, 60, 64, 69], .42, .50, 5.0),
            ("single-note line (no chord)", [60, 62, 64, 65, 67, 69, 71, 72], .35, .40, 4.2),
            ("single repeated note", [64], .35, .40, 4.2)
        };
        var report = new System.Text.StringBuilder();
        foreach (var (name, notes, spacing, decay, seconds) in cases)
        {
            var events = new List<(int, double)>();
            for (int cycle = 0; cycle * notes.Length * spacing < seconds; cycle++)
                for (int n = 0; n < notes.Length; n++)
                    events.Add((notes[n], (cycle * notes.Length + n) * spacing));
            report.AppendLine($"{name}: {Measure(events, decay, seconds)}");
        }
        return report.ToString();
    }

    private static string Measure(List<(int Note, double Start)> events, double decay, double seconds)
    {
        const int rate = 48000, block = 2400;
        var samples = Build(rate, seconds, events.ToArray(), decay, .25);
        var tracker = new RhythmTracker(rate);
        var timeline = new ChordTimeline();
        double clock = 0;
        int frames = 0, known = 0, phraseKnown = 0;
        var seen = new List<string>();
        for (int i = 0; i + block <= samples.Length; i += block)
        {
            var listening = tracker.Process(samples.AsSpan(i, block)).Listening;
            var h = listening.Phrase;
            clock += block / (double)rate;
            frames++;
            if (listening.Harmony.IsKnown) known++;
            if (h.IsKnown) phraseKnown++;
            timeline.Observe(h, clock);
            var text = h.IsKnown ? $"{PitchNames.Of(h.Root)}{(h.Quality == ChordQuality.Minor ? "m" : "")}"
                : h.RootOnly ? $"{PitchNames.Of(h.Root)}5" : "-";
            if (seen.Count == 0 || seen[^1] != text) seen.Add(text);
        }
        timeline.Close();
        return $"frame={known}/{frames} phrase={phraseKnown}/{frames} segments={timeline.Count} " +
               $"progression=[{timeline.Describe(new KeySignature(0, false))}] seq=[{string.Join(" ", seen.Take(24))}]";
    }
}
