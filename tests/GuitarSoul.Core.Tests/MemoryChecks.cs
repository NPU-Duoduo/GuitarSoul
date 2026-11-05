using GuitarSoul.Core.Memories;
using System.Text.Json;

internal static class MemoryChecks
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.FromHours(8));
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Fixture(Action<string> run)
    {
        var folder = Path.Combine(Path.GetTempPath(), "GuitarSoul-memory-tests-" + Guid.NewGuid().ToString("N"));
        try { run(folder); }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    public static void Persistence() => Fixture(folder =>
    {
        var path = Path.Combine(folder, "memories.json"); var store = new MemoryStore(path);
        Check(!Directory.Exists(folder), "Load should never create persistent files");
        var memory = store.Add(" September ", "熟悉的旋律", MemorySource.UserNote, null, Now);
        var session = Guid.NewGuid(); var link = store.Confirm(memory.Id, session, Now.AddMinutes(1));
        Check(store.Confirm(memory.Id, session, Now.AddMinutes(2)) == link, "Repeated confirmation should not inflate visits");
        store.Edit(memory.Id, "September / acoustic", "第二次备注", Now.AddMinutes(3));
        var reopened = new MemoryStore(path); var actual = reopened.Find(memory.Id);
        Check(actual.Id == memory.Id && actual.Title == "September / acoustic" && actual.Notes == "第二次备注" && actual.CreatedAt == Now &&
            actual.UpdatedAt == Now.AddMinutes(3) && actual.Source == MemorySource.UserNote && actual.AudioPath is null, "Memory content/provenance changed across reopen");
        Check(reopened.Links.Single() == link && !Directory.EnumerateFiles(folder, "*.tmp").Any(), "Links or atomic-write cleanup failed");
    });
    public static void Delete() => Fixture(folder =>
    {
        Directory.CreateDirectory(folder); var audio = Path.Combine(folder, "riff.wav"); byte[] bytes = [1, 4, 9, 16]; File.WriteAllBytes(audio, bytes);
        var store = new MemoryStore(Path.Combine(folder, "memories.json"));
        var memory = store.Add("旧片段", null, MemorySource.SavedRiff, audio, Now);
        store.Confirm(memory.Id, Guid.NewGuid(), Now); store.Delete(memory.Id);
        var reopened = new MemoryStore(store.Path);
        Check(reopened.Memories.Count == 0 && reopened.Links.Count == 0 && File.ReadAllBytes(audio).SequenceEqual(bytes), "Deleting a record altered attached audio or left links");
    });
    public static void CatRiffPersistence() => Fixture(folder =>
    {
        Directory.CreateDirectory(folder);
        var audio = Path.Combine(folder, "Riff-Cat-20261014-120000.wav");
        byte[] bytes = [0x52, 0x49, 0x46, 0x46, 0x01, 0x02, 0x03, 0x04];
        File.WriteAllBytes(audio, bytes);
        var path = Path.Combine(folder, "memories.json");
        var store = new MemoryStore(path);
        var memory = store.Add("这段让我摇起来", "刚才一串接一串的拨弦，我想留着再听。",
            MemorySource.CatRiff, audio, Now);
        var reopened = new MemoryStore(path);
        var actual = reopened.Find(memory.Id);
        Check(actual.Source == MemorySource.CatRiff && actual.Title == memory.Title && actual.Notes == memory.Notes &&
            actual.AudioPath == Path.GetFullPath(audio) && actual.CreatedAt == Now && actual.UpdatedAt == Now,
            "Cat-selected Riff provenance or attachment changed across reopen");
        Check(reopened.Links.Count == 0 && File.ReadAllBytes(actual.AudioPath!).SequenceEqual(bytes),
            "Cat-selected Riff implied a user-confirmed association or changed audio");
        var before = File.ReadAllText(path);
        var rejected = false;
        try { store.Add("缺少声音", null, MemorySource.CatRiff, Path.Combine(folder, "missing.wav"), Now); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected && File.ReadAllText(path) == before, "Cat-selected Riff accepted a missing audio attachment");
    });
    public static void CatRiffRollbackCompatibility() => Fixture(folder =>
    {
        Directory.CreateDirectory(folder);
        var audio = Path.Combine(folder, "Riff-Cat-20261014-120000.wav");
        File.WriteAllBytes(audio, [0x52, 0x49, 0x46, 0x46]);
        var path = Path.Combine(folder, "memories.json");
        var saved = new MemoryStore(path).Add("猫想留下的片段", "一小段连续的拨弦", MemorySource.SavedRiff, audio, Now);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        Check(document.RootElement.GetProperty("Memories")[0].GetProperty("Source").GetInt32() == 1,
            "Cat-selected Riff must use the existing SavedRiff wire value for rollback");
        var reopened = new MemoryStore(path).Find(saved.Id);
        Check(reopened.Source == MemorySource.SavedRiff && reopened.AudioPath == Path.GetFullPath(audio) &&
            reopened.Title == saved.Title && reopened.Notes == saved.Notes,
            "Cat-selected Riff lost its existing-format attachment after reopen");
    });
    public static void InvalidFiles() => Fixture(folder =>
    {
        Directory.CreateDirectory(folder); var path = Path.Combine(folder, "memories.json");
        foreach (var content in new[] { "{broken", "{\"SchemaVersion\":99,\"Memories\":[],\"Links\":[]}", "{\"SchemaVersion\":1,\"Memories\":null,\"Links\":[]}" })
        {
            File.WriteAllText(path, content); bool rejected = false;
            try { _ = new MemoryStore(path); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && File.ReadAllText(path) == content, "Unreadable memory must stay untouched");
        }
    });
    public static void InvalidEdits() => Fixture(folder =>
    {
        var store = new MemoryStore(Path.Combine(folder, "memories.json")); var memory = store.Add("valid", "notes", MemorySource.UserNote, null, Now);
        var before = File.ReadAllText(store.Path);
        foreach (var title in new[] { " ", new string('a', 81), "line\nbreak" })
        {
            bool rejected = false;
            try { store.Edit(memory.Id, title, "wrong", Now); } catch (ArgumentException) { rejected = true; }
            Check(rejected && File.ReadAllText(store.Path) == before && store.Find(memory.Id).Notes == "notes", "Invalid edit changed state");
        }
        var absent = false;
        try { store.Confirm(Guid.NewGuid(), Guid.NewGuid(), Now); } catch (KeyNotFoundException) { absent = true; }
        Check(absent && store.Links.Count == 0, "Unproven memory identity was linked");
    });
    private static SharedMemory Old => new(Guid.Parse("7333718e-c725-44c6-bc62-c7f8f2fe2162"), "September", "", Now.AddDays(-1), Now.AddDays(-1), MemorySource.UserNote, null);
    public static void Invitation()
    {
        VerifyInvitation();
    }
    public static void ConcurrentWrites() => Fixture(folder =>
    {
        var path = Path.Combine(folder, "memories.json");
        var first = new MemoryStore(path); var second = new MemoryStore(path);
        var memory = first.Add("first", "", MemorySource.UserNote, null, Now);
        var saved = File.ReadAllText(path); bool rejected = false;
        try { second.Add("stale", "", MemorySource.UserNote, null, Now); } catch (IOException) { rejected = true; }
        Check(rejected && second.Memories.Count == 0 && File.ReadAllText(path) == saved, "Stale writer overwrote another session");
        File.WriteAllText(path, "{damaged-after-load"); rejected = false;
        try { first.Edit(memory.Id, "overwrite", "", Now); } catch (IOException) { rejected = true; }
        Check(rejected && File.ReadAllText(path) == "{damaged-after-load" && first.Find(memory.Id).Title == "first", "Post-load damage was overwritten");
    });
    private static void VerifyInvitation()
    {
        var policy = new MemoryInvitationPolicy(Now);
        for (int i = 0; i < 120; i++) Check(policy.Step([Old], 1, true, false, false, false, true, false) is null, "Disabled invitations leaked");
        Check(policy.Step([Old], 1, true, false, true, false, true, false) == Old.Id, "Opted-in quiet moment did not invite a known memory");
        for (int i = 0; i < 30; i++) policy.Step([Old], 1, true, false, true, false, true, false);
        Check(policy.PendingMemoryId is null, "Ignored invitation did not expire");
        for (int i = 0; i < 300; i++) Check(policy.Step([Old], 1, true, false, true, false, true, false) is null, "Ignored invitation nagged again");
        var continuous = new MemoryInvitationPolicy(Now);
        for (int i = 0; i < 300; i++) Check(continuous.Step([Old], 1, true, true, true, false, true, false) is null, "Invitation interrupted playing");
        for (int i = 0; i < 19; i++) Check(continuous.Step([Old], 1, true, false, true, false, true, false) is null, "Invitation did not wait for a real pause");
        Check(continuous.Step([Old], 1, true, false, true, false, true, false) == Old.Id, "Quiet period lost invitation");
    }
    public static void Interruption()
    {
        foreach (var block in new[] { "quiet", "hidden", "playback", "active", "disabled", "stopped" })
        {
            var policy = new MemoryInvitationPolicy(Now);
            for (int i = 0; i < 100; i++) policy.Step([Old], 1, true, false, true, false, true, false);
            Check(policy.PendingMemoryId == Old.Id, "Test fixture did not show an invitation");
            var pending = policy.Step([Old], 1, block != "stopped", block == "active", block != "disabled", block == "quiet", block != "hidden", block == "playback");
            Check(pending is null, "Invitation survived " + block);
            for (int i = 0; i < 120; i++) Check(policy.Step([Old], 1, true, false, true, false, true, false) is null, "Interrupted invitation resurfaced");
        }
    }
    public static void Evidence() => Fixture(folder =>
    {
        var store = new MemoryStore(Path.Combine(folder, "memories.json")); var fresh = store.Add("September", null, MemorySource.UserNote, null, Now.AddSeconds(1));
        var policy = new MemoryInvitationPolicy(Now);
        for (int i = 0; i < 120; i++) Check(policy.Step(store.Memories, 1, true, false, true, false, true, false) is null, "New memory was immediately requested again");
        var later = new MemoryInvitationPolicy(Now.AddDays(1));
        for (int i = 0; i < 100; i++) later.Step(store.Memories, 1, true, false, true, false, true, false);
        Check(later.PendingMemoryId == fresh.Id && store.Links.Count == 0, "Invitation falsely confirmed a song association");
        store.Delete(fresh.Id);
        Check(later.Step(store.Memories, 1, true, false, true, false, true, false) is null, "Deleted memory remained in invitation");
    });
}
