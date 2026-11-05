using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Language;

internal static class LocalLanguageChecks
{
    private static readonly CatSpeechRequest Request = new(
        new ListeningFeatures(ToneColor.Warm, PlayingExpression.Sustained, .86),
        ExpressionStyle.Reserved, "我在听这个音。");

    public static void GeneratesOnlyThroughTheOutputGuard()
    {
        using var fixture = new LanguageFixture(server => server.Reply = "这个音暖暖的，我还在听。");
        var language = fixture.Create();
        try
        {
            Equal("这个音暖暖的，我还在听。", Generate(language, Request));
            Equal(LocalLanguageState.Ready, language.State);
            True(language.StatusText.Contains("就绪", StringComparison.Ordinal));
            Equal(1, fixture.Servers.Count);
        }
        finally { Dispose(language); }
    }

    public static void RefusedSentenceFallsBackWithoutKillingTheServer()
    {
        using var fixture = new LanguageFixture(server => server.Reply = "这个过载失真太摇滚了。");
        var language = fixture.Create();
        try
        {
            True(Generate(language, Request) is null);
            True(language.LastGenerationDetail?.StartsWith("Output guard rejected", StringComparison.Ordinal) == true);
            Equal(LocalLanguageState.Ready, language.State);
            Equal(2, fixture.Requests); // one guarded retry, then fallback
        }
        finally { Dispose(language); }
    }

    public static void DeadServerStopsClaimingReady()
    {
        using var fixture = new LanguageFixture(server => server.DieOnCompletions = true);
        var language = fixture.Create();
        try
        {
            True(Generate(language, Request) is null);
            Equal(LocalLanguageState.Failed, language.State);
            True(!language.StatusText.Contains("就绪", StringComparison.Ordinal));
        }
        finally { Dispose(language); }
    }

    public static void StartupTimeoutIsReportedAsFailure()
    {
        using var fixture = new LanguageFixture(server => server.NeverReady = true);
        var language = fixture.Create(startup: TimeSpan.FromMilliseconds(400));
        try
        {
            language.WarmUpAsync().GetAwaiter().GetResult();
            Equal(LocalLanguageState.Failed, language.State);
        }
        finally { Dispose(language); }
    }

    public static void IdleServerIsReleasedAndRestarted()
    {
        using var fixture = new LanguageFixture(server => server.Reply = "这个音暖暖的，我还在听。");
        var language = fixture.Create(idle: TimeSpan.FromMilliseconds(300));
        try
        {
            True(Generate(language, Request) is not null);
            Equal(1, fixture.Servers.Count);
            Wait(() => language.State == LocalLanguageState.Stopped);
            True(fixture.Servers[0].Killed);
            True(Generate(language, Request) is not null);
            Equal(2, fixture.Servers.Count);
            Equal(LocalLanguageState.Ready, language.State);
        }
        finally { Dispose(language); }
    }

    private static string? Generate(LocalQwenLanguage language, CatSpeechRequest request) =>
        language.GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult();

    private static void Dispose(LocalQwenLanguage language) =>
        language.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static void Wait(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            Thread.Sleep(25);
        }
        throw new Exception("Timed out waiting for the expected language state.");
    }

    private static void True(bool condition)
    {
        if (!condition) throw new Exception("Expected condition to be true.");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"Expected {expected}, got {actual}.");
    }
}

internal sealed class LanguageFixture : IDisposable
{
    private readonly string _root;

    public LanguageFixture(Action<FakeLanguageServerLauncher> configure)
    {
        configure(Launcher);
        _root = Path.Combine(Path.GetTempPath(), "guitar-soul-language-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "runtime"));
        File.WriteAllText(Path.Combine(_root, "runtime", "llama-server.exe"), "");
        File.WriteAllText(Path.Combine(_root, "Qwen3-1.7B-Q4_K_M.gguf"), "");
        File.WriteAllText(Path.Combine(_root, "manifest.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            modelName = "Qwen3-1.7B",
            quantization = "Q4_K_M",
            runtimePath = "runtime/llama-server.exe",
            modelPath = "Qwen3-1.7B-Q4_K_M.gguf",
            modelSource = "synthetic",
            modelRevision = "synthetic",
            modelSha256 = "synthetic",
            runtimeVersion = "synthetic"
        }));
    }

    public FakeLanguageServerLauncher Launcher { get; } = new();
    public IReadOnlyList<FakeLanguageServer> Servers => Launcher.Servers;
    public int Requests => Launcher.Servers.Sum(server => server.Requests);

