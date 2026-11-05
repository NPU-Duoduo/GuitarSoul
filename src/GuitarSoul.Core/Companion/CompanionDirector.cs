using GuitarSoul.Core.Audio;

namespace GuitarSoul.Core.Companion;

public enum CompanionAction { Idle, Notice, Listen, Groove, Engage, Settle, Groom, Rest, Jam }
public enum ExpressionStyle { Reserved, Curious }
public readonly record struct CompanionPose(CompanionAction Action, CompanionMood Mood, double Weight)
{
    /// <summary>Continuous response to observed articulation and relative dynamics, never absolute loudness.</summary>
    public double Engagement { get; init; }
    /// <summary>Separates the evidence-driven response from legacy callers and explicit pose previews.</summary>
    public bool HasMusicalEvidence { get; init; }
    /// <summary>Horizontal look direction, -1 to 1. Driven by the cat's own clock, not by sound.</summary>
    public double Gaze { get; init; }
    /// <summary>Weight shift, -1 to 1. Keeps the body from being perfectly still.</summary>
    public double Lean { get; init; }
    /// <summary>Tail liveliness, 0 to 1. Eases between intensities on its own schedule.</summary>
    public double Tail { get; init; }
    /// <summary>
    /// How much movement the cat actually spends, 0 to 1, or null when the caller did not ask.
    /// A missing value must not read as "no enthusiasm", so it stays nullable rather than 0.
    /// </summary>
    public double? Eagerness { get; init; }
}

/// <summary>
/// Responds to observable activity, never absolute volume, performance quality or inferred emotion.
/// The caller supplies elapsed time; a seed makes a complete session replayable. Weight is an enter/exit
/// envelope. Settle starts at one and fades the renderer's previous pose back to its resting position.
/// Reliable musical evidence can sustain and vary movement; joining with a guitar remains a bounded gesture.
/// </summary>
public sealed class CompanionDirector
{
    private readonly int _seed;
    private readonly CompanionInnerLife _inner;
    private InnerState _innerState;
    private Random _random;
    private double _time, _started, _duration, _activeSeconds, _quietSeconds, _steadySeconds, _unsteadySeconds;
    private double _nextNotice, _nextGroove, _nextEngage, _nextGroom;
    private double _nextJam;
    private double _expressiveSeconds, _nextExpressiveEngage = 3, _nextExpressiveJam = 12;
    private ToneColor _heardTone, _candidateTone;
    private double _toneSeconds, _heldSeconds, _flowingSeconds, _nextToneNotice;
    private bool _toneNoticePending;
    private bool _reactiveMode, _jamCuePending, _hasJoined;
    private double _engagement, _musicalSeconds, _lastMusicalOnset, _lostMusicalEvidence;
    private double _movementEntryWeight, _settleEntryWeight;
    private double _reactiveNextJam, _jamVariationSeconds, _jamDrive, _jamCueExpires, _changeDrive;
    private int _musicalOnsets;
    private long _lastSectionCue;
    private double _sectionJoinUntil, _nextSectionJoin;
    private bool _pluckHighlighted;
    private double _pluckReleaseSeconds;
    private long _observedHarmonyChange;
    private bool _hasHarmonyContext, _harmonyChanged;
    private double _harmonyNoticeUntil, _nextHarmonyNotice;
    private double _lastHarmonicTurn = -1, _harmonicSpan = 2;
    private PlayingExpression _jamExpression, _changeExpression;
    private DynamicContour _jamDynamics, _changeDynamics;
    private CompanionAction _action = CompanionAction.Idle;
    private bool _wasQuiet;
    public CompanionPose Current { get; private set; } = new(CompanionAction.Idle, CompanionMood.Waiting, 0);

    /// <summary>
    /// A seed makes a session replayable. Left unset, each run gets its own, so the cat is not
    /// the same recording every time the application starts.
    /// </summary>
    public CompanionDirector(int? seed = null)
    {
        _seed = seed ?? Environment.TickCount;
        _random = new Random(_seed);
        _inner = new CompanionInnerLife(_seed ^ 0x5F3A);
        Reset();
    }

