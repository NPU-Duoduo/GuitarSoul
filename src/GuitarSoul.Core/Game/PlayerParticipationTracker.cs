using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Game;

public enum PlayerInputKind { Unselected, IndependentDevice, ComputerLoopback, Mixed }
public sealed record PlayerInputContract(PlayerInputKind Kind, string DeviceId)
{
    public bool IsEligible => Kind == PlayerInputKind.IndependentDevice && !string.IsNullOrWhiteSpace(DeviceId);
}

public sealed record PlayerParticipation(long Epoch, double ObservedAt, bool Eligible, bool Connected,
    bool IsStale, bool IsClipping, bool IsInvalid, bool IsActive, double ContinuousSeconds,
    bool PhraseContinuous, long EffectiveOnsets, double? BeatProximity, double Participation)
{
    public bool Usable => Eligible && Connected && !IsStale && !IsClipping && !IsInvalid;
}

/// <summary>
/// One independent input only. Device selection is the mode contract, not instrument recognition.
/// Call on one owner thread; never pass accompaniment / loopback PCM here.
/// </summary>
public sealed class PlayerParticipationTracker
{
    public const double StaleSeconds = .25;
    private readonly int _sampleRate;
    private readonly PlayerInputContract _contract;
    private readonly SignalAnalyzer _signal;
    private RhythmTracker _rhythm;
    private double _lastPacket = double.NegativeInfinity, _continuous, _silence, _proximity;
    private long _lastSequence = -1, _onsets;
    private bool _active, _clipping, _invalid, _connected = true, _beatReliable;
    public long Epoch { get; private set; }

    public PlayerParticipationTracker(int sampleRate, PlayerInputContract contract, double activityThresholdDb = -45)
    {
        _rhythm = new RhythmTracker(sampleRate); _sampleRate = sampleRate;
        _contract = contract ?? throw new ArgumentNullException(nameof(contract));
        _signal = new SignalAnalyzer { NoiseFloorDb = -90, ActivityThresholdDb = activityThresholdDb };
    }

    public void Reset(long epoch)
    {
        if (epoch < Epoch) throw new ArgumentOutOfRangeException(nameof(epoch));
        Epoch = epoch; _lastPacket = double.NegativeInfinity; _lastSequence = -1;
        _continuous = _silence = _proximity = 0; _onsets = 0;
        _active = _clipping = _invalid = _beatReliable = false;
        _rhythm = new RhythmTracker(_sampleRate); _signal.Reset();
    }

    public void SetConnected(bool connected, long epoch) { Reset(epoch); _connected = connected; }

    /// <summary>Packet end and playback position refer to the same instant. Sequence is unique per epoch.</summary>
    public void Push(ReadOnlySpan<float> mono, long sequence, long epoch, double packetEnd,
        double playbackPosition, TrackArcFrame? beat)
    {
        if (!double.IsFinite(packetEnd) || packetEnd < 0 || !double.IsFinite(playbackPosition) || playbackPosition < 0)
            throw new ArgumentOutOfRangeException(nameof(packetEnd));
        // Late packets from a paused/seeked device epoch cannot revive old activity.
        if (epoch < Epoch || sequence <= _lastSequence && epoch == Epoch) return;
        if (epoch != Epoch) Reset(epoch);
        if (packetEnd <= _lastPacket) return;
        double duration = mono.Length / (double)_sampleRate;
        bool gap = packetEnd - _lastPacket > StaleSeconds;
        _lastPacket = packetEnd; _lastSequence = sequence;
        _invalid = mono.IsEmpty || duration > .25;
        foreach (float sample in mono) if (!float.IsFinite(sample)) { _invalid = true; break; }
        if (!_connected || !_contract.IsEligible || _invalid)
        { _active = false; _continuous = 0; _beatReliable = false; return; }
        if (gap) { _rhythm = new RhythmTracker(_sampleRate); _onsets = 0; _continuous = _silence = 0; }
        var level = _signal.Process(mono);
        var rhythm = _rhythm.Process(mono, _signal.ActivityThresholdDb);
        bool newOnset = rhythm.OnsetCount > _onsets;
        _onsets = rhythm.OnsetCount;
        _clipping = level.IsClipping;
        _active = level.IsActive && !_clipping;
        _continuous = _active ? _continuous + duration : 0;
        _silence = _active ? 0 : _silence + duration;
        _beatReliable = beat is { BeatBpm: > 0, BeatConfidence: >= .65 };
        if (!_beatReliable) _proximity = 0;
        else if (newOnset)
        {
            double period = 60 / beat!.BeatBpm!.Value;
            double onsetPosition = playbackPosition - (rhythm.AudioSeconds - rhythm.LastOnsetSeconds);
            double phase = (onsetPosition - beat.BeatAnchorSeconds) / period;
            double distance = Math.Abs(phase - Math.Round(phase)) * period;
            _proximity = Math.Clamp(1 - distance / .18, 0, 1);
        }
    }

    public PlayerParticipation Read(double now)
    {
        if (!double.IsFinite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
        bool stale = now < _lastPacket - 1e-6 || now - _lastPacket > StaleSeconds;
        bool active = !stale && _connected && !_invalid && _contract.IsEligible && _active;
        // At low beat confidence the entire contribution is continuous activity. Sustains remain active.
        double participation = active ? (_beatReliable ? .7 + .3 * _proximity : 1) : 0;
        return new(Epoch, double.IsFinite(_lastPacket) ? _lastPacket : -1, _contract.IsEligible, _connected,
            stale, _clipping, _invalid, active, active ? _continuous : 0,
            !stale && _connected && !_invalid && !_clipping && _contract.IsEligible && (active || _silence < .35 && _onsets > 0),
            _onsets, _beatReliable && !stale ? _proximity : null, participation);
    }
}
