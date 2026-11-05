using System.Text.Json;

namespace GuitarSoul.Core.Memories;

public enum MemorySource { UserNote, SavedRiff, CatRiff }
public sealed record SharedMemory(Guid Id, string Title, string Notes, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, MemorySource Source, string? AudioPath);
public sealed record SessionMemoryLink(Guid Id, Guid MemoryId, Guid SessionId, DateTimeOffset ConfirmedAt);
public sealed record MemoryDocument(int SchemaVersion, SharedMemory[] Memories, SessionMemoryLink[] Links);

/// <summary>User memories and opt-in audience selections. Loading never creates files.
/// Deleting a memory removes its links, never its separately saved audio.</summary>
public sealed class MemoryStore
{
    public string Path { get; }
    private MemoryDocument _document = new(1, [], []);
    private string? _lastSavedText;
    public IReadOnlyList<SharedMemory> Memories => Array.AsReadOnly(_document.Memories);
    public IReadOnlyList<SessionMemoryLink> Links => Array.AsReadOnly(_document.Links);

    public MemoryStore(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(Path)) return;
        try
        {
            _lastSavedText = File.ReadAllText(Path);
            var document = JsonSerializer.Deserialize<MemoryDocument>(_lastSavedText)
                ?? throw new InvalidDataException("记忆文件没有有效内容。");
            Validate(document);
            _document = document;
        }
        catch (JsonException exception) { throw new InvalidDataException("记忆文件无法读取；原文件已保留。", exception); }
    }

    public SharedMemory Add(string title, string? notes, MemorySource source, string? audioPath, DateTimeOffset now)
    {
        title = CleanTitle(title);
        notes = CleanNotes(notes);
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (audioPath is not null)
        {
            audioPath = System.IO.Path.GetFullPath(audioPath);
            if (source is not (MemorySource.SavedRiff or MemorySource.CatRiff) || !File.Exists(audioPath))
                throw new ArgumentException("请选择已保存的 Riff 文件作为附件。");
        }
        var memory = new SharedMemory(Guid.NewGuid(), title, notes, now, now, source, audioPath);
        Commit(_document with { Memories = [.. _document.Memories, memory] });
        return memory;
    }

    public void Edit(Guid id, string title, string? notes, DateTimeOffset now)
    {
        title = CleanTitle(title); var cleanNotes = CleanNotes(notes);
        var existing = Find(id);
        Commit(_document with { Memories = _document.Memories.Select(m => m.Id == id
            ? existing with { Title = title, Notes = cleanNotes, UpdatedAt = now } : m).ToArray() });
    }

    public SessionMemoryLink Confirm(Guid id, Guid sessionId, DateTimeOffset now)
    {
        Find(id);
        if (sessionId == Guid.Empty) throw new ArgumentException("会话身份不能为空。", nameof(sessionId));
        var previous = _document.Links.FirstOrDefault(link => link.MemoryId == id && link.SessionId == sessionId);
        if (previous is not null) return previous;
        var link = new SessionMemoryLink(Guid.NewGuid(), id, sessionId, now);
        Commit(_document with { Links = [.. _document.Links, link] });
        return link;
    }

    public void Delete(Guid id)
    {
        Find(id);
        Commit(_document with { Memories = _document.Memories.Where(m => m.Id != id).ToArray(),
            Links = _document.Links.Where(link => link.MemoryId != id).ToArray() });
    }

    public SharedMemory Find(Guid id) => _document.Memories.FirstOrDefault(m => m.Id == id)
        ?? throw new KeyNotFoundException("这条记忆已不存在，请刷新列表。");

    private void Commit(MemoryDocument next)
    {
        Validate(next);
        // Preserve failed writes in memory too: commit the state only after the atomic file replacement.
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using var writeLock = new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var currentText = File.Exists(Path) ? File.ReadAllText(Path) : null;
        if (currentText != _lastSavedText)
            throw new IOException("记忆文件已被其他会话修改，请重新打开程序后再保存；已有内容未覆盖。");
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var serialized = JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporary, serialized);
            File.Move(temporary, Path, true);
            _document = next;
            _lastSavedText = serialized;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string CleanTitle(string? title)
    {
        title = title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80 || title.Any(char.IsControl))
            throw new ArgumentException("请给记忆取一个 1–80 字的名字。");
        return title;
    }
    private static string CleanNotes(string? notes)
    {
        notes = notes?.Trim() ?? "";
        if (notes.Length > 1000) throw new ArgumentException("备注请保持在 1000 字以内。");
        return notes;
    }
    private static void Validate(MemoryDocument document)
    {
        if (document.SchemaVersion != 1) throw new InvalidDataException("记忆格式版本不受支持；原文件已保留。");
        if (document.Memories is null || document.Links is null || document.Memories.Any(m => m is null) ||
            document.Links.Any(link => link is null)) throw new InvalidDataException("记忆内容不完整；原文件已保留。");
        var ids = document.Memories.Select(m => m.Id).ToHashSet();
        if (ids.Contains(Guid.Empty) || ids.Count != document.Memories.Length)
            throw new InvalidDataException("记忆身份重复或无效；原文件已保留。");
        foreach (var memory in document.Memories)
        {
            try { CleanTitle(memory.Title); CleanNotes(memory.Notes); }
            catch (ArgumentException e) { throw new InvalidDataException("记忆名称或备注无效；原文件已保留。", e); }
            if (!Enum.IsDefined(memory.Source) || memory.AudioPath is not null &&
                (memory.Source is not (MemorySource.SavedRiff or MemorySource.CatRiff) || !System.IO.Path.IsPathFullyQualified(memory.AudioPath)))
                throw new InvalidDataException("记忆来源无效；原文件已保留。");
        }
        if (document.Links.Any(link => link.Id == Guid.Empty || link.SessionId == Guid.Empty || !ids.Contains(link.MemoryId)) ||
            document.Links.Select(link => link.Id).Distinct().Count() != document.Links.Length ||
            document.Links.Select(link => (link.MemoryId, link.SessionId)).Distinct().Count() != document.Links.Length)
            throw new InvalidDataException("记忆关联无效；原文件已保留。");
    }
}
