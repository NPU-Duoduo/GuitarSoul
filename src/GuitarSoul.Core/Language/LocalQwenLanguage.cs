using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;

namespace GuitarSoul.Core.Language;

public enum LocalLanguageState { NotInstalled, Stopped, Starting, Ready, Failed }

/// <summary>
/// The single process a local language server occupies. It exists as a seam so the
/// lifecycle (start, crash, idle unload) can be exercised without a real model.
/// </summary>
public interface ILanguageServerProcess : IDisposable
{
    bool HasExited { get; }
    event Action<string?>? OutputLine;
    void Start();
    void Kill();
}

public interface ILanguageServerLauncher
{
    ILanguageServerProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory);
}

internal sealed record LanguageServerOptions(TimeSpan StartupTimeout, TimeSpan IdleTimeout)
{
    public static LanguageServerOptions Default { get; } = new(TimeSpan.FromSeconds(45), TimeSpan.FromMinutes(5));
}

internal sealed class ProcessLanguageServerLauncher : ILanguageServerLauncher
{
    public ILanguageServerProcess Create(string executable, IReadOnlyList<string> arguments, string workingDirectory) =>
        new ServerProcess(executable, arguments, workingDirectory);

    private sealed class ServerProcess : ILanguageServerProcess
    {
        private readonly Process _process;

        public ServerProcess(string executable, IReadOnlyList<string> arguments, string workingDirectory)
        {
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDirectory
            };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            _process = new Process { StartInfo = start, EnableRaisingEvents = true };
            _process.OutputDataReceived += (_, e) => OutputLine?.Invoke(e.Data);
            _process.ErrorDataReceived += (_, e) => OutputLine?.Invoke(e.Data);
        }

        public event Action<string?>? OutputLine;

        public bool HasExited
        {
            get
            {
                try { return _process.HasExited; }
                catch (InvalidOperationException) { return true; }
            }
        }

        public void Start()
        {
            if (!_process.Start()) throw new InvalidOperationException("无法启动本地语言服务。");
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public void Kill()
        {
            try { if (!_process.HasExited) _process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }

        public void Dispose() => _process.Dispose();
    }
}

/// <summary>
/// Research-bounded local language layer. It only ever rewrites facts the rule voice
/// already decided to say; the server stays on loopback, holds no audio, and is released
/// after a bounded idle period so a desktop companion does not pin a gigabyte all day.
/// </summary>
public sealed class LocalQwenLanguage : IAsyncDisposable
{
    private sealed record Manifest(int SchemaVersion, string ModelName, string Quantization,
        string RuntimePath, string ModelPath, string ModelSource, string ModelRevision,
        string? ModelSha256 = null, string? RuntimeVersion = null);
    private sealed record Installation(Manifest Manifest, string ServerPath, string ModelPath);

    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly object _sync = new();
    private readonly Queue<string> _runtimeLog = new();
    private readonly ILanguageServerLauncher _launcher;
    private readonly LanguageServerOptions _options;
    private readonly Installation? _installation;
    private ILanguageServerProcess? _server;
    private Timer? _idleTimer;
    private Uri? _endpoint;
    private DateTime _lastUsedUtc;
    private int _inFlight;
    private bool _disposed;
    private LocalLanguageState _state;
    private string? _failure;
    private string? _lastGenerationDetail;

    public LocalQwenLanguage()
        : this(Guard(Discover), new ProcessLanguageServerLauncher(), LanguageServerOptions.Default) { }

    internal LocalQwenLanguage(string installRoot, ILanguageServerLauncher launcher, LanguageServerOptions options)
        : this(Guard(() => Load(installRoot)), launcher, options) { }

    private LocalQwenLanguage((Installation? Installation, string? Failure) discovery,
        ILanguageServerLauncher launcher, LanguageServerOptions options)
    {
        _launcher = launcher;
        _options = options;
        _installation = discovery.Installation;
        _failure = discovery.Failure;
        _state = _installation is not null ? LocalLanguageState.Stopped
            : discovery.Failure is null ? LocalLanguageState.NotInstalled
            : LocalLanguageState.Failed;
    }

