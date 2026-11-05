using System.Text.Json;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Memories;

public sealed record StageAchievement(StageStyle Style, DateTimeOffset UnlockedAt, int RuleVersion);
public sealed record AchievementDocument(int SchemaVersion, StageStyle Selected, StageAchievement[] Earned);

/// <summary>Only the detector can produce a trigger. Names are presentation, not genre or handedness recognition.</summary>
public sealed class StageTrigger
{
    public StageStyle Style { get; }
    public int RuleVersion => 2;
    internal StageTrigger(StageStyle style) => Style = style;
}

/// <summary>Bounded, sample-time evidence from live input; contains no audio, wall clock or UI state.</summary>
public sealed class StageAchievementDetector
{
    private readonly Queue<Observation> _recent = new();
    private double _lastTime = -1;
    private long _lastOnsets;
    private readonly record struct Observation(double Time, double Seconds, PlayingExpression Expression, long Onsets,
        bool SoundActive, DynamicContour Dynamics, double PulseConfidence);
    private enum SegmentKind { Uncertain, Silence, Sustained, Flowing, Animated }
    private readonly record struct Segment(SegmentKind Kind, double Seconds);
    private readonly record struct Phrase(SegmentKind Kind, double Seconds, double SilenceBefore, double OtherGapBefore);

    public void Reset() { _recent.Clear(); _lastTime = -1; _lastOnsets = 0; }

