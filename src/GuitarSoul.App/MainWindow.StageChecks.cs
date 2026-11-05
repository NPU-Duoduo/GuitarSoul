using System.IO;
using System.Windows;
using System.Windows.Controls;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

public partial class MainWindow
{
    // Isolated synthetic observations exercise the same detector/save/reveal path; no device is opened.
    private void RunStageUiChecks(string reportPath, Action<bool, string> check)
    {
        string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "stage-ui-" + Guid.NewGuid().ToString("N"));
        _stageAchievements = new StageAchievements(Path.Combine(folder, "achievements.json"));
        _stageDetector.Reset(); _unsavedAchievements.Clear(); _achievementNotices.Clear();
        RefreshStages();
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Feed(Func<double, PlayingExpression> expression, int ticks, bool eligible = true, bool active = true, bool productionGate = false,
            Func<double, bool>? activity = null, Func<double, DynamicContour>? dynamics = null,
            Func<double, long>? onsets = null, Func<double, double>? pulseConfidence = null)
        {
            for (int i = 0; i <= ticks; i++)
            {
                double time = i / 10d;
                double pulse = pulseConfidence?.Invoke(time) ?? 0;
                var rhythm = new RhythmFrame(time, time, onsets?.Invoke(time) ?? i * 4 / 10,
                    pulse >= .6 ? 120 : null, pulse) { BeatConfidence = pulse, Listening = new(ToneColor.Warm, expression(time), 1)
                    { Dynamics = dynamics?.Invoke(time) ?? DynamicContour.Steady } };
                if (productionGate) ProcessStageAchievements(rhythm, activity?.Invoke(time) ?? active, false, true);
                else ObserveStageAchievements(rhythm, eligible, activity?.Invoke(time) ?? active);
            }
        }
        bool AllLocked() => StageSelector.Items.Cast<AchievementRow>().All(x => x.Locked && x.Title == "？？？" &&
            !x.AccessibleName.Contains("Slash") && !x.AccessibleName.Contains("Hendrix") && !x.AccessibleName.Contains("摇滚老炮"));
        check(AllLocked() && StageCollectionProgress.Value == 0, "Unseen achievements hide character names, titles and accessible labels");
        foreach (var choice in StageChoices)
        {
            StageSelector.SelectedItem = StageSelector.Items.Cast<AchievementRow>().Single(x => x.Style == choice.Style);
            Click(UseStageButton);
            StageMotionPreview.IsChecked = true;
            check(StageDetail.Visibility == Visibility.Collapsed && !StagePreview.DemoMotion &&
                !StagePreview.IsJamming && StageCharacterName.Text == "？？？" && _stageAchievements.Selected == StageStyle.Everyday,
                "Locked browsing cannot preview or equip the hidden shape: " + choice.Style);
            var portrait = StagePortrait.Portrait(choice.Style, true);
            var pixels = new byte[portrait.PixelWidth * portrait.PixelHeight * 4]; portrait.CopyPixels(pixels, portrait.PixelWidth * 4, 0);
            bool grey = true, visible = false;
            for (int p = 0; p < pixels.Length; p += 4)
            { if (pixels[p + 3] > 0) visible = true; if (pixels[p] != pixels[p + 1] || pixels[p] != pixels[p + 2]) grey = false; }
            check(grey && visible, "Locked portrait is an actual opaque grey silhouette: " + choice.Style);
        }
        StageMotionPreview.IsChecked = false;
        StageSelector.SelectedIndex = 0;
        Feed(_ => PlayingExpression.Animated, 160, productionGate: true);
        check(AllLocked() && !File.Exists(_stageAchievements.Path), "Stopped capture and preview cannot award even when synthetic features are dense");
        Feed(_ => PlayingExpression.Animated, 160, eligible: false);
        Feed(_ => PlayingExpression.Animated, 160, active: false);
        check(_stageAchievements.Earned.Count == 0, "Ineligible source or silence leaves the achievement book locked");
        RenderTab(MainTabs.Items.IndexOf(StagesTab), Path.ChangeExtension(reportPath, ".achievement-locked.png"));
        double width = Width, height = Height; Width = MinWidth; Height = MinHeight;
        RenderPreview(Path.ChangeExtension(reportPath, ".achievement-compact.png"));
        Width = width; Height = height;

