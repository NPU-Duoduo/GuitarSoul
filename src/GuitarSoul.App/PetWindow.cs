using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GuitarSoul.App.Controls;
using GuitarSoul.Core.Companion;
using GuitarSoul.Core.Audio;
using GuitarSoul.Core.Memories;

namespace GuitarSoul.App;

/// <summary>Transparent desktop companion. Hiding it leaves the audio panel running.</summary>
public sealed class PetWindow : Window
{
    private readonly PixelCat _cat;
    private readonly Border _invitation;
    private readonly TextBlock _invitationText;
    private string? _memoryInvitation, _reaction, _achievement;
    private bool _positioned;

    public PetWindow()
    {
        Title = "Guitar Soul 桌面猫 " + typeof(PetWindow).Assembly.GetName().Version?.ToString(3);
        Width = 220;
        Height = 224;
        MinWidth = 160;
        MinHeight = 170;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        AutomationProperties.SetName(this, "Guitar Soul 桌面猫");
        _cat = new PixelCat
        {
            Cursor = Cursors.Hand,
            ToolTip = "拖动移动猫；右键打开控制面板或隐藏桌宠"
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _invitationText = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(37, 52, 44)), FontSize = 12,
            TextWrapping = TextWrapping.Wrap, MaxHeight = 48, ToolTip = "点击查看这段旧记忆；不回应也没关系。" };
        _invitation = new Border { Background = new SolidColorBrush(Color.FromRgb(244, 241, 234)),
            CornerRadius = new CornerRadius(5), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(4, 0, 4, 4),
            Child = _invitationText, Visibility = Visibility.Collapsed, Cursor = Cursors.Hand };
        _invitation.MouseLeftButtonDown += (_, e) =>
        {
            if (_achievement is not null) AchievementOpenRequested?.Invoke(this, EventArgs.Empty);
            else if (_memoryInvitation is not null) InvitationOpenRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };
        layout.Children.Add(_invitation); Grid.SetRow(_cat, 1); layout.Children.Add(_cat); Content = layout;

        var menu = new ContextMenu();
        var open = new MenuItem { Header = "打开控制面板(_O)" };
        open.Click += (_, _) => OpenPanelRequested?.Invoke(this, EventArgs.Empty);
        var hide = new MenuItem { Header = "隐藏桌宠(_H)" };
        hide.Click += (_, _) => Hide();
        menu.Items.Add(open);
        menu.Items.Add(new Separator());
        menu.Items.Add(hide);
        _cat.ContextMenu = menu;
        _cat.MouseLeftButtonDown += OnCatMouseLeftButtonDown;
        Loaded += (_, _) => EnsureVisiblePosition();
        IsVisibleChanged += (_, _) => { if (IsVisible) EnsureVisiblePosition(); };
        KeyDown += OnKeyDown;
        Closed += (_, _) => _cat.Dispose();
    }

    public event EventHandler? OpenPanelRequested;
    public event EventHandler? InvitationOpenRequested;
    public event EventHandler? AchievementOpenRequested;
    public void SetAchievement(string? text) { _achievement = text; UpdateBubble(); }
    public void SetInvitation(string? text)
    {
        _memoryInvitation = text;
        UpdateBubble();
    }
    public void SetReaction(string? text)
    {
        if (_reaction == text) return;
        _reaction = text;
        UpdateBubble();
    }
    internal string? DisplayedReaction => _achievement ?? _memoryInvitation ?? _reaction;
    private void UpdateBubble()
    {
        var text = DisplayedReaction;
        var bottom = Top + Height;
        _invitationText.Text = text ?? "";
        _invitationText.ToolTip = _achievement is not null ? "点击查看刚解锁的成就。" : _memoryInvitation is not null ? "点击查看这段旧记忆；不回应也没关系。" : "它听到这段琴声时的小小回应。";
        _invitation.Cursor = _achievement is not null || _memoryInvitation is not null ? Cursors.Hand : Cursors.Arrow;
        AutomationProperties.SetName(_invitation, _achievement is not null ? "新成就" : _memoryInvitation is not null ? "旧记忆邀请" : "猫对琴声的回应");
        _invitation.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        Height = text is null ? 224 : 284;
        if (_positioned && double.IsFinite(bottom)) Top = bottom - Height;
        if (IsVisible) EnsureVisiblePosition();
    }
    public void SetMood(CompanionMood mood) => _cat.Mood = mood;
    internal void RenderInvitationPreview(string path)
    {
        var content = (FrameworkElement)Content;
        content.Measure(new Size(Width, Height)); content.Arrange(new Rect(0, 0, Width, Height)); content.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path); encoder.Save(stream);
    }
    public void SetPose(CompanionPose pose) => _cat.SetPose(pose);
    public void ClearPose() => _cat.ClearPose();
    internal GuitarStyle CurrentGuitar => _cat.Guitar;
    internal bool IsJamming => _cat.IsJamming;
    public void SetJamming(bool jamming) => _cat.IsJamming = jamming;
    internal StageStyle CurrentStage => _cat.Stage;
    internal void SetStage(StageStyle stage) => _cat.Stage = stage;
    internal void SetAppearance(PetPreferences preferences)
    {
        _cat.Skin = preferences.Skin;
        _cat.Guitar = preferences.Guitar;
        _cat.MotionAmount = preferences.MotionAmount;
        _cat.ReducedMotion = preferences.ReducedMotion;
        _cat.InvalidateVisual();
    }
    public void SetRhythm(RhythmFrame rhythm, long timestamp) => _cat.SetRhythm(rhythm, timestamp);
    public void SetDemo(bool enabled) => _cat.DemoMotion = enabled;
    public void SetPreview(double? seconds, double? pulse)
    {
        _cat.PreviewSeconds = seconds; _cat.PreviewPulse = pulse; _cat.InvalidateVisual();
    }

    public void SetListening(bool listening, double level = 0)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(() => SetListening(listening, level));
            return;
        }
        _cat.IsListening = listening;
        _cat.Level = level;
    }

    private void OnCatMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            OpenPanelRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
            return;
        // DragMove returns once the pointer is released. Clamp the resulting position to
        // the primary work area so the prototype remains recoverable after display changes.
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // A mouse release can arrive between the pressed-state check and DragMove.
        }
        EnsureVisiblePosition();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            OpenPanelRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void EnsureVisiblePosition()
    {
        var area = SystemParameters.WorkArea;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (!_positioned)
        {
            Left = area.Right - width - 24;
            Top = area.Bottom - height - 8;
            _positioned = true;
        }

        Left = Math.Clamp(double.IsFinite(Left) ? Left : area.Left, area.Left,
            Math.Max(area.Left, area.Right - width));
        Top = Math.Clamp(double.IsFinite(Top) ? Top : area.Top, area.Top,
            Math.Max(area.Top, area.Bottom - height));
    }
}
