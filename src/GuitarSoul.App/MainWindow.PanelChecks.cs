using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuitarSoul.App.Controls;
using GuitarSoul.Audio.Windows;
using GuitarSoul.Core.Audio;

namespace GuitarSoul.App;

public partial class MainWindow
{
    // Exercises disclosure, navigation and viewport reachability on isolated controls.
    // Fixture devices and playback labels never open audio or write the user's settings.
    private void RunPanelUiChecks(string reportPath, Action<bool, string> check)
    {
        var warning = new InputOverloadWarning();
        float[] Tone(int rate,double amplitude) => Enumerable.Range(0,rate).Select(i=>(float)Math.Clamp(amplitude*Math.Sin(2*Math.PI*440*i/rate),-1,1)).ToArray();
        void Feed(float[] data,int rate,int block) { for(int p=0;p<data.Length;p+=block) warning.Observe(data.AsSpan(p,Math.Min(block,data.Length-p)),rate); }
        var oldNearRail = new SignalAnalyzer().Process(Tone(48000,.9995));
        Feed(Tone(48000,.9995),48000,1920);
        check(oldNearRail.IsClipping&&!warning.IsVisible,"Reproduced old near-rail flag no longer becomes a persistent warning for an unclipped tone");
        foreach(int rate in new[]{44100,48000,96000})
        {
            warning.Reset();Feed(Tone(rate,.9995),rate,rate/25);
            check(!warning.IsVisible&&!warning.HasFlatTop,$"Near-full-scale clean tone at {rate} Hz does not trigger overload");
            warning.Reset();Feed(Tone(rate,1),rate,127);
            check(!warning.IsVisible,$"Clean full-scale sine at {rate} Hz is not confused with a clipped plateau");
            var sparse=Tone(rate,.4);for(int i=0;i<rate;i+=rate/25)sparse[i]=1;
            warning.Reset();Feed(sparse,rate,rate/25);
            check(!warning.IsVisible,$"One rail sample in every callback at {rate} Hz does not count as full-block overload");
            warning.Reset();Feed(Tone(rate,1.35),rate,127);
            check(warning.IsVisible&&warning.RailFraction>.1,$"Hard-clipped tone at {rate} Hz raises an evidence-based warning");
            Feed(Tone(rate,.4),rate,rate/25);
            check(!warning.IsVisible&&!warning.HasFlatTop,$"Clean input clears the warning and clip lamp at {rate} Hz");
        }
        Feed(Tone(48000,1.4),48000,1920); warning.Reset();
        warning.Observe([float.NaN,float.PositiveInfinity],48000);
        check(!warning.IsVisible&&!warning.HasFlatTop,"Reset and invalid samples cannot retain an overload warning");
        var vu=new RackVuMeter { Level=60 };
        vu.Advance(.1,0);
        check(vu.DisplayedLevel>0&&vu.DisplayedLevel<60&&vu.HeldLevel==60,
            "Input meter falls smoothly while retaining the preceding real peak");
        for(int i=0;i<24;i++) vu.Advance(.1,0);
        check(vu.DisplayedLevel<.2&&vu.HeldLevel<40,
            "Input meter and held peak decay after silence instead of oscillating on their own");
        vu.ReducedMotion=true;vu.Level=42;
        check(vu.DisplayedLevel==42&&vu.HeldLevel==42&&
            System.Windows.Automation.AutomationProperties.GetItemStatus(vu).Contains("-18"),
            "Reduced-motion meter immediately shows the measured -18 dBFS input");
        vu.IsClipping=true;vu.Reset();
        check(!vu.IsClipping&&vu.HeldLevel==0&&vu.DisplayedLevel==0,
            "Stopping input clears the clipping indicator, held peak and needle");
        vu.Level=double.NaN;
        check(vu.Level==0,"Non-finite meter input safely returns to silence");

        // Compare actual rendered pixels, not animation flags: motion must change the image,
        // and reduced motion must produce identical pixels at different animation times.
        var room=new RehearsalRoomBackdrop
        {
            PreviewLocalTime=new DateTimeOffset(2026,10,29,21,0,0,TimeSpan.FromHours(8)),
            PreviewMoonPhase=.36
        };
        room.Measure(new Size(624,680));
        room.Arrange(new Rect(0,0,624,680));
        byte[] RoomPixels(double seconds)
        {
            room.PreviewAnimationSeconds=seconds;
            room.UpdateLayout();
            var bitmap=new RenderTargetBitmap(624,680,96,96,PixelFormats.Pbgra32);
            bitmap.Render(room);
            byte[] pixels=new byte[624*680*4];
            bitmap.CopyPixels(pixels,624*4,0);
            return pixels;
        }
        var motionStart=RoomPixels(0);
        var motionLater=RoomPixels(10);
        check(!motionStart.SequenceEqual(motionLater),
            "Rendered room foliage and dust change with animation time");
        bool RegionChanged(int left,int top,int right,int bottom)
        {
            for(int y=top;y<bottom;y++) for(int x=left;x<right;x++)
            {
                int p=(y*624+x)*4;
                if(motionStart[p]!=motionLater[p]||motionStart[p+1]!=motionLater[p+1]||
                    motionStart[p+2]!=motionLater[p+2]) return true;
            }
            return false;
        }
        check(RegionChanged(175,24,245,100)&&RegionChanged(8,480,85,620),
            "Painted hanging vines and near leaves both move in their own image regions");
        check(!RegionChanged(375,285,490,375),
            "The amplifier stays pixel-stable while local foliage moves");
        void SaveMotionFrame(byte[] pixels,string suffix)
        {
            var frame=BitmapSource.Create(624,680,96,96,PixelFormats.Pbgra32,null,pixels,624*4);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame));
            string path=Path.ChangeExtension(reportPath,".room-motion-"+suffix+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream=File.Create(path);encoder.Save(stream);
        }
        SaveMotionFrame(motionStart,"0s");SaveMotionFrame(motionLater,"10s");
        room.ReducedMotion=true;
        check(RoomPixels(0).SequenceEqual(RoomPixels(10)),
            "Reduced motion freezes actual room pixels across animation time");
        byte[] WindowPixels(byte[] pixels)
        {
            var clock=new List<byte>();
            for(int y=4;y<165;y++) for(int x=407;x<520;x++)
            {
                int index=(y*624+x)*4;
                clock.Add(pixels[index]);clock.Add(pixels[index+1]);clock.Add(pixels[index+2]);
            }
            return clock.ToArray();
        }
        room.PreviewLocalTime=new DateTimeOffset(2026,10,29,3,0,0,TimeSpan.FromHours(8));
        var beforeDawn=WindowPixels(RoomPixels(0));
        bool dawnShowsMoon=room.Description.Contains("月",StringComparison.Ordinal);
        room.PreviewLocalTime=new DateTimeOffset(2026,10,29,14,0,0,TimeSpan.FromHours(8));
        var daylight=WindowPixels(RoomPixels(0));
        room.PreviewLocalTime=new DateTimeOffset(2026,10,29,21,0,0,TimeSpan.FromHours(8));
        var evening=WindowPixels(RoomPixels(0));
        check(!daylight.SequenceEqual(beforeDawn)&&!daylight.SequenceEqual(evening)&&
            dawnShowsMoon&&room.Description.Contains("月",StringComparison.Ordinal),
            "Rendered window changes between daylight and night, including hours before dawn");
        RunRoomCycleChecks(reportPath, check);
        string ImagePath(string state) => Path.ChangeExtension(reportPath, ".panel-" + state + ".png");
        bool InViewport(FrameworkElement element)
        {
            var bounds = element.TransformToAncestor(RackScroll)
                .TransformBounds(new Rect(element.RenderSize));
            return bounds.Top >= -1 && bounds.Left >= -1 &&
                bounds.Bottom <= RackScroll.ActualHeight + 1 && bounds.Right <= RackScroll.ActualWidth + 1;
        }
        var originalSize = new Size(Width, Height);
        SourceKindSelector.SelectedIndex = 0;
        _inputPreferences = null;
        InputDevices.SelectedItem = null;
        _inputPickerOpen = false;
        UpdateInputSelection();
        MainTabs.SelectedIndex = 0;
        PerformanceDetails.IsExpanded = SoundDetails.IsExpanded = false;
        DeviceSettings.IsExpanded = false;
        UnloadTrack("选好输入后，点「开始倾听」。", true);
        RefreshCompanion();
        RefreshPerformanceExperience();
        RenderPreview(ImagePath("first-run"));
        check(string.IsNullOrEmpty(TrackName.Text) && StageMomentBadge.Visibility == Visibility.Collapsed &&
            InputMeterSection.Visibility == Visibility.Visible && InputVu.ActualHeight > 100,
            "VU remains visible before input configuration without inventing a measured level");
        // The card is a scrolling column now, so the contract is narrower and honest:
        // setup, the primary action and the accompaniment entry never need a scroll.
        check(!ListenButton.IsEnabled && InputPicker.Visibility == Visibility.Visible &&
            RackScroll.IsAncestorOf(ProgressionDetails) && !SoundDetails.IsExpanded &&
            InViewport(ListenButton) && InViewport(ChooseTrackButton),
            "First run keeps setup, the primary action and accompaniment in view");