    public IReadOnlyList<StageTrigger> Observe(RhythmFrame frame, bool eligible, bool soundActive,
        Func<StageStyle, bool>? alreadyUnlocked = null,
        GuitarEvidenceDecision guitarEvidence = GuitarEvidenceDecision.Confirmed)
    {
        if (!eligible || guitarEvidence != GuitarEvidenceDecision.Confirmed ||
            !double.IsFinite(frame.AudioSeconds) || frame.AudioSeconds < 0 || frame.OnsetCount < 0)
        { Reset(); return []; }
        if (_lastTime < 0)
        { _lastTime = frame.AudioSeconds; _lastOnsets = frame.OnsetCount; return []; }
        double seconds = frame.AudioSeconds - _lastTime;
        if (seconds == 0) return []; // A repeated UI/callback snapshot cannot earn evidence.
        if (seconds < 0 || seconds > .6 || frame.OnsetCount < _lastOnsets)
        {
            Reset(); _lastTime = frame.AudioSeconds; _lastOnsets = frame.OnsetCount;
            return [];
        }
        long onsets = frame.OnsetCount - _lastOnsets;
        _lastTime = frame.AudioSeconds; _lastOnsets = frame.OnsetCount;
        bool confidentSound = soundActive && double.IsFinite(frame.Listening.Confidence) && frame.Listening.Confidence >= .65;
        var expression = confidentSound
            ? frame.Listening.Expression : PlayingExpression.None;
        // An attack can precede its stable expression label; retain its independent onset evidence.
        double pulseConfidence = Math.Max(
            double.IsFinite(frame.Confidence) ? frame.Confidence : 0,
            double.IsFinite(frame.BeatConfidence) ? frame.BeatConfidence : 0);
        _recent.Enqueue(new(frame.AudioSeconds, seconds, expression, confidentSound ? onsets : 0,
            soundActive, confidentSound ? frame.Listening.Dynamics : DynamicContour.Steady, pulseConfidence));
        while (_recent.TryPeek(out var old) && frame.AudioSeconds - old.Time >= 45) _recent.Dequeue();
        // Bound even implausibly tiny callback intervals.
        while (_recent.Count > 9000) _recent.Dequeue();
        // Expiring old evidence during silence or uncertainty must never reveal an achievement.
        if (expression == PlayingExpression.None) return [];
        var phrases = BuildPhrases();
        double sustained = phrases.Where(x => x.Kind == SegmentKind.Sustained).Sum(x => x.Seconds);
        double flowing = phrases.Where(x => x.Kind == SegmentKind.Flowing).Sum(x => x.Seconds);
        double animated = phrases.Where(x => x.Kind == SegmentKind.Animated).Sum(x => x.Seconds);
        long attacks = _recent.Sum(x => x.Onsets);
        double building = _recent.Where(x => x.Expression != PlayingExpression.None && x.Dynamics == DynamicContour.Building).Sum(x => x.Seconds);
        double easing = _recent.Where(x => x.Expression != PlayingExpression.None && x.Dynamics == DynamicContour.Easing).Sum(x => x.Seconds);
        double flowingPulse = _recent.Where(x => x.Expression == PlayingExpression.Flowing && x.PulseConfidence >= .6).Sum(x => x.Seconds);

        int mediumHeld = phrases.Count(x => x.Kind == SegmentKind.Sustained && x.Seconds is >= 1.5 and <= 4.2);
        int extendedHeld = phrases.Count(x => x.Kind == SegmentKind.Sustained && x.Seconds >= 4.5);
        int heldReplies = CountReplies(phrases, SegmentKind.Sustained, 1, 3.8, .45, 2.5);
        int denseReplies = CountReplies(phrases, SegmentKind.Animated, 1, 4.5, .3, 1.8);
        int leadResolves = CountPairs(phrases, SegmentKind.Animated, 1.4, SegmentKind.Sustained, 1.8, 1.8);
        int expressionChanges = CountExpressionChanges(phrases, 1.8);
        bool pageArc = HasArc(phrases,
            (x => x == SegmentKind.Animated, 2d),
            (x => x == SegmentKind.Flowing, 1.5),
            (x => x == SegmentKind.Sustained, 2.5));
        bool quietLoudQuiet = HasArc(phrases,
            (x => x is SegmentKind.Sustained or SegmentKind.Flowing, 2.5),
            (x => x == SegmentKind.Animated, 3d),
            (x => x is SegmentKind.Sustained or SegmentKind.Flowing, 2d));
        double longestFlow = phrases.Where(x => x.Kind == SegmentKind.Flowing).Select(x => x.Seconds).DefaultIfEmpty().Max();

        // Specific musical shapes are considered before broader ones. Only one unseen
        // encounter consumes the current evidence, so a single passage cannot cascade rewards.
        (StageStyle Style, bool Match)[] candidates =
        [
            (StageStyle.BBKing, heldReplies >= 3 && sustained >= 6 && animated < .7),
            (StageStyle.AngusYoung, denseReplies >= 3 && animated >= 6 && attacks >= 20),
            (StageStyle.KurtCobain, quietLoudQuiet && building >= .5 && easing >= .5 && attacks >= 14),
            (StageStyle.JimmyPage, pageArc && expressionChanges <= 3 && attacks >= 14),
            (StageStyle.DavidGilmour, extendedHeld >= 3 && sustained >= 13 && animated < .7),
            (StageStyle.NileRodgers, longestFlow >= 8 && flowingPulse >= 3 && attacks >= 16 && animated < .7),
            (StageStyle.Psychedelic, sustained >= 2 && flowing >= 3 && animated >= 2 && expressionChanges >= 4 && attacks >= 14),
            (StageStyle.HardRock, leadResolves >= 2 && sustained >= 4 && animated >= 3 && attacks >= 16),
            (StageStyle.Mellow, mediumHeld >= 3 && heldReplies == 0 && sustained >= 7 && animated < .7)
        ];
        foreach (var candidate in candidates)
        {
            if (!candidate.Match || alreadyUnlocked?.Invoke(candidate.Style) == true) continue;
            _recent.Clear();
            return [new(candidate.Style)];
        }
        return [];
    }