    public void Reset()
    {
        _random = new Random(_seed);
        _inner.Reset();
        _time = _started = _duration = _activeSeconds = _quietSeconds = _steadySeconds = _unsteadySeconds = 0;
        _action = CompanionAction.Idle;
        _nextNotice = 0;
        _nextGroove = 3;
        _nextEngage = Between(35, 50);
        _nextGroom = Between(10, 18);
        _nextJam = 45;
        _expressiveSeconds = 0; _nextExpressiveEngage = 3; _nextExpressiveJam = 12;
        _heardTone = _candidateTone = ToneColor.Unknown;
        _toneSeconds = _heldSeconds = _flowingSeconds = _nextToneNotice = 0;
        _toneNoticePending = false;
        _reactiveMode = _jamCuePending = _hasJoined = false;
        _engagement = _musicalSeconds = _lostMusicalEvidence = _reactiveNextJam = _jamVariationSeconds = _jamDrive = 0;
        _lastMusicalOnset = -100;
        _movementEntryWeight = _settleEntryWeight = 0;
        _musicalOnsets = 0;
        _lastSectionCue = 0; _sectionJoinUntil = _nextSectionJoin = 0;
        _pluckHighlighted = false; _pluckReleaseSeconds = 0;
        _observedHarmonyChange = 0; _hasHarmonyContext = _harmonyChanged = false;
        _harmonyNoticeUntil = _nextHarmonyNotice = 0;
        _lastHarmonicTurn = -1; _harmonicSpan = 2;
        _jamExpression = PlayingExpression.None;
        _jamDynamics = DynamicContour.Steady;
        _jamCueExpires = double.PositiveInfinity;
        _changeDrive = 0;
        _changeExpression = PlayingExpression.None;
        _changeDynamics = DynamicContour.Steady;
        _wasQuiet = false;
        Current = new(_action, CompanionMood.Waiting, 0);
    }

