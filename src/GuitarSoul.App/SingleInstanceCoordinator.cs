using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace GuitarSoul.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;
    public bool IsPrimary { get; }
    public string? ListenerError { get; private set; }

    public SingleInstanceCoordinator(string version)
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity + ":" + Process.GetCurrentProcess().SessionId + ":" + version)))[..24];
        _pipeName = "GuitarSoul-" + key;
        _mutex = new Mutex(true, "Local\\" + _pipeName, out var created);
        IsPrimary = created;
    }

    public void Listen(Action activate)
    {
        if (!IsPrimary || _listener is not null) return;
        _listener = Task.Run(async () =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_cancellation.Token);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token);
                    request.CancelAfter(TimeSpan.FromSeconds(2));
                    var buffer = new byte[1];
                    if (await pipe.ReadAsync(buffer, request.Token) == 1 && buffer[0] == 1)
                    {
                        activate();
                        await pipe.WriteAsync(new byte[] { 1 }, request.Token);
                        await pipe.FlushAsync(request.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException exception) { ListenerError = exception.Message; }
                catch (UnauthorizedAccessException exception) { ListenerError = exception.Message; return; }
            }
        });
    }

    public bool TryActivate()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            pipe.ConnectAsync(timeout.Token).GetAwaiter().GetResult();
            pipe.WriteAsync(new byte[] { 1 }, timeout.Token).AsTask().GetAwaiter().GetResult();
            pipe.FlushAsync(timeout.Token).GetAwaiter().GetResult();
            var response = new byte[1];
            return pipe.ReadAsync(response, timeout.Token).AsTask().GetAwaiter().GetResult() == 1 && response[0] == 1;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or UnauthorizedAccessException)
        { return false; }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        // The listener owns its pipe; cancellation unblocks it without waiting on the UI thread.
        if (IsPrimary) _mutex.ReleaseMutex();
        _mutex.Dispose();
    }

    internal static void Verify(string path)
    {
        var scope = "verification-" + Guid.NewGuid().ToString("N");
        using var first = new SingleInstanceCoordinator(scope);
        using var activated = new ManualResetEventSlim();
        first.Listen(activated.Set);
        using var second = new SingleInstanceCoordinator(scope);
        using var otherVersion = new SingleInstanceCoordinator(scope + "-other");
        if (!first.IsPrimary || second.IsPrimary || !otherVersion.IsPrimary || !second.TryActivate() || !activated.Wait(1000))
            throw new InvalidOperationException("Single-instance isolation or activation failed.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, "Primary ownership, duplicate activation/acknowledgment, and version isolation passed. No audio capture or user app shutdown.");
    }
}