    private List<Phrase> BuildPhrases()
    {
        var segments = new List<Segment>();
        foreach (var item in _recent)
        {
            var kind = !item.SoundActive ? SegmentKind.Silence : item.Expression switch
            {
                PlayingExpression.Sustained => SegmentKind.Sustained,
                PlayingExpression.Flowing => SegmentKind.Flowing,
                PlayingExpression.Animated => SegmentKind.Animated,
                _ => SegmentKind.Uncertain
            };
            if (segments.Count > 0 && segments[^1].Kind == kind)
                segments[^1] = segments[^1] with { Seconds = segments[^1].Seconds + item.Seconds };
            else segments.Add(new(kind, item.Seconds));
        }
        var phrases = new List<Phrase>();
        double silence = 0, otherGap = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (segment.Kind == SegmentKind.Silence) { silence += segment.Seconds; continue; }
            if (segment.Kind == SegmentKind.Uncertain)
            {
                otherGap += segment.Seconds;
                continue;
            }
            double phraseSeconds = segment.Seconds, mergedGap = 0;
            int last = i;
            while (last + 1 < segments.Count)
            {
                int next = last + 1;
                double gap = 0;
                while (next < segments.Count && !IsExpression(segments[next].Kind))
                {
                    gap += segments[next].Seconds;
                    if (gap > .3) break;
                    next++;
                }
                if (gap > .3 || next >= segments.Count || segments[next].Kind != segment.Kind) break;
                phraseSeconds += segments[next].Seconds;
                mergedGap += gap;
                last = next;
            }
            if (phraseSeconds < .7)
            {
                otherGap += phraseSeconds + mergedGap;
                i = last;
                continue;
            }
            phrases.Add(new(segment.Kind, phraseSeconds, silence, otherGap));
            silence = otherGap = 0;
            i = last;
        }
        return phrases;
    }

    private static bool IsExpression(SegmentKind kind) =>
        kind is SegmentKind.Sustained or SegmentKind.Flowing or SegmentKind.Animated;

    private static List<Phrase> CollapseSameExpression(IReadOnlyList<Phrase> phrases, double maxGap)
    {
        var collapsed = new List<Phrase>();
        foreach (var phrase in phrases)
        {
            if (collapsed.Count > 0 && collapsed[^1].Kind == phrase.Kind &&
                phrase.SilenceBefore + phrase.OtherGapBefore <= maxGap)
                collapsed[^1] = collapsed[^1] with { Seconds = collapsed[^1].Seconds + phrase.Seconds };
            else collapsed.Add(phrase);
        }
        return collapsed;
    }

    private static int CountReplies(IReadOnlyList<Phrase> phrases, SegmentKind kind, double minPhrase,
        double maxPhrase, double minSilence, double maxSilence)
    {
        int count = 0;
        for (int i = 1; i < phrases.Count; i++)
            if (phrases[i - 1].Kind == kind && phrases[i].Kind == kind &&
                phrases[i - 1].Seconds >= minPhrase && phrases[i - 1].Seconds <= maxPhrase &&
                phrases[i].Seconds >= minPhrase && phrases[i].Seconds <= maxPhrase &&
                phrases[i].SilenceBefore >= minSilence && phrases[i].SilenceBefore <= maxSilence && phrases[i].OtherGapBefore <= 1.2)
                count++;
        return count;
    }

    private static int CountPairs(IReadOnlyList<Phrase> phrases, SegmentKind first, double firstSeconds,
        SegmentKind second, double secondSeconds, double maxGap)
    {
        phrases = CollapseSameExpression(phrases, maxGap);
        int count = 0;
        for (int i = 1; i < phrases.Count; i++)
            if (phrases[i - 1].Kind == first && phrases[i - 1].Seconds >= firstSeconds &&
                phrases[i].Kind == second && phrases[i].Seconds >= secondSeconds &&
                phrases[i].SilenceBefore + phrases[i].OtherGapBefore <= maxGap)
                count++;
        return count;
    }

    private static int CountExpressionChanges(IReadOnlyList<Phrase> phrases, double maxGap)
    {
        int count = 0;
        for (int i = 1; i < phrases.Count; i++)
            if (phrases[i - 1].Kind != phrases[i].Kind && phrases[i].SilenceBefore + phrases[i].OtherGapBefore <= maxGap)
                count++;
        return count;
    }

    private static bool HasArc(IReadOnlyList<Phrase> phrases,
        (Func<SegmentKind, bool> Kind, double Seconds) first,
        (Func<SegmentKind, bool> Kind, double Seconds) second,
        (Func<SegmentKind, bool> Kind, double Seconds) third)
    {
        phrases = CollapseSameExpression(phrases, 1.8);
        for (int i = 0; i + 2 < phrases.Count; i++)
            if (first.Kind(phrases[i].Kind) && phrases[i].Seconds >= first.Seconds &&
                second.Kind(phrases[i + 1].Kind) && phrases[i + 1].Seconds >= second.Seconds &&
                third.Kind(phrases[i + 2].Kind) && phrases[i + 2].Seconds >= third.Seconds &&
                phrases[i + 1].SilenceBefore + phrases[i + 1].OtherGapBefore <= 1.8 &&
                phrases[i + 2].SilenceBefore + phrases[i + 2].OtherGapBefore <= 1.8)
                return true;
        return false;
    }
}

