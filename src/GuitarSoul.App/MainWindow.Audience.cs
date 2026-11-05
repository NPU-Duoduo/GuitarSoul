using System.IO;
using System.Text.Json;
using System.Windows;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private readonly AudienceRiffPolicy _audienceRiffs = new();
    private bool _bufferEnabled;
    private void SyncRiffBuffer()
    {
        bool enabled = RiffBufferToggle.IsChecked == true || AutoRiffToggle.IsChecked == true;
        if (enabled == _bufferEnabled) return;
        _capture.SetRiffBufferEnabled(enabled);
        _bufferEnabled = enabled;
    }
    private void AutoRiff_Changed(object sender, RoutedEventArgs e)
    {
        if (!_preferencesReady) return;
        UpdatePetPreferences();
        SyncRiffBuffer();
        // Drop a pending phrase on opt-out, retaining the per-session quota/cooldown.
        _audienceRiffs.Step(_latestRhythm with { AudioSeconds = _latestRhythm.AudioSeconds + .001 }, false, false, true);
        AutoRiffStatus.Text = AutoRiffToggle.IsChecked == true
            ? "开始倾听后，它会偶尔保存一小段声音，只留在这台电脑。"
            : "尚未开启。由你决定要不要让它留下声音。";
        RefreshRiffBuffer();
    }
    private async void ProcessAudienceRiff(RhythmFrame sound, bool active, bool fresh)
    {
        var selection = _audienceRiffs.Step(sound, active,
            _petPreferences.AutoRiffs && _capture.IsCapturing,
            !fresh || _calibration is not null || IsKeepsakeReviewPlaying || _capture.IsRecording || _savingRiff);
        if (selection is null || _capture.BufferedSeconds < 8) return;
        _savingRiff = true;
        try
        {
            Directory.CreateDirectory(RiffDirectory);
            var existing = new DirectoryInfo(RiffDirectory).EnumerateFiles("Riff-Cat-*.wav").ToArray();
            if (existing.Length >= 200 || existing.Sum(f => f.Length) >= 500L * 1024 * 1024)
                throw new IOException("猫的声音留念已满，请先在留念页删除一些片段后再开启。");
            var path = Path.Combine(RiffDirectory, $"Riff-Cat-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.wav");
            await _capture.SaveRecentRiffAsync(path, selection.Seconds);
            // Store provenance with the WAV too, so a memory-store conflict cannot orphan its meaning.
            File.WriteAllText(Path.ChangeExtension(path, ".json"), JsonSerializer.Serialize(selection));
            if (_disposed) return;
            if (_memories is not null)
            {
                var memory = _memories.Add(selection.Title, "猫的留念：" + selection.Note,
                    MemorySource.SavedRiff, path, DateTimeOffset.Now);
                RefreshMemories(memory.Id);
            }
            RefreshRiffs();
            AudienceNote.Text = "它留下一段：「" + selection.Note + "」";
            AutoRiffStatus.Text = "刚留下一段，过一会儿再记。你可以在「留念」里回听或删除声音。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            if (!_disposed)
            {
                AutoRiffToggle.IsChecked = false;
                AutoRiffStatus.Text = "自动留念已暂停：" + error.Message;
                RefreshRiffs();
            }
        }
        finally { _savingRiff = false; if (!_disposed) RefreshRiffBuffer(); }
    }
    private static string RiffLabel(FileInfo file)
    {
        if (file.Name.StartsWith("Riff-Cat-", StringComparison.Ordinal))
        {
            try
            {
                var note = JsonSerializer.Deserialize<AudienceRiff>(File.ReadAllText(Path.ChangeExtension(file.FullName, ".json")));
                return $"{file.LastWriteTime:MM-dd HH:mm} · 猫留下的「{note?.Title ?? "一小段"}」";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        }
        return $"{file.LastWriteTime:MM-dd HH:mm} · {file.Name}";
    }
    private void DeleteRiff_Click(object sender, RoutedEventArgs e)
    {
        if (RiffList.SelectedItem is not RiffItem item) { StatusMessage.Text = "先选一段想删除的声音。"; return; }
        try
        {
            var path = Path.GetFullPath(item.Path);
            var root = Path.GetFullPath(RiffDirectory) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new IOException("该声音不在留念目录中。");
            _playback.Stop();
            File.Delete(path);
            File.Delete(Path.ChangeExtension(path, ".json"));
            RefreshRiffs(); RefreshMemories();
            StatusMessage.Text = "这段声音已删除；如有文字记忆，它仍保留。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StatusMessage.Text = "未能删除声音：" + ex.Message; }
    }
    private AudioDeviceInfo? ResolvePlaybackOutput()
    {
        if (OutputDevices.SelectedItem is AudioDeviceInfo selected) return selected;
        try
        {
            var id = AudioDevices.DefaultOutputId();
            var output = _outputs.FirstOrDefault(d => d.Id == id);
            if (output is not null) { OutputDevices.SelectedItem = output; return output; }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        MainTabs.SelectedIndex = 0;
        PlaybackOptions.Visibility = Visibility.Visible;
        PlaybackOptions.IsExpanded = true;
        UpdateLayout();
        OutputDevices.BringIntoView();
        OutputDevices.Focus();
        StatusMessage.Text = "未找到播放设备，请在「播放设备与音量」中选择输出。";
        return null;
    }
}
