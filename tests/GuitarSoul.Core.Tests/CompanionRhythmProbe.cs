using System.Text;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

/// <summary>
/// Replays ordinary playing patterns through the director and prints the pose timeline.
/// The point is to see what the cat actually does across minutes, not to assert a rule.
/// </summary>
internal static class CompanionRhythmProbe
{
    private sealed record Scene(string Name, string Note,
        Func<double, (bool Active, bool Steady, bool Onset, ListeningFeatures Listening, double Drive)> At,
        double Seconds);

    public static string Report()
    {
        var scenes = new[]
        {
            // The most common thing a player does: strum the same groove for a while.
            new Scene("steady strum, 120 bpm, medium", "同一段扫弦三分钟，力度不变", (t) =>
            {
                bool onset = Beat(t, 120);
                return (true, true, onset, new ListeningFeatures(ToneColor.Warm, PlayingExpression.Flowing, 1)
                    { PluckedPresence = .8 }, .5);
            }, 180),

            // Same groove, but the player leans into it.
            new Scene("steady strum, driven", "同样的扫弦，但弹得更用力", (t) =>
            {
                bool onset = Beat(t, 120);
                return (true, true, onset, new ListeningFeatures(ToneColor.Bright, PlayingExpression.Animated, 1)
                    { PluckedPresence = .9 }, .9);
            }, 180),

            new Scene("slow arpeggio, quiet", "慢速分解，力度轻", (t) =>
            {
                bool onset = Beat(t, 60);
                return (true, true, onset, new ListeningFeatures(ToneColor.Warm, PlayingExpression.Flowing, 1)
                    { PluckedPresence = .3 }, .15);
            }, 180),

            // Play, put the guitar down, pick it up again.
            new Scene("play 30s / rest 30s x3", "弹一会儿放下一会儿", (t) =>
            {
                bool playing = t % 60 < 30;
                if (!playing) return (false, false, false, ListeningFeatures.Empty, 0);
                bool onset = Beat(t, 120);
                return (true, true, onset, new ListeningFeatures(ToneColor.Warm, PlayingExpression.Flowing, 1)
                    { PluckedPresence = .8 }, .5);
            }, 180),

            new Scene("crescendo", "从轻到重渐强", (t) =>
            {
                double progress = Math.Clamp(t / 40, 0, 1);
                bool onset = Beat(t, 100);
                var dynamics = progress < .4 ? DynamicContour.Building
                    : progress > .85 ? DynamicContour.Easing : DynamicContour.Steady;
                return (true, true, onset, new ListeningFeatures(ToneColor.Warm,
                    progress > .5 ? PlayingExpression.Animated : PlayingExpression.Flowing, 1)
                    { Dynamics = dynamics, PluckedPresence = .7 }, .2 + .7 * progress);
            }, 60),

            new Scene("held notes", "长音", (t) =>
                (true, false, Math.Abs(t % 6) < .05,
                    new ListeningFeatures(ToneColor.Warm, PlayingExpression.Sustained, 1), 0), 60)
        };

        var text = new StringBuilder();
        foreach (var scene in scenes)
        {
            text.AppendLine($"== {scene.Name}  ({scene.Note})");
            text.AppendLine(Run(scene));
            text.AppendLine();
        }
        return text.ToString();
    }

    private static bool Beat(double time, double bpm)
    {
        double period = 60 / bpm;
        return Math.Abs(time % period) < .026;
    }