/// <summary>Permanent event-earned cosmetics, stored separately from legacy self-reported notes.</summary>
public sealed class StageAchievements
{
    private AchievementDocument _document = new(1, StageStyle.Everyday, []);
    private string? _savedText;
    public string Path { get; }
    public StageStyle Selected => _document.Selected;
    public IReadOnlyList<StageAchievement> Earned => Array.AsReadOnly(_document.Earned);
    public bool IsUnlocked(StageStyle style) => style == StageStyle.Everyday || _document.Earned.Any(x => x.Style == style);

    public StageAchievements(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(Path)) return;
        try
        {
            _savedText = File.ReadAllText(Path);
            var loaded = JsonSerializer.Deserialize<AchievementDocument>(_savedText)
                ?? throw new InvalidDataException("成就文件没有有效内容，原文件已保留。");
            Validate(loaded); _document = loaded;
        }
        catch (JsonException e) { throw new InvalidDataException("成就文件无法读取，原文件已保留。", e); }
    }

    public bool Award(StageTrigger trigger, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (IsUnlocked(trigger.Style)) return false;
        Commit(_document with { Earned = [.. _document.Earned, new(trigger.Style, now, trigger.RuleVersion)] });
        return true;
    }

    public void Select(StageStyle style)
    {
        if (!Enum.IsDefined(style) || !IsUnlocked(style)) throw new InvalidOperationException("这套形象尚未在演奏中解锁。");
        if (_document.Selected == style) return;
        Commit(_document with { Selected = style });
    }

    private void Commit(AchievementDocument next)
    {
        Validate(next);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        using var fileLock = new FileStream(Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if ((File.Exists(Path) ? File.ReadAllText(Path) : null) != _savedText)
            throw new IOException("成就已被另一窗口修改，请重新打开程序；已有记录未覆盖。");
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            string text = JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(temporary, text);
            File.Move(temporary, Path, true);
            _document = next; _savedText = text;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void Validate(AchievementDocument document)
    {
        if (document.SchemaVersion != 1 || !Enum.IsDefined(document.Selected) || document.Earned is null ||
            document.Earned.Any(x => x is null || !Enum.IsDefined(x.Style) || x.Style == StageStyle.Everyday ||
                x.RuleVersion is < 1 or > 2 || x.UnlockedAt == default) ||
            document.Earned.Select(x => x.Style).Distinct().Count() != document.Earned.Length ||
            document.Selected != StageStyle.Everyday && !document.Earned.Any(x => x.Style == document.Selected))
            throw new InvalidDataException("成就文件格式无效，原文件已保留。");
    }
}