        _stageDetector.Reset();
        _petWindow.SetInvitation("测试用旧记忆邀请");
        Feed(t => t % 7 < 3 ? PlayingExpression.Animated : PlayingExpression.Sustained, 140);
        _achievementTimer?.Stop();
        AchievementToast.BeginAnimation(OpacityProperty, null);
        check(_stageAchievements.Earned.Count == 1 && _stageAchievements.IsUnlocked(StageStyle.HardRock) &&
            AchievementToastTitle.Text == "解锁成就 · 摇滚老炮" && AchievementToastCharacter.Text.Contains("Slash") &&
            AchievementToast.Visibility == Visibility.Visible && _petWindow.DisplayedReaction?.Contains("摇滚老炮") == true,
            "A first detected musical event persists, reveals Slash, and announces the named achievement on panel and pet");
        check(CatPreview.Stage == StageStyle.Everyday && !CatPreview.IsJamming && !_capture.IsCapturing && !_capture.IsRecording,
            "Unlock notification does not force a pose, equip a cosmetic, open capture or record audio");
        RenderPreview(Path.ChangeExtension(reportPath, ".achievement-unlock.png"));
        _petWindow.RenderInvitationPreview(Path.ChangeExtension(reportPath, ".achievement-pet.png"));
        DismissAchievement_Click(this, new RoutedEventArgs());
        check(_petWindow.DisplayedReaction == "测试用旧记忆邀请", "Achievement dismissal restores the earlier memory invitation");
        _petWindow.SetInvitation(null);
        Feed(t => t % 7 < 3 ? PlayingExpression.Animated : PlayingExpression.Sustained, 140);
        check(_stageAchievements.Earned.Count == 1 && AchievementToast.Visibility == Visibility.Collapsed,
            "Repeated musical events cannot duplicate rewards or notifications");
        StageSelector.SelectedIndex = 0;
        StageMotionPreview.IsChecked = true;
        check(StagePreview.IsJamming && StagePreview.DemoMotion && StagePreview.Stage == StageStyle.HardRock,
            "An earned portrait now permits its motion preview");
        ReduceMotion.IsChecked = true;
        check(StagePreview.ReducedMotion, "Earned previews honour reduced motion");
        ReduceMotion.IsChecked = false;
        StageMotionPreview.IsChecked = false;
        Click(UseStageButton);
        check(CatPreview.Stage == StageStyle.HardRock && _petWindow.CurrentStage == StageStyle.HardRock && !CatPreview.IsJamming &&
            new StageAchievements(_stageAchievements.Path).Selected == StageStyle.HardRock,
            "Only an earned shape equips, synchronizes both cats, and survives reopening");
        RenderTab(3, Path.ChangeExtension(reportPath, ".achievement-owned.png"));
        StageDetail.BringIntoView(); StageScroll.UpdateLayout();
        RenderPreview(Path.ChangeExtension(reportPath, ".achievement-detail.png"));
        StageScroll.ScrollToTop();

