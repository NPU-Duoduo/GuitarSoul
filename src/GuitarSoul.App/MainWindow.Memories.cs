using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private sealed record MemoryItem(Guid Id, string Label);
    private MemoryStore? _memories;
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly DateTimeOffset _sessionStartedAt = DateTimeOffset.Now;
    private MemoryInvitationPolicy _invitations = new(DateTimeOffset.Now);
    private Guid? _displayedInvitation;
    private static string MemoryDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guitar Soul");

    private void InitializeMemories(bool isolatedChecks, string? checkPath = null)
    {
        _memories = null;
        _invitations = new(_sessionStartedAt);
        try
        {
            var path = checkPath ?? (isolatedChecks ? Path.Combine(Path.GetTempPath(), "GuitarSoul-check-" + Guid.NewGuid().ToString("N"), "memories.json")
                : Path.Combine(MemoryDataDirectory, "memories.json"));
            _memories = new MemoryStore(path);
            RefreshMemories();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusMessage.Text = "记忆文件暂时无法读取，原文件已保留。" + e.Message;
            MemorySummary.Text = StatusMessage.Text;
            MemoryInvitations.IsEnabled = false;
        }
    }

    private void RefreshMemories(Guid? selected = null)
    {
        if (_memories is null) return;
        selected ??= (MemoryList.SelectedItem as MemoryItem)?.Id;
        MemoryList.ItemsSource = _memories.Memories.OrderByDescending(m => m.CreatedAt)
            .Select(m => new MemoryItem(m.Id, $"{m.Title}  ·  {m.CreatedAt.LocalDateTime:MM-dd HH:mm}")).ToArray();
        MemoryList.SelectedItem = MemoryList.Items.Cast<MemoryItem>().FirstOrDefault(item => item.Id == selected);
        if (MemoryList.SelectedItem is not null) UpdateSelectedMemory();
        if (MemoryList.SelectedItem is null)
            MemorySummary.Text = _memories.Memories.Count == 0 ? "还没有记忆。它留下的片段会出现在这里，你也可以添一句话。" : $"有 {_memories.Memories.Count} 段共同记忆。";
    }

    private void Memory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateSelectedMemory();

    private void UpdateSelectedMemory()
    {
        if (_memories is null || MemoryList.SelectedItem is not MemoryItem selected) return;
        var memory = _memories.Find(selected.Id);
        MemoryTitle.Text = memory.Title;
        MemoryNotes.Text = memory.Notes;
        AttachRiff.IsChecked = false;
        var visits = _memories.Links.Count(link => link.MemoryId == memory.Id);
        var audio = memory.AudioPath is null ? "文字记忆" : File.Exists(memory.AudioPath) ? "附有已收藏的 Riff" : "原音频附件已找不到";
        var catSelected = memory.Source == MemorySource.CatRiff ||
            memory.Source == MemorySource.SavedRiff && memory.AudioPath is not null &&
            Path.GetFileName(memory.AudioPath).StartsWith("Riff-Cat-", StringComparison.Ordinal);
        MemorySummary.Text = $"{memory.CreatedAt.LocalDateTime:yyyy-MM-dd HH:mm} · {(catSelected ? "猫主动留下 · " : "")}{audio}" +
            (visits > 0 ? $" · 你确认过 {visits} 次回访" : "") + "\n名称与回访由你确认。";
    }

    private void SaveMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_memories is null) { StatusMessage.Text = "记忆文件未加载，暂时不能保存；原文件保持完整。"; return; }
        try
        {
            string? audioPath = null;
            if (AttachRiff.IsChecked == true)
            {
                if (RiffList.SelectedItem is not RiffItem riff) { StatusMessage.Text = "先在 Riff 收藏页选择一段已保存的片段，或者取消附件。"; return; }
                audioPath = riff.Path;
            }
            var memory = _memories.Add(MemoryTitle.Text, MemoryNotes.Text,
                audioPath is null ? MemorySource.UserNote : MemorySource.SavedRiff, audioPath, DateTimeOffset.Now);
            RefreshMemories(memory.Id);
            StatusMessage.Text = $"记住了「{memory.Title}」。" + (audioPath is null ? "这次只保存名字与备注。" : "附件使用已有收藏，没有新增录音。");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { StatusMessage.Text = "这段记忆没有保存：" + error.Message; }
    }

    private void EditMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_memories is null || MemoryList.SelectedItem is not MemoryItem selected) { StatusMessage.Text = "先选择想改名字或备注的记忆。"; return; }
        try
        {
            _memories.Edit(selected.Id, MemoryTitle.Text, MemoryNotes.Text, DateTimeOffset.Now);
            RefreshMemories(selected.Id);
            UpdateInvitationPresentation(_displayedInvitation);
            StatusMessage.Text = "名字与备注已更新，原来的经历与附件仍在。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { StatusMessage.Text = "修改没有保存：" + error.Message; }
    }

    private void NewMemory_Click(object sender, RoutedEventArgs e)
    {
        MemoryList.SelectedItem = null; MemoryTitle.Clear(); MemoryNotes.Clear(); AttachRiff.IsChecked = false;
        MemorySummary.Text = "可以记下一首歌的名字、一小段旋律，或者今天的练琴经历。";
    }

    private void ConfirmMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_memories is null || MemoryList.SelectedItem is not MemoryItem selected) { StatusMessage.Text = "先选择你这次弹的那段记忆。"; return; }
        try
        {
            var memory = _memories.Find(selected.Id);
            _memories.Confirm(selected.Id, _sessionId, DateTimeOffset.Now);
            _invitations.Dismiss(); UpdateInvitationPresentation(null);
            RefreshMemories(selected.Id);
            StatusMessage.Text = $"记下了：你说这次弹的是「{memory.Title}」。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusMessage.Text = "回访没有保存：" + error.Message; }
    }

    private void DeleteMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_memories is null || MemoryList.SelectedItem is not MemoryItem selected) { StatusMessage.Text = "先选择想删除的记忆。"; return; }
        try
        {
            _memories.Delete(selected.Id);
            if (_displayedInvitation == selected.Id) { _invitations.Dismiss(); UpdateInvitationPresentation(null); }
            RefreshMemories(); MemoryTitle.Clear(); MemoryNotes.Clear(); AttachRiff.IsChecked = false;
            StatusMessage.Text = "所选记忆与关联已删除，Riff 音频文件仍保留。";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { StatusMessage.Text = "删除没有保存：" + error.Message; }
    }

    private void PlayMemory_Click(object sender, RoutedEventArgs e)
    {
        if (_memories is null || MemoryList.SelectedItem is not MemoryItem selected) { StatusMessage.Text = "先选择一段记忆。"; return; }
        var memory = _memories.Find(selected.Id);
        if (memory.AudioPath is null || !File.Exists(memory.AudioPath)) { StatusMessage.Text = "这段记忆没有可回听的音频附件。"; return; }
        var output = ResolvePlaybackOutput();
        if (output is null) return;
        try
        {
            _playback.Load(memory.AudioPath, output.Id);
            _playbackPurpose = PlaybackPurpose.KeepsakeReview;
            _playback.Volume = (float)Volume.Value;
            TrackName.Text = Path.GetFileName(memory.AudioPath);
            TrackTransport.Visibility = PlaybackOptions.Visibility = Visibility.Visible;
            Seek.Maximum = Math.Max(1, _playback.Duration.TotalSeconds);
            Seek.IsEnabled = PlayButton.IsEnabled = StopPlayButton.IsEnabled = true;
            _playback.Play(); UpdatePlaybackUi(true); UpdateRiffPlaybackLabel();
            _invitations.Dismiss(); UpdateInvitationPresentation(null);
            StatusMessage.Text = $"正在回听「{memory.Title}」的附件；播放控制在「陪伴」页的伴奏区。";
        }
        catch (Exception exception) { ShowError("无法回听附件。", exception); }
    }

    private void OpenMemories_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(MemoryDataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{MemoryDataDirectory}\"") { UseShellExecute = true });
        }
        catch (Exception error) { StatusMessage.Text = "无法打开数据目录：" + error.Message; }
    }

    private void MemoryInvitations_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePetPreferences();
        if (_preferencesReady && MemoryInvitations.IsChecked != true)
        { _invitations.Dismiss(); UpdateInvitationPresentation(null); }
    }

    private void RefreshMemoryInvitation(double elapsed, bool active)
    {
        if (_memories is null) return;
        var pending = _invitations.Step(_memories.Memories, elapsed, _capture.IsCapturing, active,
            _petPreferences.MemoryInvitations, _petPreferences.QuietMode, IsVisible || _petWindow.IsVisible,
            _playback.IsPlaying || _capture.IsRecording || _calibration is not null || _savingRiff || PreviewMotion.IsChecked == true);
        if (pending != _displayedInvitation) UpdateInvitationPresentation(pending);
    }

    private void UpdateInvitationPresentation(Guid? id)
    {
        var memory = _memories?.Memories.FirstOrDefault(m => m.Id == id);
        _displayedInvitation = memory?.Id;
        var text = memory is null ? null : $"还记得你留下的「{memory.Title}」。有空时，想再听一听。";
        InvitationText.Text = text ?? "";
        MemoryInvitation.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        _petWindow.SetInvitation(text);
    }

    private void OpenInvitation_Click(object sender, RoutedEventArgs e) => OpenInvitation();
    private void OpenInvitation()
    {
        if (_displayedInvitation is not Guid id) return;
        ShowPanel(); MainTabs.SelectedItem = MemoriesTab;
        MemoryList.SelectedItem = MemoryList.Items.Cast<MemoryItem>().FirstOrDefault(item => item.Id == id);
        _invitations.Dismiss(); UpdateInvitationPresentation(null);
    }
    private void DismissInvitation_Click(object sender, RoutedEventArgs e)
    { _invitations.Dismiss(); UpdateInvitationPresentation(null); }

    private void RunMemoryUiChecks(string reportPath, Action<bool, string> check)
    {
        var folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "memory-ui-" + Guid.NewGuid().ToString("N")[..8]);
        _memories = new MemoryStore(Path.Combine(folder, "memories.json"));
        check(!MemoryInvitations.IsChecked.GetValueOrDefault() && !File.Exists(_memories.Path), "Memory invitations default off and loading creates no data");
        MainTabs.SelectedItem = MemoriesTab;
        MemoryTitle.Text = "September"; MemoryNotes.Text = "我主动留下的练琴片段";
        SaveMemory_Click(this, new RoutedEventArgs());
        var created = _memories.Memories.Single();
        check(created.Title == "September" && created.AudioPath is null && !_capture.IsCapturing && !RiffBufferToggle.IsChecked.GetValueOrDefault(),
            "Text memory saves without capture, buffering or audio attachment");
        MemoryTitle.Text = "九月的旋律";
        EditMemory_Click(this, new RoutedEventArgs());
        check(_memories.Find(created.Id).Title == "九月的旋律" && _memories.Find(created.Id).Id == created.Id, "Rename preserves memory identity");
        ConfirmMemory_Click(this, new RoutedEventArgs());
        ConfirmMemory_Click(this, new RoutedEventArgs());
        check(_memories.Links.Count == 1 && _memories.Links[0].MemoryId == created.Id, "Only explicit confirmation links a memory once per session");
        SkinSelector.SelectedIndex = (int)Controls.CatSkin.Ginger;
        QuietMode.IsChecked = true;
        check(_memories.Memories.Count == 1 && _memories.Links.Count == 1, "Appearance and quiet settings preserve shared memories");
        QuietMode.IsChecked = false;
        check(new MemoryStore(_memories.Path).Find(created.Id).Title == "九月的旋律", "Shared memory survives reopening its local file");
        DeleteMemory_Click(this, new RoutedEventArgs());
        check(_memories.Memories.Count == 0 && _memories.Links.Count == 0, "Deleting a memory removes its session links");
        // A synthetic existing attachment checks UI association without recording a device.
        Directory.CreateDirectory(folder);
        var attachment = Path.Combine(folder, "existing-riff.wav");
        File.WriteAllBytes(attachment, new byte[] { 82, 73, 70, 70 });
        RiffList.ItemsSource = new[] { new RiffItem(attachment, "已有 Riff 测试附件") };
        RiffList.SelectedIndex = 0;
        MemoryTitle.Text = "一段已有收藏"; AttachRiff.IsChecked = true;
        SaveMemory_Click(this, new RoutedEventArgs());
        check(_memories.Memories.Single().AudioPath == attachment && !_capture.IsRecording, "Audio attachment references an already saved file without recording");
        DeleteMemory_Click(this, new RoutedEventArgs());
        check(File.Exists(attachment), "Deleting memory leaves its audio file intact");
        RefreshRiffs();
        MemoryTitle.Text = "September"; MemoryNotes.Text = "界面演示 · 只在测试目录保存";
        SaveMemory_Click(this, new RoutedEventArgs());
        var visible = _memories.Memories.Single();
        var validPath = _memories.Path;
        var damagedPath = Path.Combine(folder, "damaged-memories.json");
        File.WriteAllText(damagedPath, "{damaged");
        InitializeMemories(true, damagedPath);
        check(_memories is null && !MemoryInvitations.IsEnabled && File.ReadAllText(damagedPath) == "{damaged",
            "Damaged memory data does not crash the panel or get overwritten");
        InitializeMemories(true, validPath);
        MemoryInvitations.IsEnabled = true;
        RefreshMemories(visible.Id);
        UpdateInvitationPresentation(visible.Id);
        check(MemoryInvitation.Visibility == Visibility.Visible && InvitationText.Text.Contains(visible.Title), "Invitation displays an existing confirmed memory title");
        QuietMode.IsChecked = true;
        check(MemoryInvitation.Visibility == Visibility.Collapsed, "Quiet immediately dismisses the invitation presentation");
        QuietMode.IsChecked = false;
        MemoryInvitations.IsChecked = true;
        check(_petPreferences.MemoryInvitations && !_capture.IsCapturing, "Invitation opt-in does not start capture");
        MemoryInvitations.IsChecked = false;
        MainTabs.SelectedItem = MemoriesTab;
        StatusMessage.Text = "共同记忆界面检查通过；当前显示独立测试目录中的演示记录。";
        UpdateInvitationPresentation(visible.Id);
        RenderPreview(Path.ChangeExtension(Path.GetFullPath(reportPath), ".invitation.png"));
        _petWindow.RenderInvitationPreview(Path.ChangeExtension(Path.GetFullPath(reportPath), ".pet-invitation.png"));
        UpdateInvitationPresentation(null);
    }
}