        // A room viewport of 864x1064 matches the selected reference's left region,
        // so art direction can be compared without a misleading crop/aspect change.
        Width=1344;Height=1064;
        RenderPreview(ImagePath("reference-proportions"));

        Width = MinWidth; Height = MinHeight;
        RenderPreview(ImagePath("compact"));
        bool primaryActionsVisible = InViewport(ListenButton) && InViewport(ChooseTrackButton);
        bool rackNeedsScroll = RackScroll.ScrollableHeight > 0;
        RackScroll.ScrollToEnd(); UpdateLayout();
        bool detailsReachable = !RackScroll.IsAncestorOf(PerformanceDetails);
        RackScroll.ScrollToHome(); UpdateLayout();
        check(primaryActionsVisible && detailsReachable && RackScroll.ScrollableWidth == 0 &&
            (!rackNeedsScroll || RackScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto),
            "Minimum window keeps both primary actions visible and the analysis drawer reachable in the equipment rack");
        Width = originalSize.Width; Height = originalSize.Height;

        // A deliberately long device name verifies the summary yields to the change action.
        var device = new AudioDeviceInfo("panel-fixture", "QME-10 USB Audio Interface · 吉他效果器输入设备", 2, 48000, 24);
        InputDevices.ItemsSource = new[] { device };
        InputDevices.SelectedItem = device;
        StatusMessage.Text = "输入已准备好，点击开始后才会倾听。";
        RenderPreview(ImagePath("ready"));
        check(InputModule.IsAncestorOf(ListenButton) && InputModule.IsAncestorOf(InputPicker) &&
            InputModule.IsAncestorOf(InputVu) && InputMeterSection.Visibility == Visibility.Visible && InputVu.ActualHeight > 100,
            "Input selection, listening and live metering share one functional module");
        check(ListenButton.IsEnabled && InputPicker.Visibility == Visibility.Collapsed &&
            ChangeInputButton.Visibility == Visibility.Visible && InViewport(ChangeInputButton) && !_capture.IsCapturing,
            "Remembered input folds to a summary while change remains reachable and capture stays stopped");
        ChangeInputButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        RenderPreview(ImagePath("change-input"));
        check(InputPicker.Visibility == Visibility.Visible && InputDevices.IsEnabled && !_capture.IsCapturing,
            "Change input reopens configuration without starting or replacing audio");
        _inputPickerOpen = false; UpdateConnectionStatus();

