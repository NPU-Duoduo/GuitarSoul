namespace GuitarSoul.Core.Game;

public enum GameMomentState { Ready, Following, Anticipating, Together, SongHighlight, Release, Paused, InputUnavailable, Quiet, Ended }
public enum PlaybackRunState { Ready, Playing, Paused, Stopped, Ended }
public sealed record PlaybackClock(string TrackId, long Epoch, double PositionSeconds, double MonotonicSeconds, PlaybackRunState State);
public sealed record GameMomentFrame(GameMomentState State, string Reason, string? HighlightId,
    bool HoldGuitar, bool Animate, int SyncSegments, int SharedHighlights, double LongestParticipationSeconds,
    bool Discontinuity, string? NewlySharedHighlightId);

/// <summary>
/// Deterministic shadow rules. All time comes from explicit playback / monotonic clocks.
/// No RNG, CompanionDirector, recognition gate, achievements, persistence or renderer.
/// </summary>
public sealed class GameMomentDirector
{
    public const double StopGraceSeconds = 1;
    private TrackArc _arc;
    private PlaybackClock? _previous;
    private readonly HashSet<string> _shared = new(StringComparer.Ordinal);
    private string? _currentHighlight;
    private double _joinSeconds, _sharedSeconds, _silence, _sync, _streak, _longest;
    private double _acceptPacketsAfter = -1;
    private bool _wasActive, _terminal;
    private GameMomentState _state = GameMomentState.Ready;
    public GameMomentDirector(TrackArc arc) { _arc = arc ?? throw new ArgumentNullException(nameof(arc)); }
    public IReadOnlyCollection<string> SharedHighlightIds => _shared.Order(StringComparer.Ordinal).ToArray();

    /// <summary>A new song or explicit replay creates a new session; seek/loop keeps the deduplication ledger.</summary>
    public void Load(TrackArc arc)
    {
        _arc = arc ?? throw new ArgumentNullException(nameof(arc)); _previous = null;
        _shared.Clear(); _longest = 0; _acceptPacketsAfter = -1; _terminal = false;
        ResetTransient(); _state = GameMomentState.Ready;
    }