    public LocalQwenLanguage Create(TimeSpan? idle = null, TimeSpan? startup = null) =>
        new(_root, Launcher, new LanguageServerOptions(
            startup ?? TimeSpan.FromSeconds(3), idle ?? TimeSpan.FromMinutes(5)));

    public void Dispose()
    {
        foreach (var server in Launcher.Servers) server.Dispose();
        try { Directory.Delete(_root, true); }
        catch (IOException) { }
    }
}

internal sealed class FakeLanguageServerLauncher : ILanguageServerLauncher
{
    public List<FakeLanguageServer> Servers { get; } = new();
    public string Reply { get; set; } = "这个音暖暖的，我还在听。";
    public bool DieOnCompletions { get; set; }
    public bool NeverReady { get; set; }

    public ILanguageServerProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        int port = 0;
        for (int i = 0; i + 1 < arguments.Count; i++)
            if (arguments[i] == "--port") port = int.Parse(arguments[i + 1]);
        if (port == 0) throw new Exception("The language layer did not name a loopback port.");
        var server = new FakeLanguageServer(port)
        {
            Reply = Reply,
            DieOnCompletions = DieOnCompletions,
            NeverReady = NeverReady
        };
        Servers.Add(server);
        return server;
    }
}

internal sealed class FakeLanguageServer : ILanguageServerProcess
{
    private readonly int _port;
    private readonly CancellationTokenSource _lifetime = new();
    private TcpListener? _listener;
    private volatile bool _exited;
    private bool _disposed;

    public FakeLanguageServer(int port) => _port = port;

    public string Reply { get; init; } = "";
    public bool DieOnCompletions { get; init; }
    public bool NeverReady { get; init; }
    public bool Killed { get; private set; }
    public int Requests { get; private set; }
    public event Action<string?>? OutputLine;

    public bool HasExited => _exited;

    public void Start()
    {
        OutputLine?.Invoke("fake language server asked for port " + _port);
        if (NeverReady) return;
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _ = Task.Run(() => AcceptAsync(_lifetime.Token));
    }

    public void Kill()
    {
        Killed = true;
        Stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        _lifetime.Dispose();
    }

    private void Stop()
    {
        _exited = true;
        try { _lifetime.Cancel(); } catch (ObjectDisposedException) { }
        try { _listener?.Stop(); } catch (SocketException) { }
    }

    private async Task AcceptAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false); }
            catch (Exception) { return; }
            _ = Task.Run(() => HandleAsync(client, token));
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken token)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadAsync(stream, token).ConfigureAwait(false);
                if (request is null) return;
                var (path, _) = request.Value;
                if (path.Contains("/health", StringComparison.Ordinal))
                {
                    await WriteAsync(stream, "{}", "application/json").ConfigureAwait(false);
                    return;
                }
                if (!path.Contains("/v1/chat/completions", StringComparison.Ordinal)) return;
                Requests++;
                if (DieOnCompletions)
                {
                    Stop();
                    return;
                }
                var payload = JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { role = "assistant", content = Reply } } }
                });
                await WriteAsync(stream, payload, "application/json").ConfigureAwait(false);
            }
            catch (Exception) { }
        }
    }

    private static async Task<(string Path, string Body)?> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        var received = new List<byte>();
        var chunk = new byte[2048];
        int headerEnd = -1;
        while (headerEnd < 0)
        {
            int read = await stream.ReadAsync(chunk, token).ConfigureAwait(false);
            if (read <= 0) return null;
            received.AddRange(chunk.AsSpan(0, read).ToArray());
            headerEnd = IndexOfHeaderEnd(received);
            if (received.Count > 128 * 1024) return null;
        }
        var lines = Encoding.ASCII.GetString(received.ToArray(), 0, headerEnd).Split("\r\n");
        var parts = lines[0].Split(' ');
        var path = parts.Length > 1 ? parts[1] : "/";
        int length = 0;
        foreach (var line in lines)
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line["Content-Length:".Length..].Trim(), out length);
        var body = received.Skip(headerEnd + 4).ToList();
        while (body.Count < length)
        {
            int read = await stream.ReadAsync(chunk, token).ConfigureAwait(false);
            if (read <= 0) break;
            body.AddRange(chunk.AsSpan(0, read).ToArray());
        }
        return (path, Encoding.UTF8.GetString(body.ToArray(), 0, Math.Min(length, body.Count)));
    }

    private static int IndexOfHeaderEnd(List<byte> data)
    {
        for (int i = 0; i + 3 < data.Count; i++)
            if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10) return i;
        return -1;
    }

    private static async Task WriteAsync(NetworkStream stream, string body, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(head).ConfigureAwait(false);
        await stream.WriteAsync(bytes).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }
}
