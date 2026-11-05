namespace GuitarSoul.App;

/// <summary>Checks digital rail density and flat tops before showing an overload hint.
/// Presentation only: the existing conservative eligibility gate remains independent.</summary>
internal sealed class InputOverloadWarning
{
    private double _clippedSeconds, _clearSeconds;
    private int _sampleRate, _windowSamples, _railSamples, _flatSamples, _run;
    private float _previous;
    public bool IsVisible { get; private set; }
    public bool HasFlatTop { get; private set; }
    public double RailFraction { get; private set; }

    public void Observe(ReadOnlySpan<float> samples, int sampleRate)
    {
        if (sampleRate < 8000 || sampleRate > 384000) { Reset(); return; }
        if (_sampleRate != sampleRate) { Reset(); _sampleRate = sampleRate; }
        // One 16-bit LSB from the rail; 0.999 was merely near full scale.
        const float rail = 1f - 1f / 32768;
        foreach (float sample in samples)
        {
            bool atRail = float.IsFinite(sample) && Math.Abs(sample) >= rail;
            if (atRail)
            {
                _railSamples++;
                _run = _run > 0 && Math.Sign(sample) == Math.Sign(_previous) &&
                    Math.Abs(sample - _previous) <= 1f / 32768 ? _run + 1 : 1;
                if (_run == 3) _flatSamples += 3;
                else if (_run > 3) _flatSamples++;
            }
            else _run = 0;
            _previous = sample;
            if (++_windowSamples < sampleRate / 10) continue;
            RailFraction = (double)_railSamples / _windowSamples;
            HasFlatTop = RailFraction >= .01 && (double)_flatSamples / _windowSamples >= .005;
            Advance(HasFlatTop, (double)_windowSamples / sampleRate);
            _windowSamples = _railSamples = _flatSamples = _run = 0;
        }
    }

    private void Advance(bool clipped, double seconds)
    {
        if (clipped)
        {
            _clearSeconds = 0;
            _clippedSeconds += seconds;
            if (_clippedSeconds >= .3 - 1e-9) IsVisible = true;
        }
        else
        {
            _clippedSeconds = Math.Max(0, _clippedSeconds - seconds * 2);
            _clearSeconds += seconds;
            if (_clearSeconds >= .6 - 1e-9) { IsVisible = false; _clippedSeconds = 0; }
        }
    }

    public void Reset()
    {
        _clippedSeconds = _clearSeconds = RailFraction = 0;
        _sampleRate = _windowSamples = _railSamples = _flatSamples = _run = 0;
        _previous = 0; IsVisible = HasFlatTop = false;
    }
}
