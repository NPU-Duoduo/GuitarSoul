using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private void RunChordSessionChecks(string reportPath, Action<bool, string> check)
    {
        var previousPreferences = _petPreferences;
        ChordResetSecondsInput.Text = "27"; CommitChordReset(); UpdatePetPreferences();
        string preferencePath = Path.ChangeExtension(reportPath, ".chord-preferences.json");
        _petPreferences.Save(preferencePath);
        check(PetPreferences.Load(preferencePath).ChordResetSeconds == 27,
            "Custom chord idle reset survives preference updates and reload");
        ChordResetSecondsInput.Text = "2"; CommitChordReset();
        check(_petPreferences.ChordResetSeconds == 27 && ChordResetSecondsInput.Text == "27",
            "Invalid short reset interval restores the previous value without deleting chords");
        StatusMessage.Text = "";
        _progression.Clear(); _chordLoop.Clear(); _chordIdle.Restart();
        _latestSignal = new SignalAnalyzer().Process(Enumerable.Range(0, 4800).Select(i => (float)(.3 * Math.Sin(i * .1))).ToArray());
        void Feed(int root, ChordQuality quality, double start)
        {
            var chord = new HarmonyFrame(root, quality, .95, 0);
            _progression.Observe(chord, start); _progression.Observe(chord, start + .6);
            _chordEvidence = chord;
            AdvanceChordSession(_companionClock.Elapsed.TotalSeconds, true);
            RefreshProgression();
        }
        for (int i = 0; i < 8; i++)
        {
            int root = new[] {0, 7, 9, 5}[i % 4];
            Feed(root, root == 9 ? ChordQuality.Minor : ChordQuality.Major, i);
        }
        MainTabs.SelectedIndex = 0;
        RenderPreview(Path.ChangeExtension(reportPath, ".chord-loop.png"));
        int HighlightCount() => ProgressionStrip.Children.Cast<FrameworkElement>().Count(x => AutomationProperties.GetItemStatus(x) == "当前和弦");
        check(_chordLoop.IsConfirmed && ProgressionStrip.Children.Count == 4 && HighlightCount() == 1 &&
            AutomationProperties.GetName((FrameworkElement)ProgressionStrip.Children[3]) == "F",
            "Two passes render four loop slots and one current chord instead of duplicated history");
        Feed(0, ChordQuality.Major, 8);
        check(AutomationProperties.GetItemStatus((FrameworkElement)ProgressionStrip.Children[0]) == "当前和弦" && HighlightCount() == 1,
            "Returning to the loop beginning moves the visible highlight to its first slot");
        _chordEvidence = default; RefreshProgression();
        check(HighlightCount() == 0 && ProgressionStrip.Children.Count == 4,
            "Unclear harmony removes the current highlight while preserving the confirmed pattern");
        AdvanceChordSession(_companionClock.Elapsed.TotalSeconds + 28, false); RefreshProgression();
        check(_progression.Count == 0 && _progression.Current is null && ProgressionStrip.Children.Count == 0 && RestoreChordButton.Visibility == Visibility.Visible,
            "Idle deadline clears the live song and exposes a one-step recovery action");
        RestoreChordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(_progression.Count == 9 && _chordLoop.IsConfirmed && RestoreChordButton.Visibility == Visibility.Collapsed,
            "Recovery restores the previous draft and reconstructs its loop without opening audio");
        _petPreferences = previousPreferences;
        ChordResetSecondsInput.Text = previousPreferences.ChordResetSeconds.ToString();
        _progression.Clear(); _chordLoop.Clear(); _chordIdle.Restart(); _chordEvidence = default;
        _previousChordDraft = []; RefreshProgression();
        ChordSegment[] keyDraft = [new(0, ChordQuality.Major, 0, 1), new(5, ChordQuality.Major, 1, 2), new(7, ChordQuality.Major, 2, 3)];
        _progression.Restore(keyDraft);
        var firstKey = GuessKey();
        _progression.Restore(keyDraft.Select(x => x with { Root = (x.Root + 2) % 12 }).ToArray());
        var shiftedKey = GuessKey();
        check(firstKey is not null && shiftedKey is not null && shiftedKey.Value.Key.PitchClass == (firstKey.Value.Key.PitchClass + 2) % 12,
            "Replacing a same-length draft recalculates key instead of reusing the old cached guess");
        _progression.Clear();
        _chordTimeOrigin = null;
        check(ChordSessionSeconds(100) == 0 && ChordSessionSeconds(101) == 1 && ChordSessionSeconds(110) == 10,
            "Chord session timestamps continue across capture restarts instead of using the restarted audio clock");
        _chordTimeOrigin = null;
        var checkpointPath = Path.ChangeExtension(reportPath, ".draft.json");
        _draftStore = new ChordDraftStore(checkpointPath);
        _progression.Restore([new ChordSegment(0, ChordQuality.Major, 0, 1)]);
        CheckpointChordDraft(force: true);
        check(_draftStore.TryLoad(out var savedDraft, out _) && savedDraft.Length == 1,
            "App checkpoints symbolic chords without capturing or opening a device");
        _progression.Clear(); _previousChordDraft = [];
        InitializeChordRecovery(new ChordDraftStore(checkpointPath));
        check(_previousChordDraft.Length == 1 && RestoreChordButton.Visibility == Visibility.Visible &&
            _progression.Count == 0 && !_capture.IsCapturing, "Startup recovery offers a draft without automatically resuming recording");
        System.IO.File.WriteAllText(checkpointPath, "{damaged");
        InitializeChordRecovery(new ChordDraftStore(checkpointPath));
        CheckpointChordDraft(force: true);
        check(System.IO.File.ReadAllText(checkpointPath) == "{damaged", "App does not overwrite a damaged recovery file");
        RefreshProgression();
        check(ClearProgressionButton.IsEnabled && _draftBlocked,
            "An unreadable saved draft can be explicitly cleared from the empty chord panel");
        ClearProgression_Click(this, new RoutedEventArgs());
        check(!System.IO.File.Exists(checkpointPath) && _progression.Count == 0 && !_draftBlocked,
            "Explicit chord clear also clears the persisted checkpoint");
        _draftStore = null; _chordTimeOrigin = null;
    }
}
