namespace GuitarSoul.Core.Companion;

/// <summary>Where the cat is looking and how it is holding itself, independent of any sound.</summary>
public readonly record struct InnerState(double Gaze, double Lean, double Tail, double Eagerness);

/// <summary>
/// What the cat does when nothing is asking for its attention: it looks away, shifts its weight,
/// and lets its tail keep its own schedule. This layer consumes time only, never sound, so that
/// the same playing no longer produces a byte-identical performance.
/// Attention does not switch this off; it pulls the gaze back and settles the body, the way a cat
/// still flicks its tail while it watches you play.
/// </summary>
public sealed class CompanionInnerLife
{
    private readonly int _seed;
    private Random _random;
    private double _time;
    private double _gaze, _gazeTarget, _gazeUntil, _nextGaze;
    private double _tail, _tailFrom, _tailTo, _tailSeconds, _tailLength, _nextTail;
    private double _eager, _eagerFrom, _eagerTo, _eagerSeconds, _eagerLength, _nextEager;
    private double _leanPhase;

    public CompanionInnerLife(int seed)
    {
        _seed = seed;
        _random = new Random(seed);
        Reset();
    }

    public InnerState Current { get; private set; }

    public void Reset()
    {
        _random = new Random(_seed);
        _time = 0;
        _gaze = _gazeTarget = _gazeUntil = 0;
        _nextGaze = 5 + _random.NextDouble() * 9;
        _tail = _tailFrom = _tailTo = .35;
        _tailSeconds = 0;
        _tailLength = 5 + _random.NextDouble() * 8;
        _nextTail = _tailLength;
        _eager = _eagerFrom = _eagerTo = .7;
        _eagerSeconds = 0;
        _eagerLength = 6 + _random.NextDouble() * 9;
        _nextEager = _eagerLength;
        _leanPhase = _random.NextDouble() * Math.Tau;
        Current = default;
    }

    public InnerState Step(double dt, double attention)
    {
        if (!double.IsFinite(dt) || dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
        attention = double.IsFinite(attention) ? Math.Clamp(attention, 0, 1) : 0;
        _time += dt;

        // Looking away is on its own schedule. Attention shortens the excursion rather than
        // cancelling it, so a focused cat still glances off now and then.
        if (_gazeUntil <= 0 && _time >= _nextGaze)
        {
            _gazeTarget = _random.Next(2) == 0 ? -1 : 1;
            _gazeUntil = _time + 1.2 + _random.NextDouble() * (2.6 - 1.6 * attention);
        }
        double target = _gazeUntil > 0 && _time < _gazeUntil ? _gazeTarget * (1 - .5 * attention) : 0;
        _gaze += (target - _gaze) * (1 - Math.Exp(-dt / .38));
        if (_gazeUntil > 0 && _time >= _gazeUntil)
        {
            _gazeUntil = 0;
            _nextGaze = _time + 7 + _random.NextDouble() * (20 - 10 * attention);
        }

        // The tail runs on a slower clock than the music. It eases between intensities
        // instead of tracking a note, which is why it reads as an animal and not an equaliser.
        if (_time >= _nextTail)
        {
            _tailFrom = _tail;
            _tailTo = .08 + _random.NextDouble() * .85;
            _tailSeconds = 0;
            _tailLength = 4 + _random.NextDouble() * 10;
            _nextTail = _time + _tailLength;
        }
        _tailSeconds += dt;
        double eased = Math.Clamp(_tailSeconds / .9, 0, 1);
        _tail = _tailFrom + (_tailTo - _tailFrom) * (eased * eased * (3 - 2 * eased));

        // Attention rises and falls on its own. This is what keeps a three-minute phrase from
        // becoming three minutes of identical nodding: the cat leans in, drifts, leans in again.
        if (_time >= _nextEager)
        {
            _eagerFrom = _eager;
            _eagerTo = .18 + _random.NextDouble() * .82;
            _eagerSeconds = 0;
            _eagerLength = 5 + _random.NextDouble() * 11;
            _nextEager = _time + _eagerLength;
        }
        _eagerSeconds += dt;
        double eagerEased = Math.Clamp(_eagerSeconds / 1.4, 0, 1);
        _eager = _eagerFrom + (_eagerTo - _eagerFrom) * (eagerEased * eagerEased * (3 - 2 * eagerEased));

        // Weight drifts on a slow cycle, and a second slower one keeps it from repeating.
        _leanPhase += dt * (.15 + .04 * Math.Sin(_time * .31));
        double lean = (Math.Sin(_leanPhase) * .6 + Math.Sin(_leanPhase * .43 + 1.7) * .25) * (1 - .4 * attention);

        Current = new InnerState(_gaze, Math.Clamp(lean, -1, 1), _tail, _eager);
        return Current;
    }
}
