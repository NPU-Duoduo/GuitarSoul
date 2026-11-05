using GuitarSoul.Core.Companion;

internal static class JamChecks
{
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    private static CompanionPose Tick(CompanionDirector d, int i, bool active = true, bool steady = true, bool quiet = false) =>
        d.Step(active, steady, active && i % 10 == 0, .05, quiet, ExpressionStyle.Reserved);

    public static void AdmissionAndCooldown()
    {
        var sustained = new CompanionDirector(18);
        var silent = new CompanionDirector(18);
        var rhythmic = new CompanionDirector(18);
        var starts = new List<double>();
        var durations = new List<double>();
        bool jamming = false;
        double duration = 0;
        for (int i = 0; i < 7200; i++)
        {
            Check(Tick(sustained, i, steady: false).Action != CompanionAction.Jam, "Sustain alone invited jamming");
            Check(Tick(silent, i, active: false, steady: false).Action != CompanionAction.Jam, "Silence invited jamming");
            var pose = Tick(rhythmic, i);
            if (pose.Action == CompanionAction.Jam)
            {
                if (!jamming) { starts.Add(i * .05); duration = 0; }
                duration += .05;
                Check(pose.Mood == CompanionMood.Excited && pose.Weight is >= 0 and <= 1, "Jam pose invalid");
            }
            else if (jamming) durations.Add(duration);
            jamming = pose.Action == CompanionAction.Jam;
        }
        Check(starts.Count is >= 2 and <= 4 && starts[0] >= 45, "Jam must be reachable but occasional");
        Check(starts.Zip(starts.Skip(1), (a, b) => b - a).All(gap => gap >= 90), "Jam cooldown failed");
        Check(durations.Count >= 2 && durations.All(d => d is >= 4.7 and <= 4.9), "Jam failed to finish its short phrase");
    }

    public static void Interruption()
    {
        CompanionDirector Ready()
        {
            var d = new CompanionDirector(18);
            for (int i = 0; i < 2400; i++)
                if (Tick(d, i).Action == CompanionAction.Jam) return d;
            throw new Exception("Fixture never reached Jam");
        }
        var silent = Ready();
        Check(Tick(silent, 0, active: false, steady: false).Action == CompanionAction.Settle, "Jam did not settle on stop");
        for (int i = 0; i < 650; i++) Check(Tick(silent, i, active: false, steady: false).Action != CompanionAction.Jam, "Silence restarted Jam");
        Check(silent.Current.Action == CompanionAction.Rest, "Jam did not return to existing rest state");
        var quiet = Ready();
        var stopped = Tick(quiet, 0, quiet: true);
        Check(stopped.Action == CompanionAction.Listen && stopped.Weight == 0, "Quiet did not immediately dismiss Jam");
        for (int i = 0; i < 800; i++) Check(Tick(quiet, i).Action != CompanionAction.Jam, "Unquiet restarted Jam immediately");
        quiet.Reset();
        Check(quiet.Current.Action == CompanionAction.Idle, "Reset retained Jam state");
    }
}
