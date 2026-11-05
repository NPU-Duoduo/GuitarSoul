using System.IO;
using System.Windows;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private ChordDraftStore? _draftStore;
    private bool _draftBlocked;
    private double _lastDraftCheck = -10;
    private double? _chordTimeOrigin;
    private ChordSegment[]? _lastCheckpointDraft;

    private void InitializeChordRecovery(ChordDraftStore? isolatedStore = null)
    {
        if (_suppressPreferenceSave && isolatedStore is null) return;
        _draftStore = isolatedStore ?? new ChordDraftStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guitar Soul", "chord-draft.json"));
        if (!_draftStore.TryLoad(out var draft, out var error))
        {
            _draftBlocked = true;
            StatusMessage.Text = "旧和弦草稿无法读取，已保留原文件：" + error;
            return;
        }
        _previousChordDraft = draft;
        _lastCheckpointDraft = draft;
        _draftBlocked = false;
        RestoreChordButton.Visibility = draft.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private double ChordSessionSeconds(double monotonicNow)
    {
        _chordTimeOrigin ??= monotonicNow;
        return Math.Max(0, monotonicNow - _chordTimeOrigin.Value);
    }

    private void CheckpointChordDraft(bool force = false)
    {
        if (_draftStore is null || _draftBlocked) return;
        double now = _companionClock.Elapsed.TotalSeconds;
        if (!force && now - _lastDraftCheck < 5) return;
        _lastDraftCheck = now;
        var draft = _progression.Segments.ToList();
        if (_progression.Current is ChordSegment live && live.Duration >= ChordTimeline.MinimumSegmentSeconds) draft.Add(live);
        if (draft.Count == 0) draft.AddRange(_previousChordDraft);
        if (draft.Count == 0) return;
        if (!force && _lastCheckpointDraft is not null && draft.SequenceEqual(_lastCheckpointDraft)) return;
        try { _draftStore.Save(draft); _lastCheckpointDraft = draft.ToArray(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { StatusMessage.Text = "和弦草稿暂未保存，请手动保存：" + exception.Message; }
    }
}
