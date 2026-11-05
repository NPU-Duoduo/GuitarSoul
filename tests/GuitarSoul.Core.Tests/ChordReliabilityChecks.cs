using GuitarSoul.Core.Audio;

internal static class ChordReliabilityChecks
{
    private static void Check(bool result, string message) { if (!result) throw new InvalidOperationException(message); }
    public static void RewindAndRetention()
    {
        var timeline = new ChordTimeline();
        var c = new HarmonyFrame(0, ChordQuality.Major, .9, 0);
        timeline.Observe(c, 9); timeline.Observe(c, 10); timeline.Close();
        timeline.Observe(c, 0); timeline.Observe(c, .6); timeline.Close();
        Check(timeline.Count == 2 && timeline.Segments.All(s => s.Duration >= .35), "A new capture clock erased the preceding chord");
        timeline.Observe(c, double.NaN); timeline.Observe(c, double.PositiveInfinity);
        Check(timeline.Current is null, "Invalid timestamps opened a draft");
        timeline.Clear();
        for (int i = 0; i < ChordTimeline.Capacity + 2; i++)
        {
            var chord = new HarmonyFrame(i % 12, ChordQuality.Major, .9, 0);
            timeline.Observe(chord, i); timeline.Observe(chord, i + .6);
        }
        timeline.Close();
        Check(timeline.Count == 512 && timeline.TruncatedCount == 2 && timeline.Segments[0].StartSeconds == 2,
            "Long recording retention was unbounded or silently lost its truncation count");
        long revision = timeline.Revision;
        timeline.Restore(timeline.Segments.Select(s => s with { Root = (s.Root + 2) % 12 }).ToArray());
        Check(timeline.Count == 512 && timeline.Revision > revision,
            "Replacing a full timeline did not invalidate consumers of the previous contents");
    }

    public static void DraftRoundTripAndCorruption()
    {
        string folder = Path.Combine(Path.GetTempPath(), "GuitarSoulDraft-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "draft.json"); var store = new ChordDraftStore(path);
            Check(store.TryLoad(out var empty, out _) && empty.Length == 0, "First use required an existing draft");
            ChordSegment[] original = [new(0, ChordQuality.Major, 0, 1), new(9, ChordQuality.Minor, 1, 2)];
            store.Save(original);
            Check(store.TryLoad(out var read, out _) && read.SequenceEqual(original), "Symbolic draft did not round-trip");
            Check(Directory.GetFiles(folder).Length == 1, "Atomic save left temporary files");
            const string corrupt = "{broken"; File.WriteAllText(path, corrupt);
            Check(!store.TryLoad(out _, out var error) && error is not null && File.ReadAllText(path) == corrupt,
                "Loading a damaged draft erased or rewrote its evidence");
            File.WriteAllText(path, "{\"Schema\":1,\"Segments\":[{\"Root\":99,\"Quality\":1,\"StartSeconds\":0,\"EndSeconds\":1}]}");
            Check(!store.TryLoad(out _, out _), "Invalid chord identity passed draft validation");
            store.Clear(); Check(!File.Exists(path), "Explicit clear did not remove the checkpoint");
        }
        finally { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
}
