using System.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Language;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private readonly LocalQwenLanguage _localLanguage = new();
    private CancellationTokenSource? _localSpeechCancellation;
    private long _localSpeechRequest;
    private string? _currentRuleSpeech;
    private string? _lastGeneratedSpeech;

    private void InitializeLocalLanguage()
    {
        _localLanguage.StateChanged += LocalLanguage_StateChanged;
        RefreshLocalLanguageStatus();
    }

    private void LocalLanguage_StateChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        Dispatcher.BeginInvoke(new Action(RefreshLocalLanguageStatus));
    }

    private void RefreshLocalLanguageStatus()
    {
        if (LocalLanguageStatus is null) return;
        LocalLanguageStatus.Text = LocalLanguageToggle.IsChecked == true
            ? _localLanguage.StatusText
            : "本地丰富短句已关闭；猫仍会使用有依据的规则短句。";
    }

    private async Task WarmUpLocalLanguageAsync()
    {
        if (LocalLanguageToggle.IsChecked != true || !_localLanguage.IsInstalled) return;
        await _localLanguage.WarmUpAsync();
    }

    private void LocalLanguage_Changed(object sender, RoutedEventArgs e)
    {
        if (!_preferencesReady)
        {
            RefreshLocalLanguageStatus();
            return;
        }
        UpdatePetPreferences();
        CancelLocalSpeech();
        _currentRuleSpeech = null;
        RefreshLocalLanguageStatus();
        if (LocalLanguageToggle.IsChecked == true)
        {
            _ = WarmUpLocalLanguageAsync();
            PresentListeningReaction(_voice.Current, _voice.Source);
        }
        else SetListeningReaction(_voice.Current);
    }

    private void PresentListeningReaction(string? fallback, ListeningFeatures evidence)
    {
        if (string.IsNullOrWhiteSpace(fallback))
        {
            CancelLocalSpeech();
            _currentRuleSpeech = null;
            SetListeningReaction(null);
            return;
        }
        if (string.Equals(fallback, _currentRuleSpeech, StringComparison.Ordinal)) return;

        CancelLocalSpeech();
        _currentRuleSpeech = fallback;
        if (LocalLanguageToggle.IsChecked != true || !_localLanguage.IsInstalled)
        {
            SetListeningReaction(fallback);
            return;
        }

        var request = new CatSpeechRequest(evidence, _petPreferences.Expression, fallback, _lastGeneratedSpeech);
        var cancellation = new CancellationTokenSource();
        _localSpeechCancellation = cancellation;
        long requestId = ++_localSpeechRequest;
        SetListeningReaction(null);
        _ = ResolveLocalSpeechAsync(request, requestId, cancellation.Token);
    }

    private async Task ResolveLocalSpeechAsync(CatSpeechRequest request, long requestId, CancellationToken cancellationToken)
    {
        try
        {
            var generation = _localLanguage.GenerateAsync(request, cancellationToken);
            var grace = Task.Delay(700, cancellationToken);
            if (await Task.WhenAny(generation, grace) == grace && IsCurrentSpeech(request, requestId, cancellationToken))
                SetListeningReaction(request.Fallback);

            var generated = await generation;
            if (!IsCurrentSpeech(request, requestId, cancellationToken)) return;
            if (generated is null) SetListeningReaction(request.Fallback);
            else
            {
                _lastGeneratedSpeech = generated;
                SetListeningReaction(generated);
            }
        }
        catch (OperationCanceledException) { }
    }

    private bool IsCurrentSpeech(CatSpeechRequest request, long requestId, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && requestId == _localSpeechRequest &&
        string.Equals(request.Fallback, _currentRuleSpeech, StringComparison.Ordinal) && _voice.Current is not null;

    private void CancelLocalSpeech()
    {
        _localSpeechRequest++;
        var cancellation = _localSpeechCancellation;
        _localSpeechCancellation = null;
        if (cancellation is null) return;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private void DisposeLocalLanguage()
    {
        CancelLocalSpeech();
        _localLanguage.StateChanged -= LocalLanguage_StateChanged;
        _localLanguage.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