    public CompanionPose Step(bool active, bool steadyRhythm, bool newOnset, double elapsedSeconds,
        bool quiet, ExpressionStyle style, double performanceDrive = 0, ListeningFeatures listening = default,
        GuitarEvidenceDecision guitarEvidence = GuitarEvidenceDecision.Confirmed)
    {
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        if (!Enum.IsDefined(style)) throw new ArgumentOutOfRangeException(nameof(style));
        // A suspended UI must not replay a backlog of gestures on resume.
        var dt = Math.Min(elapsedSeconds, .25);
        _time += dt;
        // The cat's own clock runs whether or not anything is being played. Quiet mode asks it
        // to hold still, so it is treated as full attention rather than as an absence of sound.
        _innerState = _inner.Step(dt, quiet ? 1 : _engagement);
        _activeSeconds = active ? _activeSeconds + dt : 0;
        _quietSeconds = active ? 0 : _quietSeconds + dt;
        bool reliable = active && double.IsFinite(listening.Confidence) && listening.Confidence >= .6;
        bool sustained = reliable && listening.Expression == PlayingExpression.Sustained;
        bool animated = reliable && listening.Expression == PlayingExpression.Animated;
        bool flowing = reliable && listening.Expression == PlayingExpression.Flowing;
        _heldSeconds = sustained ? _heldSeconds + dt : 0;
        _flowingSeconds = flowing ? _flowingSeconds + dt : Math.Max(0, _flowingSeconds - dt * 2);
        _steadySeconds = active && steadyRhythm && !sustained ? _steadySeconds + dt : Math.Max(0, _steadySeconds - dt * 2);
        _unsteadySeconds = steadyRhythm && active && !sustained ? 0 : _unsteadySeconds + dt;
        bool curious = style == ExpressionStyle.Curious;
        bool harmonicEvidence = reliable && listening.Harmony.IsKnown &&
            double.IsFinite(listening.Harmony.Strength) && listening.Harmony.Strength >= .86;
        _harmonyChanged = !quiet && harmonicEvidence && _hasHarmonyContext &&
            listening.Harmony.ChangeId > _observedHarmonyChange;
        _observedHarmonyChange = Math.Max(_observedHarmonyChange, listening.Harmony.ChangeId);
        if (harmonicEvidence) _hasHarmonyContext = true;
        if (!active && _quietSeconds >= .65 || quiet)
        {
            _hasHarmonyContext = false; _harmonyNoticeUntil = 0;
            _lastHarmonicTurn = -1; _harmonicSpan = 2;
        }
        if (_harmonyChanged)
        {
            if (_lastHarmonicTurn >= 0) _harmonicSpan = Math.Clamp(_time - _lastHarmonicTurn, .5, 4);
            _lastHarmonicTurn = _time;
            _harmonyNoticeUntil = _time + (curious ? 1.2 : 1.5);
        }
        bool expressive = active && !sustained && (animated || double.IsFinite(performanceDrive) && performanceDrive >= .35);
        _expressiveSeconds = expressive ? _expressiveSeconds + dt : Math.Max(0, _expressiveSeconds - dt);
        TrackTone(reliable && !quiet ? listening.Tone : ToneColor.Unknown, dt, curious);
        double grace = curious ? 2.2 : 1.7;
        bool attending = active || _quietSeconds < grace;
        var age = _time - _started;

        if (quiet)
        {
            _wasQuiet = true;
            ClearMusicalResponse();
            _action = attending && (_activeSeconds > 0 || Current.Mood == CompanionMood.Listening)
                ? CompanionAction.Listen : _quietSeconds >= 30 ? CompanionAction.Rest : CompanionAction.Idle;
            _started = _time;
            return Current = Pose(0);
        }
        if (_wasQuiet)
        {
            _wasQuiet = false;
            _nextGroove = _time + 3;
            _nextEngage = _time + Between(35, 50);
            _nextGroom = _time + Between(10, 18);
            _nextJam = _time + 45;
            _nextExpressiveJam = _time + 12; _nextExpressiveEngage = _time + 3;
            _nextToneNotice = _time + 4;
            _heldSeconds = _flowingSeconds = _expressiveSeconds = 0;
            _reactiveNextJam = _time + 4;
        }

        // This path is governed by the sound that is present now. A continuing dense phrase
        // does not have to finish an animation, settle, and wait for another timed admission.
        // Callers without listening features retain their existing bounded gesture contract.
        if (RespondToMusic(active, steadyRhythm, newOnset, reliable, sustained, animated, flowing,
            attending, dt, curious, performanceDrive, listening,
            guitarEvidence == GuitarEvidenceDecision.Confirmed))
            return Current = Pose(ReactiveWeight());

        if (_action is CompanionAction.Notice or CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Groom or CompanionAction.Jam)
        {
            bool stopRhythm = _action is CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam &&
                (_unsteadySeconds >= 1.2 && !expressive && !flowing || _heldSeconds >= .4);
            bool interruptGroom = _action == CompanionAction.Groom && active;
            if (age >= _duration || (!attending && _action != CompanionAction.Groom && age >= .65) || stopRhythm || interruptGroom ||
                (_action == CompanionAction.Jam && !active))
                Start(CompanionAction.Settle, curious ? 1.15 : .8);
        }
        else if (_action == CompanionAction.Settle)
        {
            // During the silence grace period hold the finished settle. Returning to Listen
            // here would start a second settle on the next tick when the grace expires.
            if (age >= _duration && (active || !attending))
                Start(attending ? CompanionAction.Listen : _quietSeconds >= 30 ? CompanionAction.Rest : CompanionAction.Idle);
        }
        else if (attending && (active || newOnset))
        {
            if (_action is CompanionAction.Idle or CompanionAction.Rest)
            {
                if (_time >= _nextNotice && (newOnset || _activeSeconds >= .15))
                {
                    Start(CompanionAction.Notice, curious ? 1.25 : .8);
                    _nextNotice = _time + 8;
                }
                else if (_activeSeconds >= .2) Start(CompanionAction.Listen);
            }
            else if (_action == CompanionAction.Listen)
            {
                if (_toneNoticePending && _time >= _nextToneNotice && _time >= _nextNotice && age >= 1.5)
                {
                    Start(CompanionAction.Notice, curious ? 1.25 : .8);
                    _toneNoticePending = false;
                    _nextToneNotice = _time + (curious ? 16 : 24);
                    _nextNotice = _time + 8;
                }
                else if (guitarEvidence == GuitarEvidenceDecision.Confirmed && !sustained &&
                    _expressiveSeconds >= (animated ? curious ? 8 : 16 : 3) && _time >= _nextExpressiveJam && newOnset)
                {
                    Start(CompanionAction.Jam, curious ? 6.4 : 4.8);
                    _nextJam = _nextExpressiveJam = _time + Between(40, 65);
                    _nextExpressiveEngage = _time + 12;
                    _nextGroove = _time + 8;
                }
                else if (!sustained && _expressiveSeconds >= (animated ? curious ? 1.2 : 5.5 : .8) && _time >= _nextExpressiveEngage && newOnset)
                {
                    Start(CompanionAction.Engage, curious ? 5.5 : 4.5);
                    _nextExpressiveEngage = _time + (animated && !curious ? Between(18, 25) : Between(10, 15));
                    _nextEngage = _time + 15;
                    _nextGroove = _time + 8;
                }
                else if (guitarEvidence == GuitarEvidenceDecision.Confirmed && !sustained &&
                    _steadySeconds >= 12 && _time >= _nextJam && age >= 1.5 && newOnset)
                {
                    Start(CompanionAction.Jam, curious ? 6.4 : 4.8);
                    _nextJam = _time + Between(90, 140);
                    _nextExpressiveJam = _nextJam;
                    _nextGroove = _time + Between(8, 14);
                }
                else if (!sustained && _steadySeconds >= 8 && _time >= _nextEngage)
                {
                    Start(CompanionAction.Engage, curious ? 2.4 : 1.6);
                    _nextEngage = _time + Between(65, 95);
                    _nextGroove = _time + Between(8, 14);
                }
                else if (!sustained && (_steadySeconds >= (curious ? 2.5 : 4) || _flowingSeconds >= (curious ? 3 : 5)) && _time >= _nextGroove && age >= 1.5)
                {
                    Start(CompanionAction.Groove, curious ? 5.5 : 3.8);
                    _nextGroove = _time + Between(12, 20);
                }
            }
        }
        else if (!attending)
        {
            if (_action == CompanionAction.Listen) Start(CompanionAction.Settle, curious ? 1.15 : .8);
            else if (_quietSeconds >= 30) Start(CompanionAction.Rest);
            else if (_action == CompanionAction.Idle && _quietSeconds >= 6 && _time >= _nextGroom)
            {
                Start(CompanionAction.Groom, 2.5);
                _nextGroom = _time + Between(40, 65);
            }
        }

        age = _time - _started;
        double weight = _action switch
        {
            CompanionAction.Settle => 1 - Smooth(age / _duration),
            CompanionAction.Notice or CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Groom or CompanionAction.Jam =>
                Math.Min(Smooth(age / .25), Smooth((_duration - age) / .4)),
            _ => 0
        };
        return Current = Pose(weight);
    }

