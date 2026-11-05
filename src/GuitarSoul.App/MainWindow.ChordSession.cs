using System.Globalization;
using System.Windows;
using System.Windows.Input;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private readonly ChordLoopTracker _chordLoop = new();
    private readonly ChordIdleReset _chordIdle = new();
    private ChordSegment[] _previousChordDraft = [];
    private double _lastChordSoundAt = double.NegativeInfinity;
    private string _chordVisualKey = "";
    private HarmonyFrame _chordEvidence;

    private void AdvanceChordSession(double now, bool newSound)
    {
        if (newSound) _lastChordSoundAt = now;
        if (!_chordIdle.Advance(now, newSound, _progression.Count > 0 || _progression.Current is not null, _petPreferences.ChordResetSeconds)) return;
        _progression.Close();
        _previousChordDraft = _progression.Segments.ToArray();
        _progression.Clear(); _chordLoop.Clear();
        _chordTimeOrigin = null;
        RestoreChordButton.Visibility = _previousChordDraft.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _chordVisualKey = "reset";
    }

    private void RestoreChord_Click(object sender, RoutedEventArgs e)
    {
        if (_previousChordDraft.Length == 0) return;
        _progression.Close();
        var replacement = _progression.Segments.ToArray();
        _progression.Restore(_previousChordDraft);
        _chordTimeOrigin = _companionClock.Elapsed.TotalSeconds - _progression.Segments.Max(x => x.EndSeconds);
        _previousChordDraft = replacement; _chordLoop.Clear(); _chordIdle.Restart();
        RestoreChordButton.Visibility = replacement.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _chordVisualKey = "restored";
        RefreshProgression();
    }

    private void ChordReset_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitChordReset(); e.Handled = true;
    }

    private void ChordReset_Changed(object sender, RoutedEventArgs e) => CommitChordReset();

    private void CommitChordReset()
    {
        if (!_preferencesReady) return;
        if (!int.TryParse(ChordResetSecondsInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds) ||
            (seconds != 0 && seconds is < 5 or > 300))
        {
            ChordResetSecondsInput.Text = _petPreferences.ChordResetSeconds.ToString(CultureInfo.InvariantCulture);
            StatusMessage.Text = "空闲重置请输入5–300秒，或0关闭。";
            return;
        }
        _petPreferences = _petPreferences with { ChordResetSeconds = seconds };
        _chordIdle.Restart(); // A settings edit starts a fresh grace period, never clears immediately.
        if (!_suppressPreferenceSave)
        {
            try { _petPreferences.Save(); }
            catch (Exception exception) { ShowError("无法保存和弦设置。", exception); }
        }
    }
}
