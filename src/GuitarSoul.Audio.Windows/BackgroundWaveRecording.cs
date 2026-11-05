using System.Threading.Channels;
using NAudio.Wave;

namespace GuitarSoul.Audio.Windows;

/// <summary>Only copies/enqueues on capture thread; all WAV writing/finalization happens on a worker.</summary>
internal sealed class BackgroundWaveRecording
{
    private readonly Channel<byte[]> _queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(64)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = true,
        AllowSynchronousContinuations = false
    });
    private int _accepting = 1;
    private Exception? _error;
    public string Path { get; }
    public bool IsAccepting => Volatile.Read(ref _accepting) == 1;
    public Task<RecordingCompletedEventArgs> Completion { get; }

    public BackgroundWaveRecording(string path, WaveFormat format)
    {
        Path = System.IO.Path.GetFullPath(path);
        // CreateNew protects existing takes; UI should use a unique file name.
        var stream = new FileStream(Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        WaveFileWriter writer;
        try { writer = new WaveFileWriter(stream, format); }
        catch { stream.Dispose(); throw; }
        Completion = Task.Run(async () =>
        {
            try
            {
                await foreach (var block in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
                    writer.Write(block, 0, block.Length);
            }
            catch (Exception ex) { Interlocked.CompareExchange(ref _error, ex, null); }
            finally
            {
                Complete();
                try { writer.Dispose(); }
                catch (Exception ex) { Interlocked.CompareExchange(ref _error, ex, null); }
            }
            return new RecordingCompletedEventArgs(Path, _error);
        });
    }

    public void TryEnqueue(byte[] block)
    {
        if (!IsAccepting) return;
        if (!_queue.Writer.TryWrite(block) && IsAccepting)
        {
            Interlocked.CompareExchange(ref _error,
                new IOException("磁盘写入未能跟上录音，录音已停止。此 WAV 可能不完整；请检查可用空间及磁盘速度。"), null);
            Complete();
        }
    }

    public void Complete()
    {
        if (Interlocked.Exchange(ref _accepting, 0) == 1) _queue.Writer.TryComplete();
    }
}
