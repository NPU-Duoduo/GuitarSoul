namespace GuitarSoul.Core.Audio;

/// <summary>Bounded PCM history in whole sample frames. The owner synchronizes access.</summary>
public sealed class AudioRingBuffer
{
    private readonly byte[] _data;
    private readonly int _blockAlign;
    private int _writeOffset;
    public int Count { get; private set; }

    public AudioRingBuffer(int capacityFrames, int blockAlign)
    {
        if (capacityFrames <= 0 || blockAlign <= 0 || (long)capacityFrames * blockAlign > 128 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(capacityFrames), "Audio history must fit within 128 MiB.");
        _blockAlign = blockAlign;
        _data = new byte[capacityFrames * blockAlign];
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.Length % _blockAlign != 0) throw new ArgumentException("Incomplete sample frame.", nameof(data));
        if (data.Length >= _data.Length)
        {
            data[^_data.Length..].CopyTo(_data);
            Count = _data.Length;
            _writeOffset = 0;
            return;
        }
        int first = Math.Min(data.Length, _data.Length - _writeOffset);
        data[..first].CopyTo(_data.AsSpan(_writeOffset));
        data[first..].CopyTo(_data);
        _writeOffset = (_writeOffset + data.Length) % _data.Length;
        Count = Math.Min(_data.Length, Count + data.Length);
    }

    public byte[] Snapshot()
    {
        var result = new byte[Count];
        int start = (_writeOffset - Count + _data.Length) % _data.Length;
        int first = Math.Min(Count, _data.Length - start);
        _data.AsSpan(start, first).CopyTo(result);
        _data.AsSpan(0, Count - first).CopyTo(result.AsSpan(first));
        return result;
    }

    public void Clear()
    {
        Array.Clear(_data);
        _writeOffset = Count = 0;
    }
}
