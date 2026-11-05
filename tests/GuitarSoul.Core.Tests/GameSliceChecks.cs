using System.Text.Json;
using GuitarSoul.Core.Game;

internal static class GameSliceChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Throws(Action action)
    {
        try { action(); } catch (ArgumentException) { return; }
        throw new Exception("Invalid input was not rejected.");
    }
    internal static TrackArc Timeline(string id = "synthetic-track") => new(id, 45, [],
        [new("h1", 7, 10, 22, 23, "synthetic-reference", 1), new("h2", 27, 30, 40, 41, "synthetic-reference", 1)]);
    private static PlayerParticipation Player(double time, bool active, long epoch = 0) =>
        new(epoch, time, true, true, false, false, false, active, active ? time : 0, active, 0, null, active ? 1 : 0);
    private static PlaybackClock Clock(double position, double? now = null, long epoch = 0, PlaybackRunState state = PlaybackRunState.Playing) =>
        new("synthetic-track", epoch, position, now ?? position, state);

    public static void NoInputNeverCredits()
    {
        foreach (var kind in new[] { PlayerInputKind.Unselected, PlayerInputKind.IndependentDevice, PlayerInputKind.ComputerLoopback, PlayerInputKind.Mixed })
        {
            var director = new GameMomentDirector(Timeline());
            var tracker = new PlayerParticipationTracker(8000, new(kind, "chosen-input"));
            for (int i = 0; i <= 900; i++)
            {
                double time = i * .05;
                // Deliberately feed loud input for prohibited sources, silence for the independent source.
                tracker.Push(kind == PlayerInputKind.IndependentDevice ? new float[400] : Tone(i, .2), i, 0, time, time, null);
                var frame = director.Step(Clock(time), tracker.Read(time));
                Check(!frame.HoldGuitar && frame.SharedHighlights == 0 && frame.NewlySharedHighlightId is null,
                    $"{kind} received shared-highlight credit or a guitar at {time}.");
            }
        }
    }

    public static void SustainsAndTiming()
    {
        var director = new GameMomentDirector(Timeline());
        var tracker = new PlayerParticipationTracker(8000, new(PlayerInputKind.IndependentDevice, "test-input"));
        double firstFollowing = -1, firstJam = -1; int jamTicks = 0, releases = 0, awards = 0;
        for (int i = 0; i <= 900; i++)
        {
            double time = i * .05;
            tracker.Push(Tone(i, .08), i, 0, time, time, null);
            var player = tracker.Read(time);
            var frame = director.Step(Clock(time), player);
            if (frame.State == GameMomentState.Following && firstFollowing < 0) firstFollowing = time;
            if (frame.State == GameMomentState.Together && firstJam < 0) firstJam = time;
            if (time >= 10 && time < 22 && frame.HoldGuitar) jamTicks++;
            if (time >= 7 && time < 10) Check(frame.State == GameMomentState.Anticipating, "Three-second anticipation was missing.");
            if (time >= 22 && time < 23) { Check(frame.State == GameMomentState.Release && !frame.HoldGuitar, "Release leaked held guitar."); releases++; }
            if (frame.NewlySharedHighlightId is not null) awards++;
            if (time > 5 && time < 44) Check(player.IsActive && player.ContinuousSeconds >= 5, "Sustain lost participation without new onsets.");
        }
        Check(firstFollowing <= 1 && firstJam >= 10 && firstJam <= 10.5, "Feedback/highlight latency exceeded candidate limits.");
        Check(jamTicks >= 230 && releases >= 19 && awards == 2, "Sustain failed to cover the high window or double-counted it.");
    }

    public static void DropoutGraceAndRejoin()
    {
        var director = new GameMomentDirector(Timeline());
        bool shortGapHeld = true; double leave = -1, rejoin = -1; int awards = 0;
        for (int i = 0; i < 460; i++)
        {
            double time = i * .05;
            bool active = !(time >= 12 && time < 12.7) && !(time >= 15 && time < 17);
            var f = director.Step(Clock(time), Player(time, active));
            if (time >= 12 && time < 12.7) shortGapHeld &= f.HoldGuitar;
            if (time >= 15 && !f.HoldGuitar && leave < 0) leave = time;
            if (time >= 17 && f.HoldGuitar && rejoin < 0) rejoin = time;
            if (f.NewlySharedHighlightId is not null) awards++;
        }
        Check(shortGapHeld && leave is >= 15.8 and <= 16.2 && rejoin is >= 17 and <= 17.5, "Dropout grace / rejoin timing failed.");
        Check(awards == 1, "Rejoining the same high window granted another award.");
    }

    public static void TransportResets()
    {
        var d = new GameMomentDirector(Timeline());
        for (int i = 0; i <= 240; i++) d.Step(Clock(i * .05), Player(i * .05, true));
        Check(d.SharedHighlightIds.Count == 1, "Setup did not enter first highlight.");
        var paused = d.Step(Clock(12, 12.05, 1, PlaybackRunState.Paused), Player(12.05, true, 1));
        Check(paused.State == GameMomentState.Paused && !paused.HoldGuitar && paused.SyncSegments == 0, "Pause left transient game state.");
        var resumed = d.Step(Clock(12, 13, 2), Player(12, true, 0));
        Check(!resumed.HoldGuitar && resumed.Discontinuity, "Old input revived Jam on resume.");
        // Forward seek to h2: first update must discard prior transient participation.
        var seek = d.Step(Clock(32, 13.05, 3), Player(13.05, true, 3));
        Check(!seek.HoldGuitar && seek.NewlySharedHighlightId is null && seek.SyncSegments == 0, "Seek awarded or retained old high state.");
        for (int i = 1; i <= 30; i++) d.Step(Clock(32 + i * .05, 13.05 + i * .05, 3), Player(13.05 + i * .05, true, 3));
        Check(d.SharedHighlightIds.Count == 2, "Fresh input after seek could not rejoin.");
        d.Step(Clock(10, 14.6, 4), Player(14.6, true, 4));
        for (int i = 1; i <= 30; i++) d.Step(Clock(10 + i * .05, 14.6 + i * .05, 4), Player(14.6 + i * .05, true, 4));
        Check(d.SharedHighlightIds.Count == 2, "Loop/backward seek counted a HighlightId twice.");
        var jump = d.Step(Clock(31, 16.15, 4), Player(16.15, true, 4));
        Check(jump.Discontinuity && !jump.HoldGuitar, "Unannounced clock jump was not reset.");
        var stopped = d.Step(Clock(31, 16.2, 5, PlaybackRunState.Stopped), Player(16.2, true, 5));
        Check(stopped.State == GameMomentState.Ended && !stopped.HoldGuitar, "Stop did not win.");
        Check(d.Step(Clock(31.05, 16.25, 5), Player(16.25, true, 5)).State == GameMomentState.Ended, "Ended session restarted implicitly.");
        d.Load(Timeline("new-song"));
        var fresh = d.Step(new("new-song", 0, 0, 0, PlaybackRunState.Ready), Player(0, false));
        Check(fresh.SharedHighlights == 0 && fresh.HighlightId is null && fresh.SyncSegments == 0, "Song change retained prior session.");
    }

    public static void PriorityAndFreshness()
    {
        var d = new GameMomentDirector(Timeline());
        for (int i = 0; i <= 240; i++) d.Step(Clock(i * .05), Player(i * .05, true));
        var reduced = d.Step(Clock(12.05), Player(12.05, true), reducedMotion: true);
        Check(reduced.State == GameMomentState.Together && reduced.HoldGuitar && !reduced.Animate, "Reduced motion changed game outcome or retained animation.");
        var quiet = d.Step(Clock(12.1), Player(12.1, true), quiet: true);
        Check(quiet.State == GameMomentState.Quiet && !quiet.HoldGuitar && quiet.SyncSegments == 0, "Quiet did not override.");
        foreach (var bad in new[] { Player(12.15, true) with { Connected=false }, Player(12.15, true) with { IsClipping=true },
            Player(12.15, true) with { IsInvalid=true }, Player(12.15, true) with { Eligible=false },
            Player(12.15, true) with { Participation=double.NaN }, Player(11, true), Player(14, true) })
            Check(d.Step(Clock(12.15), bad).State == GameMomentState.InputUnavailable, "Unusable packet retained participation.");
        Check(d.Step(Clock(12.2),Player(12.1,true)).State==GameMomentState.InputUnavailable,
            "A cached packet from before input failure revived participation.");
        Throws(() => d.Step(Clock(11), Player(11, true)));
    }

    public static void TrackerContract()
    {
        var t = new PlayerParticipationTracker(8000, new(PlayerInputKind.IndependentDevice,"chosen"));
        var lowBeat = new TrackArcFrame(0, -10, 1, 1, 0, 120, .2, 0);
        for (int i = 0; i < 30; i++) t.Push(Tone(i,.1), i, 0, i*.05, i*.05, lowBeat);
        var a = t.Read(1.45);
        Check(a.IsActive && a.ContinuousSeconds >= 1.4 && a.BeatProximity is null && a.Participation == 1, "Low confidence beat was not disabled.");
        t.Push(Tone(30,.1),30,0,1.5,1.5,lowBeat with { BeatConfidence=.9 });
        Check(t.Read(1.5).IsActive && t.Read(1.5).Participation is >= .7 and <= 1,
            "A reliable beat made a long sustain lose participation.");
        t.Push(new float[400], 0, 0, 1.5, 1.5, lowBeat);
        Check(t.Read(1.5).IsActive, "Duplicate packet mutated activity.");
        Check(t.Read(1.8).IsStale && !t.Read(1.8).IsActive, "Stale audio retained contribution.");
        t.Reset(1); t.Push(Tone(0,.1), 50, 0, 2, 2, null);
        Check(t.Read(2).IsStale, "Previous epoch packet revived activity.");
        t.Push([float.NaN], 0, 1, 2.05, 2.05, null);
        Check(t.Read(2.05).IsInvalid && !t.Read(2.05).IsActive, "Invalid PCM was accepted.");
        t.Push(Enumerable.Repeat(1f,400).ToArray(), 1, 1, 2.1, 2.1, null);
        Check(t.Read(2.1).IsClipping && !t.Read(2.1).IsActive, "Clipped input was credited.");
        t.SetConnected(false,2); t.Push(Tone(0,.1), 0,2,3,3,null);
        Check(!t.Read(3).Connected && !t.Read(3).IsActive, "Disconnected device accepted PCM.");
    }

    public static void ArcRepeatAndContrast()
    {
        const int rate = 8000;
        var pcm = Enumerable.Range(0, rate * 65).Select(i => {
            double time=i/(double)rate;
            double gain = time is >= 20 and < 32 || time is >= 45 and < 57 ? .25 : .025;
            return (float)(gain * Math.Sin(2*Math.PI*220*time) * (.6 + .4*Math.Exp(-time%.5*14)));
        }).ToArray();
        var analyzer = new TrackArcAnalyzer();
        var first = analyzer.Analyze(pcm,rate,"fixture");
        var repeat = analyzer.Analyze(pcm,rate,"fixture");
        Check(JsonSerializer.Serialize(first)==JsonSerializer.Serialize(repeat), "Repeat analysis changed timeline or IDs.");
        Check(first.Highlights.Any(h=>h.Start is >= 18 and <= 23) && first.Highlights.Any(h=>h.Start is >= 43 and <= 48), "Relative energy sections were missed.");
        Check(first.Highlights.All(h=>h.End-h.Start is >= 6 and <= 16 && h.Start-h.BuildingStart==3), "Window/anticipation bounds failed.");
        var quieter = analyzer.Analyze(pcm.Select(s=>s*.25f).ToArray(),rate,"fixture");
        Check(first.Highlights.Select(h=>(h.Start,h.End)).SequenceEqual(quieter.Highlights.Select(h=>(h.Start,h.End))), "Gain alone changed highlight intervals.");
        var flat = Enumerable.Range(0,rate*60).Select(i=>(float)(.7*Math.Sin(2*Math.PI*220*i/rate))).ToArray();
        Check(analyzer.Analyze(flat,rate,"flat-loud").Highlights.Count==0, "Constant loud tone became a high window.");
        Check(analyzer.Analyze(new float[rate*60],rate,"silence").Highlights.Count==0, "Silence became a high window.");
        var ramp = Enumerable.Range(0,rate*75).Select(i=> {
            double t=i/(double)rate;
            double gain=t<20?.025:t<28?.025+.225*(t-20)/8:t<40?.25:.025;
            return (float)(gain*Math.Sin(2*Math.PI*220*t));
        }).ToArray();
        var built=analyzer.Analyze(ramp,rate,"ramp");
        Check(built.Highlights.Count==1 && built.Highlights[0].Start is >= 27 and <= 29,
            "Gradual ramp was mistaken for the arrival before its plateau.");
        Throws(()=>analyzer.Analyze([float.NaN],rate,"invalid"));
        try { analyzer.Analyze(pcm,rate,"cancel",new CancellationToken(true)); throw new Exception("Cancellation ignored."); }
        catch (OperationCanceledException) { }
        Throws(()=>new TrackArc("bad",30,[],[new("same",7,10,20,21,"test",1),new("same",22,23,29,30,"test",1)]));
    }

    public static void ReplayDeterminism()
    {
        string Run()
        {
            var d=new GameMomentDirector(Timeline());
            return JsonSerializer.Serialize(Enumerable.Range(0,901).Select(i=>d.Step(Clock(i*.05),Player(i*.05,i%100<80))).ToArray());
        }
        Check(Run()==Run(),"Identical shadow replay produced different states.");
    }

    private static float[] Tone(int tick,double gain) => Enumerable.Range(0,400)
        .Select(i=>(float)(gain*Math.Sin(2*Math.PI*220*(tick*400+i)/8000d))).ToArray();
}
