using System.Buffers.Binary;
using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

internal static class PcmSamples
{
    public static void Validate(WaveFormat format)
    {
        if (format.Channels < 1 || format.SampleRate < 1 ||
            !(format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32 ||
              format.Encoding == WaveFormatEncoding.Pcm && format.BitsPerSample is 8 or 16 or 24 or 32))
            throw new NotSupportedException($"暂不支持这个输入格式：{format}。请在 Windows 声音设置中选择 PCM 或 32-bit float。");
    }

    public static AudioSamplesEventArgs Analyze(byte[] buffer, int length, WaveFormat format, int selectedChannel)
    {
        int frames = length / format.BlockAlign;
        int sampleBytes = format.BitsPerSample / 8;
        int channel = selectedChannel;
        if (channel < 0)
        {
            // Do not sum L/R: phase-inverted stereo guitar signals can cancel completely.
            double bestEnergy = -1;
            for (int c = 0; c < format.Channels; c++)
            {
                double energy = 0;
                for (int f = 0; f < frames; f++)
                {
                    float sample = Read(buffer.AsSpan(f * format.BlockAlign + c * sampleBytes), format);
                    energy += sample * sample;
                }
                if (energy > bestEnergy) { bestEnergy = energy; channel = c; }
            }
        }
        var mono = new float[frames];
        double sum = 0;
        float peak = 0;
        for (int f = 0; f < frames; f++)
        {
            float value = Read(buffer.AsSpan(f * format.BlockAlign + channel * sampleBytes), format);
            mono[f] = value;
            sum += value * value;
            peak = Math.Max(peak, Math.Abs(value));
        }
        return new(mono, format.SampleRate, peak, frames == 0 ? 0 : (float)Math.Sqrt(sum / frames));
    }

    private static float Read(ReadOnlySpan<byte> bytes, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            float value = BinaryPrimitives.ReadSingleLittleEndian(bytes);
            return float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0;
        }
        return format.BitsPerSample switch
        {
            8 => (bytes[0] - 128) / 128f,
            16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
            24 => ((bytes[0] | bytes[1] << 8 | bytes[2] << 16) << 8 >> 8) / 8388608f,
            32 => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f,
            _ => 0
        };
    }

    public static byte[] CopyRecordingData(byte[] buffer, int length, WaveFormat format, int channel)
    {
        int frames = length / format.BlockAlign;
        int sampleBytes = format.BitsPerSample / 8;
        if (channel < 0) return buffer.AsSpan(0, frames * format.BlockAlign).ToArray();
        var output = new byte[frames * sampleBytes];
        for (int f = 0; f < frames; f++)
            buffer.AsSpan(f * format.BlockAlign + channel * sampleBytes, sampleBytes)
                .CopyTo(output.AsSpan(f * sampleBytes));
        return output;
    }
}
