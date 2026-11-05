using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using GuitarSoul.App.Controls;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

public partial class MainWindow
{
    private sealed record StageChoice(StageStyle Style, string Name, string Achievement, string Hint, string Instrument, string Trigger);
    private sealed record AchievementRow(StageStyle Style, bool Locked, string Title, string Subtitle, string Hint, string Status)
    { public string AccessibleName => $"{Title}，{Subtitle}，{Hint}，{Status}"; }
    private static readonly StageChoice[] StageChoices =
    [
        new(StageStyle.HardRock, "Slash", "摇滚老炮", "让快速短句冲出去，再稳稳落回一个长音。", "Les Paul · 琥珀渐变",
            "因两次快速乐句落回舒展长音的主音句法而相遇；不再由持续扫弦或单纯起音数量触发。"),
        new(StageStyle.Psychedelic, "Jimi Hendrix", "反手琴才够味", "让和弦、流动过门与热烈短句彼此穿插。", "Stratocaster · 奶油白 / 左手持琴",
            "因长音、流动与密集表达多次自然换位而相遇，借此表达节奏与主音交织，而非识别具体技法。"),
        new(StageStyle.Mellow, "清音", "余音未了", "慢慢弹几句不拖得太长的音，给每句一点呼吸。", "Stratocaster · 鼠尾草绿",
            "因至少三段从容、适中长度的长音乐句而相遇；持续无人变化的单音不会触发。"),
        new(StageStyle.JimmyPage, "Jimmy Page", "一把琴，两副嗓子", "先让 riff 推进，经过流动的过门，再用长音收束。", "EDS-1275 · 樱桃红双颈",
            "因密集 riff、流动过门、长音收束按顺序形成完整段落而相遇，不再靠集齐三种标签。"),
        new(StageStyle.DavidGilmour, "David Gilmour", "这一音，还不想结束", "让几个独立的长音都真正展开，别急着追下一个。", "Black Strat · 黑琴体 / 黑护板",
            "因至少三段充分延展的长音乐句而相遇；它与适中长音的清音形态使用不同段落长度。"),
        new(StageStyle.BBKing, "B.B. King", "留白也是一句话", "弹一句，停一会儿，再让下一句回答。", "Lucille · 黑色 / 金色硬件",
            "因多段长音乐句之间自然的留白而相遇。音与音之间，也有故事。"),
        new(StageStyle.AngusYoung, "Angus Young", "下课了，开摇！", "短促热烈的几段 riff，中间喘口气，再来。", "SG · 樱桃红 / 尖双角",
            "因几段密集 riff 与短暂停顿的接力而相遇。热烈可以一阵一阵来。"),
        new(StageStyle.KurtCobain, "Kurt Cobain", "下一段，动静大点", "从安静展开，冲进密集段落，再回到余音。", "Mustang · 湖蓝 / 左手持琴",
            "因安静—爆发—回落的完整动态弧线而相遇，而不是一次普通的音量变化；不必提高设备总音量。"),
        new(StageStyle.NileRodgers, "Nile Rodgers", "跟着晃就对了", "不急不抢，让接连的拨弦贴着稳定脉冲流动。", "Hitmaker Strat · 白色 / 银色护板",
            "因一段持续流动且脉冲稳定的节奏拨弦而相遇；普通无脉冲的分解和弦不再足够。")
    ];
    private StageAchievements? _stageAchievements;
    private readonly StageAchievementDetector _stageDetector = new();
    private readonly Dictionary<StageStyle, StageTrigger> _unsavedAchievements = new();
    private readonly Queue<StageStyle> _achievementNotices = new();
    private DispatcherTimer? _achievementTimer;
    private StageStyle? _announcedAchievement;
    private bool _stagesReady, _refreshingStages, _achievementSaveFailed;
    private StageChoice SelectedStage => StageChoices.FirstOrDefault(x => x.Style == (StageSelector.SelectedItem as AchievementRow)?.Style) ?? StageChoices[0];
    internal void ShowStages() => MainTabs.SelectedItem = StagesTab;

    private void InitializeStages(bool isolated)
    {
        string folder = isolated ? Path.Combine(Path.GetTempPath(), "GuitarSoul-stage-preview-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guitar Soul");
        try { _stageAchievements = new StageAchievements(Path.Combine(folder, "stage-achievements.json")); }
        catch (Exception e) { StageFeedback.Text = "成就暂时无法读取，原文件已保留。请重新打开程序后再试。\n" + e.Message; }
        // Self-reported notes are retained but cannot become event-earned achievements.
        if (!isolated && File.Exists(Path.Combine(folder, "stage-collection.json")))
        {
            LegacyStageHistory.Visibility = Visibility.Visible;
            try
            {
                var legacy = new StageCollection(Path.Combine(folder, "stage-collection.json"));
                LegacyStageNotes.Text = "旧版记录完整保留；成就从实际演奏中重新发现。\n" +
                    string.Join("\n", legacy.Entries.Select(x => $"{x.CreatedAt:yyyy-MM-dd} · {x.Title}"));
            }
            catch (Exception e) { LegacyStageNotes.Text = "旧版文件已保留，本次未读取：" + e.Message; }
        }
        _achievementTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        _achievementTimer.Tick += (_, _) => DismissAchievement_Click(this, new RoutedEventArgs());
        _petWindow.AchievementOpenRequested += (_, _) => OpenAchievement_Click(this, new RoutedEventArgs());
        _stagesReady = true;
        RefreshStages();
    }