        QuietMode.IsChecked = true;
        _stageDetector.Reset();
        Feed(t => t < 2.5 ? PlayingExpression.Sustained : t < 5 ? PlayingExpression.Flowing
            : t < 7.5 ? PlayingExpression.Animated : t < 10 ? PlayingExpression.Flowing : PlayingExpression.Sustained, 140);
        _achievementTimer?.Stop();
        check(_stageAchievements.IsUnlocked(StageStyle.Psychedelic) && AchievementToastTitle.Text == "解锁成就 · 反手琴才够味" &&
            _petWindow.DisplayedReaction is null && _stageAchievements.Selected == StageStyle.HardRock,
            "Natural phrase alternation reveals Jimi Hendrix while quiet mode suppresses pet interruptions and keeps equipped shape");
        DismissAchievement_Click(this, new RoutedEventArgs());
        QuietMode.IsChecked = false;
        _stageDetector.Reset(); Feed(_ => PlayingExpression.Sustained, 140,
            activity: t => t % 3.4 < 3, onsets: t => (long)(t / 3.4) + 1);
        _achievementTimer?.Stop();
        check(_stageAchievements.Earned.Count == 3 && AchievementToastTitle.Text == "解锁成就 · 余音未了" && StageCollectionProgress.Value == 3,
            "Long-note playing unlocks the third achievement without a challenge selection or manual confirmation");
        DismissAchievement_Click(this, new RoutedEventArgs());
        RefreshStages();
        check(StageSelector.Items.Cast<AchievementRow>().Count(x => !x.Locked) == 3 && StageCollectionProgress.Maximum == 9,
            "Original three discoveries leave six new artists hidden in the nine-entry collection");
        void Reveal(StageStyle style, Action perform)
        {
            _stageDetector.Reset(); perform();
            while (_announcedAchievement is not null) DismissAchievement_Click(this, new RoutedEventArgs());
            check(_stageAchievements.IsUnlocked(style), "Natural feature event reveals the additional artist: " + style);
            StageSelector.SelectedItem = StageSelector.Items.Cast<AchievementRow>().Single(x => x.Style == style);
            Click(UseStageButton);
            check(CatPreview.Stage == style && _petWindow.CurrentStage == style && StageDetail.Visibility == Visibility.Visible,
                $"Newly earned artist can be inspected and equipped on both cats: {style} (panel={CatPreview.Stage}, pet={_petWindow.CurrentStage}, detail={StageDetail.Visibility})");
        }
        Reveal(StageStyle.JimmyPage, () => Feed(t => t < 5 ? PlayingExpression.Animated : t < 10 ? PlayingExpression.Flowing : PlayingExpression.Sustained, 150));
        Reveal(StageStyle.DavidGilmour, () => Feed(_ => PlayingExpression.Sustained, 240,
            activity: t => t % 6.4 < 6, onsets: t => (long)(t / 6.4) + 1));
        Reveal(StageStyle.BBKing, () => Feed(_ => PlayingExpression.Sustained, 180,
            activity: t => t % 4 < 3.1, onsets: t => (long)(t / 4) + 1));
        Reveal(StageStyle.AngusYoung, () => Feed(_ => PlayingExpression.Animated, 180, activity: t => t % 4 < 3.1));
        Reveal(StageStyle.KurtCobain, () => Feed(t => t < 5 ? PlayingExpression.Sustained : t < 12 ? PlayingExpression.Animated : PlayingExpression.Sustained, 180,
            dynamics: t => t < 9 ? DynamicContour.Building : DynamicContour.Easing));
        Reveal(StageStyle.NileRodgers, () => Feed(_ => PlayingExpression.Flowing, 200,
            onsets: t => (long)(t * 2), pulseConfidence: _ => .9));
        check(_stageAchievements.Earned.Count == 9 && StageSelector.Items.Cast<AchievementRow>().All(x => !x.Locked),
            "All nine discoveries persist with independent portraits and titles");
        StageScroll.ScrollToTop();
        RenderTab(MainTabs.Items.IndexOf(StagesTab), Path.ChangeExtension(reportPath, ".achievement-complete.png"));
        Click(RestoreStageButton);
        check(CatPreview.Stage == StageStyle.Everyday && _petWindow.CurrentStage == StageStyle.Everyday && _stageAchievements.Earned.Count == 9,
            "Restoring everyday appearance keeps all earned achievements");
        StopListening("成就检查完成；只有隔离的合成特征，没有采集或录音。");
        MainTabs.SelectedIndex = 0;
    }
}
