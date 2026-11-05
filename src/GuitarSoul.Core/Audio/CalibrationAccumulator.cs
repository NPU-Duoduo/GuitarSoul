namespace GuitarSoul.Core.Audio;

public enum CalibrationStatus
{
    Collecting,
    Complete,
    Rejected
}

public sealed record CalibrationResult(
    CalibrationStatus Status,
    double Progress,
    double? SuggestedNoiseFloorDb,
    double? SuggestedActivityThresholdDb,
    string Message);

/// <summary>
/// Collects a minimum amount of actual input audio, rather than elapsed wall time.
/// Keep guitar strings muted and stop backing playback during calibration.
/// Loud or strongly changing input is rejected; this cannot distinguish very quiet sustained
/// music from steady noise. A successful result is a recommendation, never proof of silence.
/// </summary>
public sealed class CalibrationAccumulator
{
    private const double MaximumQuietRmsDb = -40;
    private const double MaximumQuietPeakDb = -24;
    private readonly long _requiredFrames;
    private readonly int _windowFrames;
    private readonly int _channels;
    private long _frames;
    private long _currentWindowFrames;
    private double _sumSquares;
    private double _windowSumSquares;
    private double _lowestWindowDb = double.PositiveInfinity;
    private double _highestWindowDb = -120;

    public CalibrationAccumulator(int sampleRate, int channels = 1, double requiredSeconds = 3)
    {
        if (sampleRate is < 8000 or > 384000)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        SignalAnalyzer.ValidateChannels(0, channels);
        if (!double.IsFinite(requiredSeconds) || requiredSeconds is < 0.1 or > 30)
            throw new ArgumentOutOfRangeException(nameof(requiredSeconds));
        _channels = channels;
        _requiredFrames = (long)Math.Ceiling(sampleRate * requiredSeconds);
        _windowFrames = Math.Max(1, sampleRate / 20); // 50 ms, independent of capture callback size.
    }

    public CalibrationResult Result { get; private set; } = Collecting(0);

    public CalibrationResult AddSamples(ReadOnlySpan<float> interleavedSamples)
    {
        SignalAnalyzer.ValidateChannels(interleavedSamples.Length, _channels);
        if (Result.Status != CalibrationStatus.Collecting)
            return Result;

        for (var offset = 0; offset < interleavedSamples.Length && _frames < _requiredFrames; offset += _channels)
        {
            double frameEnergy = 0;
            for (var channel = 0; channel < _channels; channel++)
            {
                var sample = interleavedSamples[offset + channel];
                if (!float.IsFinite(sample))
                    return Reject("输入包含无效采样，请重新连接设备后再校准。");
                var magnitude = Math.Abs((double)sample);
                if (SignalAnalyzer.ToDb(magnitude) >= MaximumQuietPeakDb)
                    return Reject("检测到明显声音。请停止伴奏、轻按琴弦止音后重新校准。");
                frameEnergy += magnitude * magnitude;
            }

            _frames++;
            _currentWindowFrames++;
            _sumSquares += frameEnergy;
            _windowSumSquares += frameEnergy;

            if (_currentWindowFrames >= _windowFrames || _frames == _requiredFrames)
            {
                var windowRmsDb = SignalAnalyzer.ToDb(Math.Sqrt(_windowSumSquares / (_currentWindowFrames * _channels)));
                _lowestWindowDb = Math.Min(_lowestWindowDb, windowRmsDb);
                _highestWindowDb = Math.Max(_highestWindowDb, windowRmsDb);
                if (windowRmsDb >= MaximumQuietRmsDb)
                    return Reject("输入持续过响。请停止演奏与伴奏，并检查增益后重新校准。");
                if (_highestWindowDb > -58 && _highestWindowDb - _lowestWindowDb > 14)
                    return Reject("检测到明显音量变化。请等待琴弦与效果尾音安静后重新校准。");
                _currentWindowFrames = 0;
                _windowSumSquares = 0;
            }
        }

        if (_frames < _requiredFrames)
            return Result = Collecting((double)_frames / _requiredFrames);

        var observedDb = SignalAnalyzer.ToDb(Math.Sqrt(_sumSquares / (_frames * _channels)));
        // Avoid a near-zero digital stream creating an unusably sensitive threshold.
        var noiseFloor = Math.Clamp(observedDb, -90, -40);
        var threshold = Math.Clamp(Math.Max(observedDb, _highestWindowDb) + 10, -60, -30);
        return Result = new CalibrationResult(CalibrationStatus.Complete, 1, noiseFloor, threshold,
            "已取得安静输入样本。建议值用于活动检测，请轻拨琴弦检查响应。");
    }

    public void Reset()
    {
        _frames = _currentWindowFrames = 0;
        _sumSquares = _windowSumSquares = 0;
        _lowestWindowDb = double.PositiveInfinity;
        _highestWindowDb = -120;
        Result = Collecting(0);
    }

    private CalibrationResult Reject(string message) => Result = new(CalibrationStatus.Rejected,
        (double)_frames / _requiredFrames, null, null, message);

    private static CalibrationResult Collecting(double progress) => new(CalibrationStatus.Collecting,
        progress, null, null, "校准中，请停止伴奏并轻按琴弦保持安静。");
}
