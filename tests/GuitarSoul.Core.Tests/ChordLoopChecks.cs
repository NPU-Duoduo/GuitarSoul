using GuitarSoul.Core.Audio;

internal static class ChordLoopChecks
{
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    private static ChordSegment Note(int root, int index, ChordQuality quality = ChordQuality.Major) => new(root, quality, index, index + .8);

    public static void ConfirmationAndFollowing()
    {
        var loop = new ChordLoopTracker();
        var notes = new List<ChordSegment>();
        int[] cycle = [0, 7, 9, 5];
        for (int i = 0; i < 7; i++) notes.Add(Note(cycle[i % 4], i));
        loop.Update(notes, null);
        Check(!loop.IsConfirmed, "Incomplete second pass became a loop");
        notes.Add(Note(5, 7)); loop.Update(notes, null);
        Check(loop.Pattern.Select(x => x.Root).SequenceEqual(cycle) && loop.ActiveIndex == 3, "Two full passes did not confirm the ordered pattern");
        for (int i = 8; i < 20; i++)
        {
            notes.Add(Note(cycle[i % 4], i)); loop.Update(notes, null);
            Check(loop.Pattern.Count == 4 && loop.ActiveIndex == i % 4, "Repeated passes grew the display or highlighted the wrong slot");
        }
        loop.Clear();
        Check(!loop.IsConfirmed && loop.ActiveIndex == -1, "Clear retained loop state");
    }

    public static void RepeatedChordsAndRejection()
    {
        var loop = new ChordLoopTracker();
        int[] roots = [0, 7, 0, 5, 0, 7, 0, 5, 0, 7, 0];
        var notes = roots.Select((root, i) => Note(root, i)).ToList();
        loop.Update(notes, null);
        Check(loop.Pattern.Count == 4 && loop.Pattern[loop.ActiveIndex].Root == 0,
            "Repeated root positions did not match by preceding context");
        notes.Add(Note(2, 11)); loop.Update(notes, null);
        Check(loop.IsConfirmed && loop.ActiveIndex == -1, "One off-pattern chord either guessed a slot or discarded the whole loop");
        loop.Update(notes, null); loop.Update(notes, null);
        Check(loop.IsConfirmed, "UI refreshes counted as additional deviations");
        notes.Add(Note(4, 12)); loop.Update(notes, null);
        notes.Add(Note(11, 13)); loop.Update(notes, null);
        Check(!loop.IsConfirmed, "Repeated deviations did not return to draft mode");
        loop.Update(Enumerable.Range(0, 12).Select(i => Note(0, i, i % 2 == 0 ? ChordQuality.Major : ChordQuality.Unknown)).ToArray(), null);
        Check(!loop.IsConfirmed, "Quality flicker on one root became a musical loop");
        loop.Update([Note(0, 0), Note(7, 1), Note(0, 2)], new ChordSegment(7, ChordQuality.Major, 3, 3.1));
        Check(!loop.IsConfirmed, "A short uncommitted current chord completed a loop");
    }

    public static void IdleAndRestore()
    {
        var idle = new ChordIdleReset();
        Check(!idle.Advance(0, true, true, 15), "First sound cleared a recording");
        Check(!idle.Advance(14.9, false, true, 15) && idle.Advance(15, false, true, 15), "Idle deadline did not reset at 15 seconds");
        Check(!idle.Advance(16, false, false, 15), "Empty recording reset repeatedly");
        idle.Restart(); idle.Advance(20, true, true, 5);
        Check(idle.Advance(26, true, true, 5), "A new sound after a long gap merged two songs when no quiet callback arrived");
        idle.Restart();
        for (int i = 0; i < 100; i++) Check(!idle.Advance(100 + i, true, true, 5), "Active unrecognized sound was treated as silence");
        Check(!idle.Advance(203.9, false, true, 5) && idle.Advance(204, false, true, 5), "Silence did not count from the last actual sound");
        idle.Restart();
        Check(!idle.Advance(1000, false, true, 0) && !idle.Advance(9999, false, true, 0), "Disabled idle reset cleared a recording");
        idle.Restart();
        Check(!idle.Advance(10000, false, true, 5), "Restoring or editing settings immediately discarded the draft");
        var timeline = new ChordTimeline();
        timeline.Restore([Note(0, 0), Note(7, 1)]);
        Check(timeline.Count == 2 && timeline.DescribeAbsolute() == "C G" && timeline.Current is null, "Restoring changed the retained chords");
        timeline.Observe(new HarmonyFrame(9, ChordQuality.Minor, .9, 0), 3);
        timeline.Observe(new HarmonyFrame(9, ChordQuality.Minor, .9, 0), 4); timeline.Close();
        Check(timeline.DescribeAbsolute() == "C G Am", "Recording could not continue after restore");
    }
}