        TrackName.Text = "练琴伴奏 · 带有较长名字的本地音频文件.wav";
        TrackTransport.Visibility = PlaybackOptions.Visibility = Visibility.Visible;
        PlayButton.IsEnabled = StopPlayButton.IsEnabled = Seek.IsEnabled = true;
        Seek.Maximum = 180; Seek.Value = 42; TrackTime.Text = "00:42 / 03:00";
        StatusMessage.Text = "布局演示：没有载入或播放音频。";
        RenderPreview(ImagePath("playback"));
        check(InViewport(PlayButton) && InViewport(Seek) && !PerformanceDetails.IsExpanded &&
            !RackScroll.IsAncestorOf(PlaybackOptions) && !_playback.IsPlaying,
            "Loaded transport stays on home while output configuration is outside the home rack");

        double audioTime = 0;
        foreach (int root in new[] { 0, 7, 9, 5 })
            for (int tick = 0; tick < 24; tick++, audioTime += .05)
                _progression.Observe(new HarmonyFrame(root, root == 9 ? ChordQuality.Minor : ChordQuality.Major, .95, 0), audioTime);
        RefreshProgression();
        MainTabs.SelectedIndex = 0;
        RenderPreview(ImagePath("chords"));
        check(CurrentChordName.Text == "F" && ProgressionStrip.Children.Count == 4 && SaveProgressionButton.IsEnabled && InViewport(ProgressionDetails),
            "Home shows the live chord, ordered history and save action");
        MainTabs.SelectedItem = SettingsTab;
        PerformanceDetails.IsExpanded = SoundDetails.IsExpanded = PlaybackOptions.IsExpanded = true;
        RenderPreview(ImagePath("details"));
        bool Expanded(Expander section) => section.IsExpanded &&
            section.Template.FindName("ExpandSite", section) is FrameworkElement { Visibility: Visibility.Visible };
        check(Expanded(PerformanceDetails) && Expanded(PlaybackOptions) && Expanded(SoundDetails) &&
            OutputDevices.ActualHeight > 0 && BeatStatus.ActualHeight > 0 &&
            ProgressionStrip.Children.Count > 0 && SaveProgressionButton.IsEnabled && !_capture.IsCapturing,
            "Settings retain chord drafts, input diagnostics and output configuration");
        check(FindName("AudioWave") is null && RackScroll.IsAncestorOf(ProgressionDetails),
            "Home retains chord recording and has no duplicate waveform");
        double previousVolume=Volume.Value;
        Volume.Value=.5;
        Slider.IncreaseSmall.Execute(null,Volume);
        check(Math.Abs(Volume.Value-.52)<.001&&Math.Abs(_playback.Volume-.52)<.001&&!_playback.IsPlaying&&!_capture.IsCapturing,
            "Rotary volume small-step command updates actual playback gain without opening audio");
        Slider.MaximizeValue.Execute(null,Volume);
        check(Volume.Value==Volume.Maximum&&Math.Abs(_playback.Volume-1)<.001,
            "Rotary volume maximum command reaches the real playback limit");
        Slider.MinimizeValue.Execute(null,Volume);
        check(Volume.Value==Volume.Minimum&&_playback.Volume==0,
            "Rotary volume minimum command mutes real playback gain");
        Volume.Value=previousVolume;
        PerformanceDetails.IsExpanded = SoundDetails.IsExpanded = PlaybackOptions.IsExpanded = false;
        MainTabs.SelectedIndex = 0;
        _progression.Clear(); RefreshProgression();
        UnloadTrack("输入已准备好，点击开始后才会倾听。", true);
        check(TrackTransport.Visibility == Visibility.Collapsed && PlaybackOptions.Visibility == Visibility.Collapsed,
            "Unloading returns accompaniment to its single file entry");

        RiffList.ItemsSource = Array.Empty<RiffItem>();
        RiffList.Visibility = RiffActions.Visibility = Visibility.Collapsed;
        RiffEmpty.Text = "还没有留念。开启后，你尽管弹，它会挑选一小段。";
        AudienceNote.Text = string.Empty;
        StatusMessage.Text = "准备就绪。";

        foreach (var (tab, title, name) in new[] {
            (MemoriesTab, "共同记忆", "memories"),
            (StagesTab, "演奏中的意外收获", "collection"),
            (SettingsTab, "按你的习惯来", "settings") })
        {
            MainTabs.SelectedItem = tab;
            RenderPreview(ImagePath(name));
            check(PageTitle.Text == title && PageTitle.Visibility == Visibility.Visible && !_capture.IsCapturing,
                "Navigation shows the correct heading without changing input: " + name);
        }
        check(MainTabs.Items.IndexOf(SettingsTab) == MainTabs.Items.Count - 1,
            "Infrequent settings follow the daily companion, memories and collection pages");
        MainTabs.SelectedIndex = 0;
        RackScroll.ScrollToTop();
        RefreshCompanion();
        RefreshPerformanceExperience();
        RunChordSessionChecks(reportPath, check);
    }
}
