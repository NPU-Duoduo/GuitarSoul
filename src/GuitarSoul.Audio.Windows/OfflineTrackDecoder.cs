using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

public sealed record DecodedMonoTrack(float[] Samples, int SampleRate);

/// <summary>Bounded local-file decoding for deterministic track-arc analysis.</summary>
public static class OfflineTrackDecoder
{
    public static Task<DecodedMonoTrack> DecodeAsync(string path, CancellationToken cancellation = default) =>
        Task.Run(() => Decode(path, cancellation), cancellation);

    private static DecodedMonoTrack Decode(string path, CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var reader = new AudioFileReader(path);
        int channels = reader.WaveFormat.Channels;
        int sampleRate = reader.WaveFormat.SampleRate;
        if (channels < 1 || sampleRate is < 8000 or > 192000)
            throw new NotSupportedException("伴奏解码格式不适合结构预分析。");
        if (reader.TotalTime <= TimeSpan.Zero || reader.TotalTime > TimeSpan.FromMinutes(15))
            throw new NotSupportedException("结构预分析仅支持 15 分钟以内的伴奏。");

        var buffer = new float[Math.Max(4096, channels * 4096)];
        var energy = new double[channels];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            int frames = read / channels;
            for (int frame = 0; frame < frames; frame++)
            for (int channel = 0; channel < channels; channel++)
            {
                float sample = buffer[frame * channels + channel];
                if (float.IsFinite(sample)) energy[channel] += sample * (double)sample;
            }
        }

        int selected = 0;
        for (int channel = 1; channel < channels; channel++)
            if (energy[channel] > energy[selected]) selected = channel;

        reader.Position = 0;
        long expected = (long)Math.Ceiling(reader.TotalTime.TotalSeconds * sampleRate) + sampleRate;
        if (expected > int.MaxValue) throw new NotSupportedException("伴奏过长，无法在本机完成预分析。");
        var mono = new float[(int)expected];
        int written = 0;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            int frames = read / channels;
            if (written + frames > mono.Length) Array.Resize(ref mono, Math.Min(int.MaxValue, written + frames + sampleRate));
            for (int frame = 0; frame < frames; frame++)
            {
                float sample = buffer[frame * channels + selected];
                mono[written++] = float.IsFinite(sample) ? Math.Clamp(sample, -1f, 1f) : 0;
            }
        }
        Array.Resize(ref mono, written);
        return new(mono, sampleRate);
    }
}