    public event EventHandler? StateChanged;
    public LocalLanguageState State { get { lock (_sync) return _state; } }
    public bool IsInstalled => _installation is not null;
    internal string? LastGenerationDetail { get { lock (_sync) return _lastGenerationDetail; } }
    public string StatusText
    {
        get
        {
            lock (_sync) return _state switch
            {
                LocalLanguageState.NotInstalled => "未安装本地语言包；仍使用可靠的规则短句。",
                LocalLanguageState.Stopped => $"已发现 {_installation!.Manifest.ModelName} {_installation.Manifest.Quantization}；开始倾听时在本机启动。",
                LocalLanguageState.Starting => "正在本机载入 Qwen；载入期间仍使用规则短句。",
                LocalLanguageState.Ready => $"{_installation!.Manifest.ModelName} {_installation.Manifest.Quantization} 已在本机就绪。",
                _ => "本地 Qwen 暂不可用；已自动回到规则短句。" +
                    (string.IsNullOrWhiteSpace(_failure) ? "" : $"（{_failure}）")
            };
        }
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        if (_installation is null) return;
        try { await EnsureStartedAsync(cancellationToken).ConfigureAwait(false); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { MarkFailed(exception); }
    }

    public async Task<string?> GenerateAsync(CatSpeechRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_installation is null || _disposed) return null;
        Interlocked.Increment(ref _inFlight);
        try
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            Uri endpoint;
            lock (_sync) endpoint = _endpoint ?? throw new InvalidOperationException("本地语言服务尚未就绪。");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var retry = attempt == 0 ? "" : "\n重试要求：上一次照抄了禁用句。必须使用不同句式，不得复述备用句或上一句。";
                var payload = new
                {
                    model = _installation.Manifest.ModelName,
                    messages = new object[]
                    {
                        new { role = "system", content = CatLanguage.SystemPrompt },
                        new { role = "user", content = CatLanguage.BuildUserPrompt(request) + retry }
                    },
                    temperature = attempt == 0 ? .7 : .85,
                    top_p = .8,
                    top_k = 20,
                    presence_penalty = attempt == 0 ? 1.2 : 1.5,
                    max_tokens = 64,
                    stream = false,
                    chat_template_kwargs = new { enable_thinking = false },
                    reasoning_effort = "none"
                };
                using var body = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync(new Uri(endpoint, "v1/chat/completions"), body, timeout.Token)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                    RememberGenerationDetail($"HTTP {(int)response.StatusCode}: {detail}");
                    return null;
                }
                await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);
                var content = json.RootElement.GetProperty("choices")[0].GetProperty("message")
                    .GetProperty("content").GetString();
                if (CatLanguage.TryAccept(content, request, out var accepted))
                {
                    RememberGenerationDetail(attempt == 0 ? "Accepted" : "Accepted after one guarded retry");
                    return accepted;
                }
                RememberGenerationDetail("Output guard rejected: " + content);
            }
            return null;
        }
        catch (OperationCanceledException exception) { RememberGenerationDetail("Cancelled or timed out: " + exception.Message); return null; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidOperationException)
        {
            RememberGenerationDetail(exception.GetType().Name + ": " + exception.Message);
            // A live server that answered badly stays usable; a dead one must not keep
            // advertising "已在本机就绪" until the next request happens to restart it.
            bool serverGone;
            lock (_sync) serverGone = _server is null || _server.HasExited;
            if (serverGone || State != LocalLanguageState.Ready) MarkFailed(exception);
            return null;
        }
        finally
        {
            Touch();
            Interlocked.Decrement(ref _inFlight);
        }
    }

    public static async Task<bool> WriteDiagnosticAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await using var language = new LocalQwenLanguage();
        var cases = new[]
        {
            ("warm-sustain-curious", new ListeningFeatures(ToneColor.Warm, PlayingExpression.Sustained, .86),
                ExpressionStyle.Curious, "我想再听一会儿，这个长音暖暖的。"),
            ("bright-sustain-reserved", new ListeningFeatures(ToneColor.Bright, PlayingExpression.Sustained, .84),
                ExpressionStyle.Reserved, "我听着这个明亮的长音。"),
            ("flowing-reserved", new ListeningFeatures(ToneColor.Warm, PlayingExpression.Flowing, .81),
                ExpressionStyle.Reserved, "我听着这些音一个接着一个。"),
            ("animated-curious", new ListeningFeatures(ToneColor.Bright, PlayingExpression.Animated, .88),
                ExpressionStyle.Curious, "拨弦密起来了，我的爪子也想跟一小段。"),
            ("building-reserved", new ListeningFeatures(ToneColor.Unknown, PlayingExpression.None, .9)
                { Dynamics = DynamicContour.Building }, ExpressionStyle.Reserved, "我听着这段琴声慢慢展开。"),
            ("easing-curious", new ListeningFeatures(ToneColor.Unknown, PlayingExpression.None, .9)
                { Dynamics = DynamicContour.Easing }, ExpressionStyle.Curious, "琴声轻轻收住，我也把爪子放轻。")
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var results = new List<object>();
        var generatedSentences = new List<string>();
        string? previous = "我听着这些音一个接着一个。";
        foreach (var item in cases)
        {
            var request = new CatSpeechRequest(item.Item2, item.Item3, item.Item4, previous);
            var watch = Stopwatch.StartNew();
            var generated = await language.GenerateAsync(request, timeout.Token).ConfigureAwait(false);
            watch.Stop();
            results.Add(new
            {
                name = item.Item1,
                tone = item.Item2.Tone.ToString(),
                expression = item.Item2.Expression.ToString(),
                dynamics = item.Item2.Dynamics.ToString(),
                style = item.Item3.ToString(),
                fallback = item.Item4,
                generated,
                usedFallback = generated is null,
                language.LastGenerationDetail,
                elapsedMilliseconds = watch.ElapsedMilliseconds
            });
            if (generated is not null) { generatedSentences.Add(generated); previous = generated; }
        }
        int distinct = generatedSentences.Distinct(StringComparer.Ordinal).Count();
        int fallbackCount = cases.Length - generatedSentences.Count;
        bool passed = language.IsInstalled && language.State == LocalLanguageState.Ready &&
            generatedSentences.Count >= 4 && distinct == generatedSentences.Count;
        var report = new
        {
            generatedAt = DateTimeOffset.Now,
            installed = language.IsInstalled,
            state = language.State.ToString(),
            language.StatusText,
            passed,
            totalCount = cases.Length,
            generatedCount = generatedSentences.Count,
            fallbackCount,
            distinctCount = distinct,
            generationCoverage = generatedSentences.Count / (double)cases.Length,
            results,
            note = "Pass means the local service was ready, at least four cases produced distinct accepted sentences, and rejected output fell back safely. It does not mean every case generated. Synthetic structured evidence only; no device, audio file, capture, or upload was used."
        };
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
        return passed;
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_installation is null) throw new InvalidOperationException("未安装本地语言包。");
        lock (_sync)
        {
            if (_state == LocalLanguageState.Ready && _server is { HasExited: false }) return;
        }

        await _startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_sync)
            {
                if (_state == LocalLanguageState.Ready && _server is { HasExited: false }) return;
                StopServerLocked();
            }
            SetState(LocalLanguageState.Starting);
            int port = ReserveLoopbackPort();
            var arguments = new List<string>
            {
                "--model", _installation.ModelPath,
                "--host", "127.0.0.1", "--port", port.ToString(),
                "--ctx-size", "2048", "--parallel", "1",
                "--threads", Math.Max(2, Environment.ProcessorCount / 2).ToString(),
                "--no-webui", "--cors-origins", "localhost",
                "--jinja", "--reasoning", "off", "--reasoning-budget", "0"
            };
            var process = _launcher.Create(_installation.ServerPath, arguments, Path.GetDirectoryName(_installation.ServerPath)!);
            process.OutputLine += RememberRuntimeLine;
            process.Start();
            var endpoint = new Uri($"http://127.0.0.1:{port}/");
            lock (_sync) { _server = process; _endpoint = endpoint; }

            using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startup.CancelAfter(_options.StartupTimeout);
            while (true)
            {
                if (process.HasExited)
                    throw new InvalidOperationException("本地语言服务提前退出。" + RuntimeTail());
                try
                {
                    using var response = await _http.GetAsync(new Uri(endpoint, "health"), startup.Token)
                        .ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        SetState(LocalLanguageState.Ready);
                        return;
                    }
                }
                catch (HttpRequestException) { }
                try { await Task.Delay(250, startup.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (startup.IsCancellationRequested) { break; }
            }
            throw new TimeoutException($"本地模型在 {_options.StartupTimeout.TotalSeconds:0} 秒内没有完成载入。");
        }
        finally { _startGate.Release(); }
    }

    private static Installation? Discover()
    {
        foreach (var directory in CandidateDirectories())
        {
            var root = Path.GetFullPath(directory);
            if (!File.Exists(Path.Combine(root, "manifest.json"))) continue;
            return Load(root);
        }
        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        var overridePath = Environment.GetEnvironmentVariable("GUITAR_SOUL_QWEN_HOME");
        if (!string.IsNullOrWhiteSpace(overridePath)) yield return overridePath;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Guitar Soul", "Models", "Qwen3-1.7B");
        yield return Path.Combine(AppContext.BaseDirectory, "Models", "Qwen3-1.7B");
    }

    private static Installation Load(string directory)
    {
        var root = Path.GetFullPath(directory);
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath)) throw new FileNotFoundException("缺少本地语言包清单。", manifestPath);
        var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("本地语言包清单为空。");
        if (manifest.SchemaVersion != 1 || !string.Equals(manifest.ModelName, "Qwen3-1.7B", StringComparison.Ordinal) ||
            !string.Equals(manifest.Quantization, "Q4_K_M", StringComparison.Ordinal))
            throw new InvalidDataException("本地语言包版本或量化格式不受支持。");
        var server = ResolveInside(root, manifest.RuntimePath);
        var model = ResolveInside(root, manifest.ModelPath);
        if (!File.Exists(server) || !File.Exists(model))
            throw new InvalidDataException("本地语言包缺少运行时或模型文件。");
        return new(manifest, server, model);
    }

    private static (Installation? Installation, string? Failure) Guard(Func<Installation?> load)
    {
        try { return (load(), null); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { return (null, exception.Message); }
    }

    private static string ResolveInside(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
            throw new InvalidDataException("本地语言包清单含有无效路径。");
        var result = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("本地语言包路径超出安装目录。");
        return result;
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private void Touch()
    {
        lock (_sync) _lastUsedUtc = DateTime.UtcNow;
    }

    private void RememberRuntimeLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_sync)
        {
            _runtimeLog.Enqueue(line.Trim());
            while (_runtimeLog.Count > 12) _runtimeLog.Dequeue();
        }
    }

    private void RememberGenerationDetail(string detail)
    {
        if (detail.Length > 800) detail = detail[..800];
        lock (_sync) _lastGenerationDetail = detail;
    }

    private string RuntimeTail()
    {
        lock (_sync) return _runtimeLog.Count == 0 ? "" : " " + string.Join(" | ", _runtimeLog.TakeLast(3));
    }

    private void MarkFailed(Exception exception)
    {
        lock (_sync) _failure = exception.Message;
        SetState(LocalLanguageState.Failed);
    }

    private void SetState(LocalLanguageState state)
    {
        bool changed;
        lock (_sync)
        {
            changed = _state != state;
            _state = state;
            if (state == LocalLanguageState.Ready) _lastUsedUtc = DateTime.UtcNow;
        }
        if (!changed) return;
        if (state == LocalLanguageState.Ready) StartIdleTimer();
        else StopIdleTimer();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartIdleTimer()
    {
        var period = TimeSpan.FromMilliseconds(
            Math.Clamp(_options.IdleTimeout.TotalMilliseconds / 4, 50, 30_000));
        lock (_sync)
        {
            _idleTimer?.Dispose();
            _idleTimer = new Timer(OnIdleTick, null, period, period);
        }
    }

    private void StopIdleTimer()
    {
        lock (_sync) { _idleTimer?.Dispose(); _idleTimer = null; }
    }

    private void OnIdleTick(object? state)
    {
        try
        {
            bool stopped = false;
            lock (_sync)
            {
                if (_disposed || _state != LocalLanguageState.Ready) return;
                if (Volatile.Read(ref _inFlight) > 0) return;
                if (_server is null || _server.HasExited) return;
                if (DateTime.UtcNow - _lastUsedUtc < _options.IdleTimeout) return;
                StopServerLocked();
                stopped = true;
            }
            if (stopped) SetState(LocalLanguageState.Stopped);
        }
        catch (Exception)
        {
            // A timer callback must never take the process down; the next request restarts cleanly.
        }
    }

    private void StopServerLocked()
    {
        _idleTimer?.Dispose();
        _idleTimer = null;
        if (_server is null) return;
        try { _server.Kill(); }
        catch (InvalidOperationException) { }
        finally { _server.Dispose(); _server = null; _endpoint = null; }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        lock (_sync) StopServerLocked();
        _http.Dispose();
        _startGate.Dispose();
        return ValueTask.CompletedTask;
    }
}