    public GameMomentFrame Step(PlaybackClock clock, PlayerParticipation player, bool quiet = false, bool reducedMotion = false)
    {
        if (clock.TrackId != _arc.TrackId) throw new ArgumentException("Load the matching track before stepping.", nameof(clock));
        if (!Enum.IsDefined(clock.State) || !double.IsFinite(clock.PositionSeconds) || !double.IsFinite(clock.MonotonicSeconds) ||
            clock.PositionSeconds < 0 || clock.PositionSeconds > _arc.DurationSeconds + .001 || clock.MonotonicSeconds < 0 || clock.Epoch < 0)
            throw new ArgumentOutOfRangeException(nameof(clock));
        if (_previous is { } old && (clock.MonotonicSeconds < old.MonotonicSeconds || clock.Epoch < old.Epoch))
            throw new ArgumentException("Clock and epoch must be monotonic.", nameof(clock));
        double dt = _previous is null ? 0 : clock.MonotonicSeconds - _previous.MonotonicSeconds;
        double travel = _previous is null ? 0 : clock.PositionSeconds - _previous.PositionSeconds;
        bool discontinuity = _previous is not null && (clock.Epoch != _previous.Epoch || clock.State != _previous.State ||
            dt > .25 || clock.State == PlaybackRunState.Playing && Math.Abs(travel - dt) > .15);
        if (discontinuity) { ResetTransient(); _acceptPacketsAfter = clock.MonotonicSeconds; dt = 0; }
        _previous = clock;
        if (_terminal || clock.State is PlaybackRunState.Stopped or PlaybackRunState.Ended || clock.PositionSeconds >= _arc.DurationSeconds)
        { _terminal = true; ResetTransient(); return Output(GameMomentState.Ended, "session-ended", null); }
        if (clock.State == PlaybackRunState.Paused) { ResetTransient(); return Output(GameMomentState.Paused, "playback-paused", null); }
        if (clock.State == PlaybackRunState.Ready) { ResetTransient(); return Output(GameMomentState.Ready, "ready", null); }
        if (quiet) { ResetTransient(); _acceptPacketsAfter = clock.MonotonicSeconds; return Output(GameMomentState.Quiet, "quiet-overrides-game", null); }
        bool fresh = player.Usable && double.IsFinite(player.Participation) && player.Participation is >= 0 and <= 1 &&
            double.IsFinite(player.ContinuousSeconds) && player.ContinuousSeconds >= 0 &&
            player.Epoch == clock.Epoch && player.ObservedAt > _acceptPacketsAfter &&
            clock.MonotonicSeconds - player.ObservedAt is >= 0 and <= PlayerParticipationTracker.StaleSeconds;
        if (!fresh)
        { ResetTransient(); _acceptPacketsAfter = clock.MonotonicSeconds; return Output(GameMomentState.InputUnavailable, "independent-input-not-ready", null); }
        bool active = player.IsActive;
        // Credit only elapsed intervals whose two endpoints both contain fresh player activity.
        double credited = active && _wasActive ? dt : 0;
        _wasActive = active;
        _joinSeconds = active ? _joinSeconds + dt : 0;
        _silence = active ? 0 : _silence + dt;
        _streak = active ? _streak + credited : 0;
        _longest = Math.Max(_longest, _streak);
        _sync = Math.Clamp(_sync + (active ? dt * player.Participation / 3 : -dt / 2), 0, 1);
        var highlight = _arc.Highlights.FirstOrDefault(h => clock.PositionSeconds >= h.Start && clock.PositionSeconds < h.End);
        if (highlight is not null)
        {
            bool entered = _currentHighlight != highlight.HighlightId;
            if (entered) { _currentHighlight = highlight.HighlightId; _sharedSeconds = 0; }
            // Grace can only sustain Together in this same highlight; it cannot grant entry.
            bool together = active && _joinSeconds >= .2 || !entered && _state == GameMomentState.Together && _silence < StopGraceSeconds;
            string? newlyShared = null;
            if (together && active)
            {
                _sharedSeconds += Math.Min(credited, Math.Max(0, clock.PositionSeconds - highlight.Start));
                if (_sharedSeconds >= .5 && _shared.Add(highlight.HighlightId)) newlyShared = highlight.HighlightId;
            }
            return Output(together ? GameMomentState.Together : GameMomentState.SongHighlight,
                together ? active ? "highlight-and-player-active" : "player-stop-grace" : "highlight-without-player",
                highlight.HighlightId, newlyShared);
        }
        _currentHighlight = null; _sharedSeconds = 0;
        var release = _arc.Highlights.FirstOrDefault(h => clock.PositionSeconds >= h.End && clock.PositionSeconds < h.ReleaseEnd);
        if (release is not null) return Output(GameMomentState.Release, "highlight-ended", release.HighlightId);
        var upcoming = _arc.Highlights.FirstOrDefault(h => clock.PositionSeconds >= h.BuildingStart && clock.PositionSeconds < h.Start);
        if (upcoming is not null && active) return Output(GameMomentState.Anticipating, "highlight-in-three-seconds-or-less", upcoming.HighlightId);
        return Output(active ? GameMomentState.Following : GameMomentState.Ready, active ? "player-active" : "waiting-for-player", null);

        GameMomentFrame Output(GameMomentState state, string reason, string? id, string? newlyShared = null)
        {
            _state = state;
            return new(state, reason, id, state == GameMomentState.Together,
                !reducedMotion && state is GameMomentState.Following or GameMomentState.Anticipating or GameMomentState.Together or GameMomentState.SongHighlight or GameMomentState.Release,
                (int)Math.Ceiling(_sync * 4), _shared.Count, _longest, discontinuity, newlyShared);
        }
    }

    private void ResetTransient()
    {
        _currentHighlight = null; _joinSeconds = _sharedSeconds = _silence = _sync = _streak = 0;
        _wasActive = false; _state = GameMomentState.Ready;
    }
}
