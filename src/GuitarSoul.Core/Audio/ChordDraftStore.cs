using System.Text.Json;

namespace GuitarSoul.Core.Audio;

/// <summary>One local symbolic draft. Never contains captured samples or starts playback.</summary>
public sealed class ChordDraftStore(string path)
{
    private sealed record Document(int Schema, DateTimeOffset SavedAt, ChordSegment[] Segments);
    public bool TryLoad(out ChordSegment[] segments, out string? error)
    {
        segments = []; error = null;
        try
        {
            if (!File.Exists(path)) return true;
            var document = JsonSerializer.Deserialize<Document>(File.ReadAllText(path));
            if (document is null || document.Schema != 1 || document.Segments is null || document.Segments.Length > ChordTimeline.Capacity ||
                document.Segments.Any(s => s.Root is < 0 or > 11 || !Enum.IsDefined(s.Quality) ||
                    !double.IsFinite(s.StartSeconds) || !double.IsFinite(s.EndSeconds) || s.StartSeconds < 0 || s.Duration < ChordTimeline.MinimumSegmentSeconds))
                throw new InvalidDataException("和弦草稿格式无效。");
            segments = document.Segments; return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { error = exception.Message; return false; }
    }

    public void Save(IReadOnlyList<ChordSegment> segments)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Document(1, DateTimeOffset.UtcNow, segments.TakeLast(ChordTimeline.Capacity).ToArray())));
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public void Clear() { if (File.Exists(path)) File.Delete(path); }
}
