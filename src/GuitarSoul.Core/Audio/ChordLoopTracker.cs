namespace GuitarSoul.Core.Audio;

/// <summary>Exact chord-order loops, not bars, rhythm, song identity or score following.</summary>
public sealed class ChordLoopTracker
{
    private ChordSegment[] _pattern = [];
    private (int Root, ChordQuality Quality, double Start)? _last;
    private int _misses;
    public IReadOnlyList<ChordSegment> Pattern => _pattern;
    public int ActiveIndex { get; private set; } = -1;
    public bool IsConfirmed => _pattern.Length > 0;

    public void Clear() { _pattern = []; _last = null; _misses = 0; ActiveIndex = -1; }

    public void Update(IReadOnlyList<ChordSegment> history, ChordSegment? current)
    {
        var notes = history.Where(x => x.Duration >= ChordTimeline.MinimumSegmentSeconds).ToList();
        if (current is ChordSegment chord && chord.Duration >= ChordTimeline.MinimumSegmentSeconds) notes.Add(chord);
        if (notes.Count == 0) { Clear(); return; }
        var end = notes[^1];
        var token = (end.Root, end.Quality, end.StartSeconds);
        bool changed = _last != token;
        _last = token;
        if (_pattern.Length > 0)
        {
            int best = 0, index = -1; bool tied = false;
            for (int slot = 0; slot < _pattern.Length; slot++)
            {
                int matched = 0;
                while (matched < Math.Min(notes.Count, _pattern.Length * 2) &&
                    Same(notes[^(matched + 1)], _pattern[(slot - matched % _pattern.Length + _pattern.Length) % _pattern.Length])) matched++;
                if (matched > best) { best = matched; index = slot; tied = false; }
                else if (matched == best && matched > 0) tied = true;
            }
            ActiveIndex = best >= 2 && !tied ? index : -1;
            if (changed) _misses = ActiveIndex >= 0 ? 0 : _misses + 1;
            if (_misses < 3) return;
            _pattern = []; ActiveIndex = -1; _misses = 0;
        }
        // Find the shortest exact period in the recent suffix, requiring two complete passes.
        for (int length = 2; length <= Math.Min(8, notes.Count / 2); length++)
        {
            int start = notes.Count - length * 2;
            bool repeat = true;
            for (int i = 0; i < length; i++) repeat &= Same(notes[start + i], notes[start + length + i]);
            if (!repeat || notes.Skip(start).Take(length).Select(x => x.Root).Distinct().Count() < 2) continue;
            _pattern = notes.Skip(start).Take(length).ToArray();
            ActiveIndex = length - 1; return;
        }
    }

    private static bool Same(ChordSegment a, ChordSegment b) => a.Root == b.Root && a.Quality == b.Quality;
}

public sealed class ChordIdleReset
{
    private double? _lastSound;
    public static int ValidSeconds(int seconds) => seconds == 0 ? 0 : Math.Clamp(seconds, 5, 300);
    public void Restart() => _lastSound = null;
    public bool Advance(double now, bool soundActive, bool hasRecording, int resetSeconds)
    {
        if (!double.IsFinite(now)) return false;
        bool expired = hasRecording && resetSeconds != 0 && _lastSound is double last &&
            now - last >= ValidSeconds(resetSeconds);
        if (soundActive) _lastSound = now;
        if (!hasRecording) return false;
        _lastSound ??= now;
        if (!expired && (resetSeconds == 0 || now - _lastSound.Value < ValidSeconds(resetSeconds))) return false;
        _lastSound = soundActive ? now : null;
        return true;
    }
}
