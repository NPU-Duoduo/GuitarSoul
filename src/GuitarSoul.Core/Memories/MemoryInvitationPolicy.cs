namespace GuitarSoul.Core.Memories;

/// <summary>At most one invitation per running-app session. A displayed invitation is consumed
/// even when ignored, hidden by activity or disabled. This class never writes memories or links.</summary>
public sealed class MemoryInvitationPolicy(DateTimeOffset sessionStartedAt)
{
    private double _listeningSeconds, _quietSeconds, _visibleSeconds;
    private bool _consumed;
    public Guid? PendingMemoryId { get; private set; }

    public Guid? Step(IReadOnlyList<SharedMemory> memories, double elapsedSeconds, bool listening,
        bool active, bool enabled, bool quiet, bool presentationVisible, bool playbackOrRecording)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var dt = Math.Min(elapsedSeconds, 1);
        if (listening) _listeningSeconds += dt;
        _quietSeconds = listening && !active ? _quietSeconds + dt : 0;
        bool allowed = listening && enabled && !quiet && presentationVisible && !playbackOrRecording;
        if (PendingMemoryId is Guid id)
        {
            _visibleSeconds += dt;
            if (!allowed || active || _visibleSeconds >= 30 || !memories.Any(m => m.Id == id)) Dismiss();
            return PendingMemoryId;
        }
        if (_consumed || !allowed || active || _listeningSeconds < 90 || _quietSeconds < 20) return null;
        // New memories can be revisited in the next app session; never immediately solicit repetition.
        var memory = memories.Where(m => m.CreatedAt < sessionStartedAt)
            .OrderByDescending(m => m.UpdatedAt).ThenBy(m => m.Id).FirstOrDefault();
        if (memory is null) return null;
        PendingMemoryId = memory.Id;
        _visibleSeconds = 0;
        _consumed = true;
        return memory.Id;
    }

    public void Dismiss() { PendingMemoryId = null; _visibleSeconds = 0; }
}
