using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

internal enum ReceptionState { Stopped, Waiting, Receiving, BelowThreshold, Silent, NoData }

// A connected endpoint is not evidence that music reached the application.
internal sealed class AudioReception
{
    private TimeSpan? _lastSample;
    private TimeSpan? _lastSound;
    private TimeSpan? _lastActive;
    public bool HasHeardSound => _lastSound.HasValue;

    public void Reset() => _lastSample = _lastSound = _lastActive = null;

    public void Observe(SignalFrame frame, TimeSpan capturedAt)
    {
        _lastSample = capturedAt;
        if (frame.IsActive || frame.Peak >= .001) _lastSound = capturedAt;
        if (frame.IsActive) _lastActive = capturedAt;
    }

    public ReceptionState GetState(bool capturing, TimeSpan now)
    {
        if (!capturing) return ReceptionState.Stopped;
        if (!_lastSample.HasValue) return ReceptionState.Waiting;
        if ((now - _lastSample.Value).TotalSeconds >= .6)
            return HasHeardSound ? ReceptionState.NoData : ReceptionState.Waiting;
        if (_lastActive.HasValue && (now - _lastActive.Value).TotalSeconds < .3) return ReceptionState.Receiving;
        if (_lastSound.HasValue && (now - _lastSound.Value).TotalSeconds < .3) return ReceptionState.BelowThreshold;
        return HasHeardSound ? ReceptionState.Silent : ReceptionState.Waiting;
    }
}