    private bool RespondToMusic(bool active, bool steadyRhythm, bool newOnset, bool reliable,
        bool sustained, bool animated, bool flowing, bool attending, double dt, bool curious,
        double performanceDrive, ListeningFeatures listening, bool allowJam)
    {
        double drive = double.IsFinite(performanceDrive) ? Math.Clamp(performanceDrive, 0, 1) : 0;
        bool musical = reliable && (sustained || animated || flowing || steadyRhythm || drive >= .35);
        if (!_reactiveMode && !musical) return false;
        if (!_reactiveMode)
        {
            _reactiveMode = true;
            _jamCuePending = true;
            _musicalSeconds = 0;
            _musicalOnsets = 0;
            _jamCueExpires = double.PositiveInfinity;
            _jamExpression = _changeExpression = listening.Expression;
            _jamDynamics = _changeDynamics = listening.Dynamics;
            _jamDrive = _changeDrive = drive;
            if (_action == CompanionAction.Jam)
            {
                _hasJoined = true;
                _jamCuePending = false;
                _reactiveNextJam = Math.Max(_reactiveNextJam, Math.Max(_nextJam, _nextExpressiveJam));
            }
        }

        _lostMusicalEvidence = reliable ? 0 : _lostMusicalEvidence + dt;
        if (reliable && newOnset && !sustained)
        {
            _lastMusicalOnset = _time;
            _musicalOnsets++;
        }
        bool recentArticulation = _time - _lastMusicalOnset < .8;
        double target = !active || !reliable || sustained ? 0
            : animated ? .72 + .25 * drive
            : flowing ? .35 + .4 * drive
            : steadyRhythm ? .3 + .38 * drive
            : recentArticulation && drive >= .35 ? .2 + .6 * drive : 0;
        if (target > 0)
        {
            target += listening.Dynamics == DynamicContour.Building ? .1
                : listening.Dynamics == DynamicContour.Easing ? -.18 : 0;
            target *= curious ? 1 : .95;
        }
        target = Math.Clamp(target, 0, 1);
        bool section = reliable && listening.SectionLift >= .35 && listening.SectionCue > _lastSectionCue;
        if (section)
        {
            _lastSectionCue = listening.SectionCue;
            _sectionJoinUntil = _time + 2;
        }
        bool prominentPlucks = reliable && listening.PluckedPresence >= .6 && recentArticulation;
        _pluckReleaseSeconds = prominentPlucks ? 0 : _pluckReleaseSeconds + dt;
        if (_pluckReleaseSeconds >= 1) _pluckHighlighted = false;
        if (prominentPlucks && !_pluckHighlighted)
        {
            _pluckHighlighted = true;
            _sectionJoinUntil = _time + 3;
        }
        if (prominentPlucks || reliable && listening.SectionLift >= .35) target = Math.Max(target, curious ? .85 : .75);
        double responseSeconds = target > _engagement ? curious ? .2 : .42 : .3;
        _engagement += (target - _engagement) * (1 - Math.Exp(-dt / responseSeconds));
        if (_engagement < .001) _engagement = 0;

        // Joining requires several actual articulations, not just an elapsed listening timer.
        // After a joined phrase, unchanged sound cannot keep starting new guitar phrases.
        _musicalSeconds = active && reliable && target >= .5 && recentArticulation
            ? _musicalSeconds + dt : Math.Max(0, _musicalSeconds - dt * 2);
        bool changed = reliable && (listening.Expression != _jamExpression ||
            listening.Dynamics != _jamDynamics || Math.Abs(drive - _jamDrive) >= .25);
        bool sameCandidate = listening.Expression == _changeExpression && listening.Dynamics == _changeDynamics &&
            Math.Abs(drive - _changeDrive) < .12;
        if (!sameCandidate)
        {
            _changeExpression = listening.Expression;
            _changeDynamics = listening.Dynamics;
            _changeDrive = drive;
            _jamVariationSeconds = 0;
        }
        _jamVariationSeconds = changed ? _jamVariationSeconds + dt : 0;
        if (_jamVariationSeconds >= .4)
        {
            _jamExpression = listening.Expression;
            _jamDynamics = listening.Dynamics;
            _jamDrive = drive;
            _jamVariationSeconds = 0;
            if (target >= .5)
            {
                _jamCuePending = true;
                _jamCueExpires = _hasJoined ? _time + 2 : double.PositiveInfinity;
            }
        }
        if (_time > _jamCueExpires) _jamCuePending = false;
        if (_harmonyChanged && target >= .5)
        {
            _jamCuePending = true;
            _jamCueExpires = _time + 1.5;
        }

        double age = _time - _started;
        bool released = _action == CompanionAction.Settle && !active ||
            sustained && _heldSeconds >= .15 || !active && _quietSeconds >= .35 ||
            _lostMusicalEvidence >= .4;
        double engageThreshold = curious ? .55 : .65;
        double engageRelease = curious ? .48 : .57;
        var desired = released || _engagement < (_action is CompanionAction.Groove or CompanionAction.Engage ? .18 : .24)
            ? CompanionAction.Listen
            : _engagement >= (_action == CompanionAction.Engage ? engageRelease : engageThreshold)
                ? CompanionAction.Engage : CompanionAction.Groove;

        if (_action == CompanionAction.Jam)
        {
            // A confirmed harmonic turn can carry an already joined phrase forward.
            // A repeated label or elapsed cooldown never renews it; hard bounds remain.
            if (_harmonyChanged && recentArticulation && target >= .5 && age >= .5)
                _duration = Math.Min(curious ? 12 : 9.6,
                    Math.Max(_duration, age + _harmonicSpan + (curious ? .5 : .2)));
            if (!allowJam || !active || sustained || _lostMusicalEvidence >= .4 || target < .35)
                Start(CompanionAction.Settle, curious ? 1.15 : .8);
            else if (age >= _duration)
                Start(desired); // Keep listening to the ongoing phrase after putting the guitar down.
            return true;
        }

        if (desired is CompanionAction.Groove or CompanionAction.Engage)
        {
            // An initial ear tilt is brief; it never locks out a clearly changing phrase.
            if (_action == CompanionAction.Notice && age < (curious ? .3 : .55)) return true;
            if (_action != desired) Start(desired);
            bool join = allowJam && newOnset && _jamCuePending && _time >= _reactiveNextJam &&
                _musicalSeconds >= (curious ? 5 : 8) && _musicalOnsets >= (curious ? 12 : 20) &&
                _engagement >= engageThreshold && _time - _started >= .5;
            bool musicalHighlight = allowJam && newOnset && _time >= _nextSectionJoin &&
                _time < _sectionJoinUntil &&
                _musicalOnsets >= 4 && _engagement >= engageThreshold;
            join |= musicalHighlight;
            if (join)
            {
                Start(CompanionAction.Jam, curious ? 6.4 : 4.8);
                _reactiveNextJam = _time + Between(40, 65);
                _nextSectionJoin = _time + 12;
                _sectionJoinUntil = 0;
                _hasJoined = true;
                _jamCuePending = false;
                _jamVariationSeconds = 0;
                _jamExpression = _changeExpression = listening.Expression;
                _jamDynamics = _changeDynamics = listening.Dynamics;
                _jamDrive = _changeDrive = drive;
            }
            return true;
        }

        if (_action is CompanionAction.Groove or CompanionAction.Engage)
            Start(CompanionAction.Settle, curious ? 1.15 : .8);
        else if (_action == CompanionAction.Settle)
        {
            if (age >= _duration && (active || !attending))
            {
                Start(attending ? CompanionAction.Listen : _quietSeconds >= 30 ? CompanionAction.Rest : CompanionAction.Idle);
                if (!active || _lostMusicalEvidence >= .65) ClearMusicalResponse();
            }
        }
        else if (active && _action is CompanionAction.Idle or CompanionAction.Rest)
        {
            Start(CompanionAction.Notice, curious ? 1.25 : .8);
            _nextNotice = _time + 8;
        }
        else if (_action == CompanionAction.Notice && age >= _duration || _action == CompanionAction.Groom && active)
            Start(CompanionAction.Listen);
        else if (_action == CompanionAction.Listen)
        {
            if (!attending) Start(CompanionAction.Settle, curious ? 1.15 : .8);
            else if (reliable && listening.Harmony.IsKnown && _time < _harmonyNoticeUntil &&
                _time >= _nextHarmonyNotice && age >= (curious ? .3 : .6))
            {
                Start(CompanionAction.Notice, curious ? 1.25 : .8);
                _harmonyNoticeUntil = 0;
                _nextHarmonyNotice = _time + (curious ? 3 : 5);
            }
            else if (_toneNoticePending && _time >= _nextToneNotice && _time >= _nextNotice && age >= 1.5)
            {
                Start(CompanionAction.Notice, curious ? 1.25 : .8);
                _toneNoticePending = false;
                _nextToneNotice = _time + (curious ? 16 : 24);
                _nextNotice = _time + 8;
            }
            else if (_lostMusicalEvidence >= .65) ClearMusicalResponse();
        }
        return true;
    }

