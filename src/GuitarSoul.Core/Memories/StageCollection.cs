using System.Text.Json;

namespace GuitarSoul.Core.Memories;

// Values are persisted; append new artists without renumbering existing collections.
public enum StageStyle { Everyday = 0, HardRock = 1, Psychedelic = 2, Mellow = 3,
    JimmyPage = 4, DavidGilmour = 5, BBKing = 6, AngusYoung = 7, KurtCobain = 8, NileRodgers = 9 }
public enum ExplorationSource { Played, Listened }
public sealed record StageExploration(Guid Id, StageStyle Style, ExplorationSource Source, string Title, DateTimeOffset CreatedAt);
public sealed record StageDocument(int SchemaVersion, StageStyle Selected, StageStyle[] Unlocked, StageExploration[] Entries);

/// <summary>Explicit, user-confirmed exploration. No audio or automatic genre inference.</summary>
public sealed class StageCollection
{
    private StageDocument _document = new(1, StageStyle.Everyday, [], []);
    private string? _savedText;
    public string Path { get; }
    public StageStyle Selected => _document.Selected;
    public IReadOnlyList<StageExploration> Entries => Array.AsReadOnly(_document.Entries);
    public bool IsUnlocked(StageStyle style) => style == StageStyle.Everyday || _document.Unlocked.Contains(style);

    public StageCollection(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(Path)) return;
        try
        {
            _savedText = File.ReadAllText(Path);
            var document = JsonSerializer.Deserialize<StageDocument>(_savedText)
                ?? throw new InvalidDataException("舞台收藏没有有效内容；原文件已保留。");
            Validate(document);
            _document = document;
        }
        catch (JsonException e) { throw new InvalidDataException("舞台收藏无法读取；原文件已保留。", e); }
    }

    public StageExploration Record(StageStyle style, ExplorationSource source, string title, DateTimeOffset now, Guid? editId = null)
    {
        if (style == StageStyle.Everyday || !Enum.IsDefined(style) || !Enum.IsDefined(source))
            throw new ArgumentException("请选择一种舞台风格和经历来源。");
        title = CleanTitle(title);
        var previous = editId.HasValue ? Find(editId.Value) : null;
        var entry = new StageExploration(previous?.Id ?? Guid.NewGuid(), style, source, title, previous?.CreatedAt ?? now);
        var unlocked = source == ExplorationSource.Played
            ? _document.Unlocked.Append(style).Distinct().ToArray() : _document.Unlocked;
        Commit(_document with
        {
            Unlocked = unlocked,
            Entries = previous is null ? [.. _document.Entries, entry]
                : _document.Entries.Select(x => x.Id == entry.Id ? entry : x).ToArray()
        });
        return entry;
    }

    public void Select(StageStyle style)
    {
        if (!Enum.IsDefined(style) || !IsUnlocked(style)) throw new InvalidOperationException("先确认一次亲自尝试，再使用这套舞台形态。");
        Commit(_document with { Selected = style });
    }

    public void Delete(Guid id)
    {
        Find(id);
        // A corrected/deleted note does not revoke a previously earned cosmetic.
        Commit(_document with { Entries = _document.Entries.Where(x => x.Id != id).ToArray() });
    }

    private StageExploration Find(Guid id) => _document.Entries.FirstOrDefault(x => x.Id == id)
        ?? throw new KeyNotFoundException("这条经历已不存在，请重新打开舞台收藏。");

    private void Commit(StageDocument next)
    {
        Validate(next);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using var fileLock = new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((File.Exists(Path) ? File.ReadAllText(Path) : null) != _savedText)
            throw new IOException("收藏已被另一个窗口修改，请重新打开程序；已有内容未覆盖。");
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var text = JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporary, text);
            File.Move(temporary, Path, true);
            _document = next;
            _savedText = text;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static string CleanTitle(string? title)
    {
        title = title?.Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 80 || title.Any(char.IsControl))
            throw new ArgumentException("请写下 1–80 字的曲名或练习描述。");
        return title;
    }

    private static void Validate(StageDocument document)
    {
        if (document.SchemaVersion != 1 || !Enum.IsDefined(document.Selected) ||
            document.Unlocked is null || document.Entries is null ||
            document.Unlocked.Any(x => x == StageStyle.Everyday || !Enum.IsDefined(x)) ||
            document.Unlocked.Distinct().Count() != document.Unlocked.Length ||
            document.Selected != StageStyle.Everyday && !document.Unlocked.Contains(document.Selected))
            throw new InvalidDataException("舞台收藏格式无效；原文件已保留。");
        var ids = new HashSet<Guid>();
        foreach (var entry in document.Entries)
        {
            if (entry is null || entry.Id == Guid.Empty || !ids.Add(entry.Id) || !Enum.IsDefined(entry.Style) ||
                entry.Style == StageStyle.Everyday || !Enum.IsDefined(entry.Source) ||
                entry.Source == ExplorationSource.Played && !document.Unlocked.Contains(entry.Style))
                throw new InvalidDataException("舞台经历内容无效；原文件已保留。");
            try { CleanTitle(entry.Title); }
            catch (ArgumentException e) { throw new InvalidDataException("舞台经历描述无效；原文件已保留。", e); }
        }
    }
}
