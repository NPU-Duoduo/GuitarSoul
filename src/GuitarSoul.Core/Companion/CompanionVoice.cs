using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Companion;

/// <summary>
/// Short, occasional observations of stable acoustic cues. This is a local character voice,
/// not recognition of a song, a player's emotions, or performance quality.
/// </summary>
public sealed class CompanionVoice
{
    private readonly record struct Cue(ToneColor Tone, PlayingExpression Expression, DynamicContour Dynamics);
    private Cue? _candidate, _lastSpoken;
    private ExpressionStyle? _style;
    private double _time, _activeSeconds, _stableSeconds, _gapSeconds, _hideAt, _nextSpeech;
    public string? Current { get; private set; }
    /// <summary>The evidence attached to the currently visible observation, cleared when it disappears.</summary>
    public ListeningFeatures Source { get; private set; }

    public void Reset()
    {
        _candidate = _lastSpoken = null;
        _style = null;
        _time = _activeSeconds = _stableSeconds = _gapSeconds = _hideAt = _nextSpeech = 0;
        Clear();
    }

    public string? Step(ListeningFeatures listening, bool active, bool quiet, ExpressionStyle style, double elapsedSeconds,
        GuitarEvidenceDecision guitarEvidence = GuitarEvidenceDecision.Confirmed)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style));
        _time += Math.Min(elapsedSeconds, .25);
        bool styleChanged = _style.HasValue && _style != style;
        _style = style;
        bool wellFormed = double.IsFinite(listening.Confidence) && listening.Confidence is >= 0 and <= 1 &&
            Enum.IsDefined(listening.Tone) && Enum.IsDefined(listening.Expression) && Enum.IsDefined(listening.Dynamics);
        bool valid = wellFormed && listening.Confidence >= .6 &&
            (listening.Tone != ToneColor.Unknown || listening.Expression != PlayingExpression.None || listening.Dynamics != DynamicContour.Steady);
        // A callback gap cannot extend a sentence or count as evidence that the sound continued.
        if (quiet || guitarEvidence != GuitarEvidenceDecision.Confirmed || !wellFormed || styleChanged || elapsedSeconds > .6)
        {
            _candidate = null;
            _activeSeconds = _stableSeconds = _gapSeconds = 0;
            Clear();
            return null;
        }
        if (!active || !valid)
        {
            _gapSeconds += Math.Min(elapsedSeconds, .25);
            // A note's short gap preserves progress, but contributes no listening evidence.
            if (_gapSeconds > .65)
            {
                _candidate = null;
                _activeSeconds = _stableSeconds = 0;
            }
            // A visible observation refers to the phrase just heard. Keep it readable
            // through a detached note's gap, without extending its display deadline.
            if (active || _gapSeconds > .65 || _time >= _hideAt) Clear();
            return Current;
        }

        double dt = Math.Min(elapsedSeconds, .25);
        _gapSeconds = 0;
        _activeSeconds += dt;
        var cue = GetCue(listening);
        if (_candidate != cue) { _candidate = cue; _stableSeconds = 0; }
        _stableSeconds += dt;
        if (Current is not null && _time >= _hideAt) Clear();
        bool curious = style == ExpressionStyle.Curious;
        if (Current is not null && _stableSeconds >= (curious ? 2 : 3) &&
            cue != GetCue(Source)) Clear();
        if (Current is null && _time >= _nextSpeech && _lastSpoken != cue &&
            _activeSeconds >= (curious ? 4 : 6) && _stableSeconds >= (curious ? 2 : 3))
        {
            Current = Observe(cue, curious);
            Source = listening;
            _lastSpoken = cue;
            _hideAt = _time + (curious ? 5 : 4.5);
            _nextSpeech = _time + (curious ? 24 : 36);
        }
        return Current;
    }

    private void Clear() { Current = null; Source = ListeningFeatures.Empty; }

    // Confirm the evidence the sentence actually describes. A flowing phrase may cross
    // several tone colours without interrupting the same articulation observation.
    private static Cue GetCue(ListeningFeatures listening) => listening.Dynamics != DynamicContour.Steady
        ? new(ToneColor.Unknown, PlayingExpression.None, listening.Dynamics)
        : new(listening.Expression is PlayingExpression.Flowing or PlayingExpression.Animated
            ? ToneColor.Unknown : listening.Tone, listening.Expression, DynamicContour.Steady);

    private static string Observe(Cue cue, bool curious)
    {
        if (cue.Dynamics == DynamicContour.Building)
            return curious ? "我听见琴声一点点展开，耳朵也跟过去了。" : "我听着这段琴声慢慢展开。";
        if (cue.Dynamics == DynamicContour.Easing)
            return curious ? "琴声轻轻收住，我也把爪子放轻。" : "我陪这段琴声慢慢收住。";
        return cue.Expression switch
        {
            PlayingExpression.Sustained => cue.Tone switch
            {
                ToneColor.Warm => curious ? "我想再听一会儿，这个长音暖暖的。" : "我陪着这个温厚的长音。",
                ToneColor.Bright => curious ? "我竖着耳朵，这个长音亮亮的。" : "我听着这个明亮的长音。",
                _ => curious ? "这个音还在延伸，我也待在这里。" : "我听着，让这个音慢慢延伸。"
            },
            PlayingExpression.Animated => curious ? "拨弦密起来了，我的爪子也想跟一小段。" : "我听见拨弦密起来了，轻轻跟着。",
            PlayingExpression.Flowing => curious ? "这些音连着过来，我跟着轻轻晃一会儿。" : "我听着这些音一个接着一个。",
            _ => cue.Tone == ToneColor.Bright
                ? curious ? "这次琴声亮亮的，我竖起耳朵了。" : "我听着这份明亮的琴声。"
                : curious ? "琴声暖暖的，我想凑近听一会儿。" : "我在听这份温厚的琴声。"
        };
    }
}