    private double ReactiveWeight()
    {
        double age = _time - _started;
        return _action switch
        {
            CompanionAction.Settle => 1 - Smooth(age / _duration),
            // A change of musical pose is not a new phrase. Keep the body's response
            // continuous when picking up/putting down the guitar or easing into a groove.
            // Silence and held notes still take the existing Settle path above.
            CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam =>
                _movementEntryWeight + (1 - _movementEntryWeight) * Smooth(age / .25),
            CompanionAction.Notice or CompanionAction.Groom =>
                Math.Min(Smooth(age / .25), Smooth((_duration - age) / .4)),
            _ => 0
        };
    }

    private void ClearMusicalResponse()
    {
        _reactiveMode = _jamCuePending = _hasJoined = false;
        _movementEntryWeight = _settleEntryWeight = 0;
        _engagement = _musicalSeconds = _lostMusicalEvidence = _jamVariationSeconds = 0;
        _musicalOnsets = 0;
        _lastMusicalOnset = -100;
    }

    private void TrackTone(ToneColor tone, double dt, bool curious)
    {
        if (tone == ToneColor.Unknown || !Enum.IsDefined(tone))
        {
            _candidateTone = ToneColor.Unknown;
            _toneSeconds = 0;
            _toneNoticePending = false;
            return;
        }
        if (tone != _candidateTone) { _candidateTone = tone; _toneSeconds = 0; _toneNoticePending = false; }
        _toneSeconds += dt;
        if (_toneSeconds < (curious ? 1.2 : 2) || tone == _heardTone) return;
        // The first tone supplies context; a later stable change may earn one small ear tilt.
        _toneNoticePending = _heardTone != ToneColor.Unknown;
        _heardTone = tone;
    }

