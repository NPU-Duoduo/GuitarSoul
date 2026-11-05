using GuitarSoul.Core.Memories;

internal static class StageCollectionChecks
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.FromHours(8));
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name);
    }
    private static void Fixture(Action<string> run)
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var folder = Path.GetFullPath(Path.Combine(root, "GuitarSoul-stage-" + Guid.NewGuid().ToString("N")));
        if (!folder.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture must stay under the temporary directory.");
        try { run(Path.Combine(folder, "collection.json")); }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    public static void SourcesAndPersistence() => Fixture(path =>
    {
        var store = new StageCollection(path);
        Check(!File.Exists(path) && store.Selected == StageStyle.Everyday, "Opening must not write or change the everyday cat.");
        var listen = store.Record(StageStyle.HardRock, ExplorationSource.Listened, "一起听了一首歌", Now);
        Check(!store.IsUnlocked(StageStyle.HardRock), "Playback must not award playing achievements.");
        Reject<InvalidOperationException>(() => store.Select(StageStyle.HardRock));
        var played = store.Record(StageStyle.Mellow, ExplorationSource.Played, " 慢速分解和弦 ", Now.AddMinutes(1));
        store.Select(StageStyle.Mellow);
        var reopened = new StageCollection(path);
        Check(reopened.Selected == StageStyle.Mellow && reopened.IsUnlocked(StageStyle.Mellow) && !reopened.IsUnlocked(StageStyle.HardRock),
            "Mellow reward and chosen appearance must survive reopen without upgrading listening to playing.");
        Check(reopened.Entries.Single(x => x.Id == played.Id).Title == "慢速分解和弦" &&
            reopened.Entries.Single(x => x.Id == listen.Id).Source == ExplorationSource.Listened, "Provenance or title lost.");
        reopened.Record(StageStyle.Psychedelic, ExplorationSource.Played, "更正曲风", Now.AddMinutes(2), listen.Id);
        var edited = new StageCollection(path).Entries.Single(x => x.Id == listen.Id);
        Check(edited.Style == StageStyle.Psychedelic && edited.CreatedAt == Now && edited.Source == ExplorationSource.Played,
            "Correction must replace the same experience and preserve its date.");
        reopened.Delete(played.Id);
        var afterDelete = new StageCollection(path);
        Check(afterDelete.Entries.All(x => x.Id != played.Id) && afterDelete.IsUnlocked(StageStyle.Mellow) && afterDelete.Selected == StageStyle.Mellow,
            "Deleting a note should remove its text without revoking an earned appearance.");
        afterDelete.Select(StageStyle.Everyday);
        Check(new StageCollection(path).Selected == StageStyle.Everyday, "Everyday appearance must remain selectable.");
    });
    public static void FailuresPreserveCollection() => Fixture(path =>
    {
        var store = new StageCollection(path);
        store.Record(StageStyle.HardRock, ExplorationSource.Played, "第一次尝试", Now);
        var original = File.ReadAllText(path);
        Reject<ArgumentException>(() => store.Record(StageStyle.Mellow, ExplorationSource.Played, "", Now));
        Reject<ArgumentException>(() => store.Record(StageStyle.Everyday, ExplorationSource.Played, "invalid", Now));
        Reject<ArgumentException>(() => store.Record(StageStyle.Mellow, (ExplorationSource)99, "invalid", Now));
        Check(File.ReadAllText(path) == original && !store.IsUnlocked(StageStyle.Mellow), "Rejected edits changed saved state.");
        var other = new StageCollection(path);
        other.Record(StageStyle.Psychedelic, ExplorationSource.Played, "另一个窗口", Now);
        var latest = File.ReadAllText(path);
        Reject<IOException>(() => store.Record(StageStyle.Mellow, ExplorationSource.Played, "stale", Now));
        Check(File.ReadAllText(path) == latest && !store.IsUnlocked(StageStyle.Mellow), "Stale writer overwrote a newer collection or mutated memory.");
        foreach (var broken in new[] { "{broken", "{\"SchemaVersion\":9}", "{\"SchemaVersion\":1,\"Selected\":3,\"Unlocked\":[],\"Entries\":[]}",
                     "{\"SchemaVersion\":1,\"Selected\":0,\"Unlocked\":[],\"Entries\":[null]}" })
        {
            File.WriteAllText(path, broken);
            Reject<InvalidDataException>(() => _ = new StageCollection(path));
            Check(File.ReadAllText(path) == broken, "Malformed file was overwritten.");
        }
    });
}