    private void Stage_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_stagesReady || _refreshingStages) return;
        RefreshStageDetail();
        if (StageDetail.Visibility == Visibility.Visible) StageDetail.BringIntoView();
    }
    private void StageMotion_Changed(object sender, RoutedEventArgs e)
    { if (_stagesReady) UpdateStagePreview(); }
    private void UpdateStagePreview()
    {
        if (!_stagesReady) return;
        bool owned = _stageAchievements?.IsUnlocked(SelectedStage.Style) == true;
        StagePreview.Skin = _petPreferences.Skin;
        StagePreview.Stage = owned ? SelectedStage.Style : StageStyle.Everyday;
        StagePreview.Guitar = owned ? StageLook.Guitar(SelectedStage.Style, _petPreferences.Guitar) : GuitarStyle.None;
        StagePreview.IsJamming = owned;
        StagePreview.Mood = CompanionMood.Excited;
        StagePreview.DemoMotion = owned && StageMotionPreview.IsChecked == true;
        StagePreview.PreviewSeconds = StagePreview.DemoMotion ? null : .12;
        StagePreview.PreviewPulse = StagePreview.DemoMotion ? null : .8;
        StagePreview.MotionAmount = _petPreferences.MotionAmount;
        StagePreview.ReducedMotion = _petPreferences.ReducedMotion || _petPreferences.QuietMode;
        StagePreview.InvalidateVisual();
        if (_petPreferences.QuietMode) _petWindow.SetAchievement(null);
    }
    private void RefreshStageDetail()
    {
        var choice = SelectedStage;
        var earned = _stageAchievements?.Earned.FirstOrDefault(x => x.Style == choice.Style);
        bool owned = earned is not null;
        StageDetail.Visibility = owned ? Visibility.Visible : Visibility.Collapsed;
        // Hidden detail text is cleared too: automation/tooltips must not reveal locked identities.
        StageCharacterName.Text = owned ? choice.Name : "？？？";
        StageInstrument.Text = owned ? choice.Instrument : "";
        StageDescription.Text = owned ? choice.Trigger : "";
        StageUnlockedDate.Text = earned is null ? "" : $"相遇于 {earned.UnlockedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        UseStageButton.IsEnabled = owned && _stageAchievements!.Selected != choice.Style;
        UseStageButton.Content = _stageAchievements?.Selected == choice.Style ? "一起弹时正在使用" : "一起弹时用这套";
        UpdateStagePreview();
    }
    private void RefreshStages()
    {
        var selected = SelectedStage.Style;
        _refreshingStages = true;
        StageSelector.ItemsSource = StageChoices.Select(choice =>
        {
            bool owned = _stageAchievements?.IsUnlocked(choice.Style) == true;
            return new AchievementRow(choice.Style, !owned, owned ? choice.Achievement : "？？？",
                owned ? choice.Name : "尚未发现", owned ? choice.Instrument.Replace(" / ", "\n") : choice.Hint, owned ? "已解锁" : "未解锁");
        }).ToArray();
        StageSelector.SelectedItem = StageSelector.Items.Cast<AchievementRow>().Single(x => x.Style == selected);
        _refreshingStages = false;
        var active = _stageAchievements?.Selected ?? StageStyle.Everyday;
        CatPreview.Stage = active; _petWindow.SetStage(active);
        StageActive.Text = active == StageStyle.Everyday ? "当前：日常形态" : $"当前：{StageChoices.Single(x => x.Style == active).Name} · 一起弹时登场";
        StageGuitarHint.Text = active == StageStyle.Everyday ? "演奏中有机会解锁新的舞台形象，去「成就」看看。"
            : $"当前使用「{StageChoices.Single(x => x.Style == active).Name}」的配套琴；恢复日常形态后使用这里选的琴。";
        RestoreStageButton.Visibility = active == StageStyle.Everyday ? Visibility.Collapsed : Visibility.Visible;
        RestoreStageButton.IsEnabled = active != StageStyle.Everyday;
        int count = _stageAchievements?.Earned.Count ?? 0;
        StageCollectionProgress.Maximum = StageChoices.Length;
        StageCollectionStatus.Text = $"已发现 {count} / {StageChoices.Length}";
        StageCollectionProgress.Value = count;
        RefreshStageDetail();
    }
    private void UseStage_Click(object sender, RoutedEventArgs e)
    {
        if (_stageAchievements?.IsUnlocked(SelectedStage.Style) != true) return;
        try
        {
            _stageAchievements.Select(SelectedStage.Style); RefreshStages();
            StageFeedback.Text = $"已选择 {SelectedStage.Name}，它会在一起弹时登场。";
        }
        catch (Exception ex) { StageFeedback.Text = "形态未切换：" + ex.Message; }
    }
    private void RestoreStage_Click(object sender, RoutedEventArgs e)
    {
        try { _stageAchievements?.Select(StageStyle.Everyday); RefreshStages(); StageFeedback.Text = "已恢复日常形态，成就一直都在。"; }
        catch (Exception ex) { StageFeedback.Text = "形态未切换：" + ex.Message; }
    }

    // Called by the real capture callback, not the animation/preview clock.
    private void ProcessStageAchievements(RhythmFrame rhythm, bool active, bool clipping, bool fresh)
    {
        bool eligible = _capture.IsCapturing && SelectedSourceKind == AudioSourceKind.GuitarInput && fresh &&
            _calibration is null && !IsKeepsakeReviewPlaying && PreviewMotion.IsChecked != true && !clipping;
        ObserveStageAchievements(rhythm, eligible, active);
    }
    private void ObserveStageAchievements(RhythmFrame rhythm, bool eligible, bool active)
    {
        if (_stageAchievements is null) return;
        var triggered = _stageDetector.Observe(rhythm, eligible, active, _stageAchievements.IsUnlocked);
        foreach (var trigger in triggered)
            if (!_stageAchievements.IsUnlocked(trigger.Style)) _unsavedAchievements.TryAdd(trigger.Style, trigger);
        if (!_achievementSaveFailed && _unsavedAchievements.Count > 0) SaveTriggeredAchievements();
    }
    private void SaveTriggeredAchievements()
    {
        if (_stageAchievements is null) return;
        try
        {
            foreach (var trigger in _unsavedAchievements.Values.ToArray())
            {
                if (_stageAchievements.Award(trigger, DateTimeOffset.Now)) _achievementNotices.Enqueue(trigger.Style);
                _unsavedAchievements.Remove(trigger.Style);
            }
            _achievementSaveFailed = false;
            RetryAchievementButton.Visibility = Visibility.Collapsed;
            StageFeedback.Text = "";
        }
        catch (Exception e)
        {
            _achievementSaveFailed = true;
            StageFeedback.Text = "发现了新成就，但尚未保存。请重试保存，或重开程序后再演奏一次。\n" + e.Message;
            StatusMessage.Text = "成就尚未保存，请到「成就」页重试。";
            RetryAchievementButton.Visibility = Visibility.Visible;
        }
        RefreshStages();
        ShowNextAchievement();
    }
    private void RetryAchievement_Click(object sender, RoutedEventArgs e)
    {
        // Reload merges a concurrent writer; no old snapshot can overwrite their achievements.
        try
        {
            if (_stageAchievements is null) return;
            _stageAchievements = new StageAchievements(_stageAchievements.Path);
            SaveTriggeredAchievements();
        }
        catch (Exception ex) { StageFeedback.Text = "仍未保存，原文件已保留：" + ex.Message; }
    }
    private void ShowNextAchievement()
    {
        if (_announcedAchievement is not null || !_achievementNotices.TryDequeue(out var style)) return;
        _announcedAchievement = style;
        var choice = StageChoices.Single(x => x.Style == style);
        AchievementToastPortrait.Stage = style;
        AchievementToastPortrait.IsLocked = false;
        AchievementToastTitle.Text = $"解锁成就 · {choice.Achievement}";
        AchievementToastCharacter.Text = $"{choice.Name} 已加入图鉴 · 这次演奏，被它记住了。";
        AchievementToast.Visibility = Visibility.Visible;
        if (!_petPreferences.ReducedMotion && !_petPreferences.QuietMode && SystemParameters.ClientAreaAnimation)
            AchievementToast.BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(240)));
        if (!_petPreferences.QuietMode) _petWindow.SetAchievement($"解锁成就「{choice.Achievement}」\n{choice.Name} 已加入图鉴");
        _achievementTimer?.Start();
    }
    private void DismissAchievement_Click(object sender, RoutedEventArgs e)
    {
        _achievementTimer?.Stop();
        AchievementToast.BeginAnimation(OpacityProperty, null);
        AchievementToast.Visibility = Visibility.Collapsed;
        _announcedAchievement = null;
        _petWindow.SetAchievement(null);
        ShowNextAchievement();
    }
    private void OpenAchievement_Click(object sender, RoutedEventArgs e)
    {
        var style = _announcedAchievement;
        DismissAchievement_Click(sender, e);
        ShowPanel(); ShowStages();
        if (style.HasValue)
            StageSelector.SelectedItem = StageSelector.Items.Cast<AchievementRow>().Single(x => x.Style == style);
        StageDetail.BringIntoView();
    }
}