    private void Start(CompanionAction action, double duration = 0)
    {
        // A brief evidence gap can begin settling and recover on the next tick.
        // Carry only the still-visible fraction of the previous musical movement;
        // a completed settle, quiet or reset must start gently from zero again.
        _movementEntryWeight = _reactiveMode && IsMusicalMovement(action)
            ? IsMusicalMovement(_action) ? Math.Clamp(Current.Weight, 0, 1)
                : _action == CompanionAction.Settle ? _settleEntryWeight * Math.Clamp(Current.Weight, 0, 1) : 0
            : 0;
        if (action == CompanionAction.Settle)
            _settleEntryWeight = _reactiveMode && IsMusicalMovement(_action) ? Math.Clamp(Current.Weight, 0, 1) : 0;
        _action = action; _started = _time; _duration = duration;
    }
    private static bool IsMusicalMovement(CompanionAction action) =>
        action is CompanionAction.Groove or CompanionAction.Engage or CompanionAction.Jam;
    private CompanionPose Pose(double weight) => new(_action, _action switch
    {
        CompanionAction.Notice or CompanionAction.Listen or CompanionAction.Groove or CompanionAction.Settle => CompanionMood.Listening,
        CompanionAction.Engage or CompanionAction.Jam => CompanionMood.Excited,
        CompanionAction.Rest => CompanionMood.Resting,
        _ => CompanionMood.Waiting
    }, weight) { Engagement = _engagement, HasMusicalEvidence = _reactiveMode,
        Gaze = _innerState.Gaze, Lean = _innerState.Lean, Tail = _innerState.Tail,
        Eagerness = _innerState.Eagerness };
    private double Between(double low, double high) => low + _random.NextDouble() * (high - low);
    private static double Smooth(double value) { value = Math.Clamp(value, 0, 1); return value * value * (3 - 2 * value); }
}