    private static string Run(Scene scene)
    {
        var director = new CompanionDirector(417);
        var timeline = new List<(double At, CompanionAction Action)>();
        var engagement = new List<double>();
        var gazes = new List<double>();
        var leans = new List<double>();
        var tails = new List<double>();
        var eagers = new List<double>();
        var action = CompanionAction.Idle;
        double dt = .05;
        for (double t = 0; t < scene.Seconds; t += dt)
        {
            var (active, steady, onset, listening, drive) = scene.At(t);
            var pose = director.Step(active, steady, onset, dt, false, ExpressionStyle.Reserved, drive, listening);
            if (pose.Action != action)
            {
                action = pose.Action;
                timeline.Add((t, action));
            }
            engagement.Add(pose.Engagement);
            gazes.Add(pose.Gaze);
            leans.Add(pose.Lean);
            tails.Add(pose.Tail);
            eagers.Add(pose.Eagerness ?? 0);
        }

        var text = new StringBuilder();
        var runs = new List<(CompanionAction Action, double Start, double End)>();
        for (int i = 0; i < timeline.Count; i++)
        {
            double end = i + 1 < timeline.Count ? timeline[i + 1].At : scene.Seconds;
            runs.Add((timeline[i].Action, timeline[i].At, end));
        }
        text.AppendLine("  timeline:");
        foreach (var run in runs)
            text.AppendLine($"    {run.Start,6:0.0}s  {run.Action,-8} {run.End - run.Start,5:0.0}s");

        var counts = runs.GroupBy(r => r.Action).ToDictionary(g => g.Key, g => g.Count());
        text.AppendLine($"  changes: {runs.Count}, " +
            string.Join(", ", counts.OrderByDescending(k => k.Value).Select(k => $"{k.Key}×{k.Value}")));
        var sustained = runs.Where(r => r.End - r.Start > 20).ToList();
        if (sustained.Count > 0)
            text.AppendLine($"  held >20s: {string.Join(", ", sustained.Select(r => $"{r.Action} {r.End - r.Start:0}s"))}");
        if (engagement.Count > 0)
            text.AppendLine($"  engagement: min {engagement.Min():0.00} max {engagement.Max():0.00} " +
                $"avg {engagement.Average():0.00}");

        // The cat's own clock: does the body keep changing while the music does not?
        double away = gazes.Count(g => Math.Abs(g) > .5) / (double)gazes.Count;
        var signatures = new HashSet<(CompanionAction, int, int, int, int)>();
        for (int i = 0; i < gazes.Count; i++)
            signatures.Add((timeline.Count == 0 ? CompanionAction.Idle : ActionAt(timeline, i * dt),
                (int)Math.Round(gazes[i] * 1.6), (int)Math.Round(leans[i] * 2),
                (int)Math.Round(tails[i] * 3), (int)Math.Round(eagers[i] * 3)));
        text.AppendLine($"  own clock: gaze {gazes.Min():+0.00;-0.00}..{gazes.Max():+0.00;-0.00} " +
            $"(away {away:P0}), lean {leans.Min():+0.0;-0.0}..{leans.Max():+0.0;-0.0}, " +
            $"tail {tails.Min():0.00}..{tails.Max():0.00}, eager {eagers.Min():0.00}..{eagers.Max():0.00}");
        text.AppendLine($"  distinct visible poses: {signatures.Count} over {gazes.Count} frames " +
            $"({signatures.Count / (double)gazes.Count:P1} unique)");

        // The renderer multiplies engagement by eagerness before rounding to pixels. If that
        // product never moves, the cat taps with one fixed force no matter how long it plays.
        var strikes = new List<double>();
        for (int i = 0; i < engagement.Count; i++)
            if (engagement[i] > 0)
                strikes.Add((.65 + .85 * engagement[i]) * (.4 + .8 * eagers[i]));
        if (strikes.Count > 0)
        {
            // Sample once a second: sub-pixel easing is not what a player notices, a whole
            // minute of the same force is.
            var sampled = new List<double>();
            for (int i = 0; i < strikes.Count; i += 20) sampled.Add(strikes[i]);
            int flat = 0, longestFlat = 0;
            for (int i = 1; i < sampled.Count; i++)
            {
                flat = Math.Abs(sampled[i] - sampled[i - 1]) < .03 ? flat + 1 : 0;
                longestFlat = Math.Max(longestFlat, flat);
            }
            double mean = strikes.Average();
            double spread = Math.Sqrt(strikes.Select(s => (s - mean) * (s - mean)).Average());
            text.AppendLine($"  strike force: {strikes.Min():0.00}..{strikes.Max():0.00} " +
                $"(spread {spread:0.00}), longest flat stretch {longestFlat}s");
        }
        return text.ToString().TrimEnd();
    }

    private static CompanionAction ActionAt(List<(double At, CompanionAction Action)> timeline, double time)
    {
        var action = CompanionAction.Idle;
        foreach (var entry in timeline)
        {
            if (entry.At > time) break;
            action = entry.Action;
        }
        return action;
    }
}
