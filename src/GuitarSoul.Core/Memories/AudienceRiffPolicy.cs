using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Memories;

public sealed record AudienceRiff(double Seconds, string Title, string Note);

/// <summary>Opt-in, bounded phrase selection. Audio time prevents UI stalls from fabricating a phrase.</summary>
public sealed class AudienceRiffPolicy
{
    private double _lastTime, _phraseStart = -1, _lastActive, _activeSeconds, _nextSave;
    private long _startOnsets;
    private double _brightness, _drive;
    public int SelectedCount { get; private set; }
    public void Reset()
    {
        _lastTime = _lastActive = _activeSeconds = _nextSave = _brightness = _drive = 0;
        _phraseStart = -1; _startOnsets = 0; SelectedCount = 0;
    }
    public AudienceRiff? Step(RhythmFrame sound, bool active, bool enabled, bool inhibited)
    {
        if (!double.IsFinite(sound.AudioSeconds) || sound.AudioSeconds < 0) return null;
        double dt = sound.AudioSeconds - _lastTime;
        _lastTime = sound.AudioSeconds;
        if (dt <= 0) return null;
        if (!enabled || inhibited || dt > .6 || SelectedCount >= 6 || sound.AudioSeconds < _nextSave)
        {
            _phraseStart = -1; _activeSeconds = 0;
            return null;
        }
        if (active)
        {
            if (_phraseStart < 0)
            {
                _phraseStart = sound.AudioSeconds - dt;
                _startOnsets = sound.OnsetCount; _brightness = _drive = _activeSeconds = 0;
            }
            _activeSeconds += dt; _lastActive = sound.AudioSeconds;
            _brightness += sound.Brightness * dt; _drive += sound.Drive * dt;
        }
        if (_phraseStart < 0) return null;
        double length = sound.AudioSeconds - _phraseStart;
        bool ended = sound.AudioSeconds - _lastActive >= 1.2;
        if (!ended && length < 24) return null;
        var candidate = _activeSeconds >= 8 && sound.OnsetCount - _startOnsets >= 5;
        var duration = Math.Min(28, length + .35);
        var bright = _brightness / Math.Max(.01, _activeSeconds);
        var drive = _drive / Math.Max(.01, _activeSeconds);
        _phraseStart = -1; _activeSeconds = 0;
        if (!candidate) return null;
        SelectedCount++; _nextSave = sound.AudioSeconds + 90;
        return drive > .4
            ? new(duration, "这段让我摇起来", "刚才一串接一串的拨弦，我想留着再听。")
            : bright > .35
                ? new(duration, "耳朵竖起来的一段", "琴声亮起来的这一小段，我悄悄留了下来。")
                : new(duration, "坐在旁边听的一段", "你刚才连着弹了一会儿，我想记住这一小段。");
    }
}
