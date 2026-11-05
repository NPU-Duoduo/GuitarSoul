namespace GuitarSoul.Core.Companion;

public enum CompanionMood { Waiting, Listening, Excited, Resting }
public readonly record struct CompanionState(CompanionMood Mood, double Energy);

/// <summary>Expressive reactions to input activity, not psychological emotion inference.</summary>
public sealed class CompanionStateMachine
{
    private double _activeSeconds;
    private double _quietSeconds;
    private double _strongSeconds;
    private double _energy;
    private CompanionMood _mood;
    public CompanionState Current => new(_mood, _energy);

    public CompanionState Step(bool active, double energy, double elapsedSeconds)
    {
        if (!double.IsFinite(energy) || !double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        var dt = Math.Min(elapsedSeconds, 1); // A stalled UI must not jump through several states.
        var target = active ? Math.Clamp(energy, 0, 1) : 0;
        _energy += (target - _energy) * (1 - Math.Exp(-dt / (target > _energy ? .25 : .9)));
        _activeSeconds = active ? _activeSeconds + dt : 0;
        _quietSeconds = active ? 0 : _quietSeconds + dt;
        _strongSeconds = active && _energy >= .55 ? Math.Min(3, _strongSeconds + dt) : Math.Max(0, _strongSeconds - dt * 2);

        if (_quietSeconds >= 15) _mood = CompanionMood.Resting;
        else if (_quietSeconds >= 1.8) _mood = CompanionMood.Waiting;
        else if (_strongSeconds >= 2) _mood = CompanionMood.Excited;
        else if (_activeSeconds >= .2 && _mood is CompanionMood.Waiting or CompanionMood.Resting)
            _mood = CompanionMood.Listening;
        else if (_mood == CompanionMood.Excited && _strongSeconds == 0)
            _mood = CompanionMood.Listening;
        return Current;
    }

    public void Reset()
    {
        _activeSeconds = _quietSeconds = _strongSeconds = _energy = 0;
        _mood = CompanionMood.Waiting;
    }
}
