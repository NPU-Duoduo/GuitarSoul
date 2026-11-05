using System.Drawing;
using System.Windows;
using GuitarSoul.Core.Language;
using Forms = System.Windows.Forms;

namespace GuitarSoul.App;

public partial class App : Application
{
    private Forms.NotifyIcon? _tray;
    private Icon? _applicationIcon;
    private MainWindow? _mainWindow;
    private SingleInstanceCoordinator? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var languageCheckIndex = Array.IndexOf(e.Args, "--local-language-check");
        if (languageCheckIndex >= 0 && languageCheckIndex + 1 < e.Args.Length)
        {
            bool passed = LocalQwenLanguage.WriteDiagnosticAsync(e.Args[languageCheckIndex + 1])
                .GetAwaiter().GetResult();
            Shutdown(passed ? 0 : 2); return;
        }
        if (e.Args is ["--render-stage-gallery", var stagePath])
        {
            StageGallery.Render(stagePath);
            Shutdown(); return;
        }
        if (e.Args is ["--render-beat-replay", var beatBefore, var beatAfter, var beatPath])
        {
            RockerGallery.RenderBeatReplay(beatBefore, beatAfter, beatPath);
            Shutdown(); return;
        }
        if (e.Args is ["--render-motion-comparison", var before, var after, var comparisonPath])
        {
            RockerGallery.RenderMotionComparison(before, after, comparisonPath);
            Shutdown(); return;
        }
        var rockerIndex = Array.IndexOf(e.Args, "--render-rocker-gallery");
        if (rockerIndex >= 0 && rockerIndex + 1 < e.Args.Length)
        {
            RockerGallery.Render(e.Args[rockerIndex + 1]);
            Shutdown(); return;
        }
        var candidatesIndex = Array.IndexOf(e.Args, "--render-cat-candidates");
        if (candidatesIndex >= 0 && candidatesIndex + 1 < e.Args.Length)
        {
            CatCandidates.Render(e.Args[candidatesIndex + 1]);
            Shutdown(); return;
        }
        var instanceCheckIndex = Array.IndexOf(e.Args, "--single-instance-check-report");
        if (instanceCheckIndex >= 0 && instanceCheckIndex + 1 < e.Args.Length)
        {
            SingleInstanceCoordinator.Verify(e.Args[instanceCheckIndex + 1]);
            Shutdown(); return;
        }
        var galleryIndex = Array.IndexOf(e.Args, "--render-cat-gallery");
        if (galleryIndex >= 0 && galleryIndex + 1 < e.Args.Length)
        {
            CatGallery.Render(e.Args[galleryIndex + 1]);
            Shutdown(); return;
        }
        var version = typeof(App).Assembly.GetName().Version?.ToString(3) ?? "0.5.2";
        bool diagnostic = e.Args.Contains("--ui-check-report") || e.Args.Contains("--render-preview") ||
            e.Args.Contains("--render-tab") || e.Args.Contains("--render-game-preview") || e.Args.Contains("--offline-rehearsal");
        if (!diagnostic)
        {
            _instance = new SingleInstanceCoordinator(version);
            if (!_instance.IsPrimary)
            {
                if (!_instance.TryActivate())
                    MessageBox.Show("本版本已经在运行，但暂时未响应。请从托盘打开已有窗口。", "Guitar Soul");
                Shutdown(); return;
            }
            // Queue activation before constructing the window; a second launch can arrive during startup.
            _instance.Listen(() => Dispatcher.BeginInvoke(ShowMainWindow));
        }
        _mainWindow = new MainWindow(diagnostic);
        MainWindow = _mainWindow;
        var rehearsalIndex = Array.IndexOf(e.Args, "--offline-rehearsal");
        if (rehearsalIndex >= 0 && rehearsalIndex + 2 < e.Args.Length)
        {
            _mainWindow.RunOfflineRehearsal(e.Args[rehearsalIndex + 1], e.Args[rehearsalIndex + 2]);
            _mainWindow.Dispose(); Shutdown(); return;
        }
        var checkIndex = Array.IndexOf(e.Args, "--ui-check-report");
        if (checkIndex >= 0 && checkIndex + 1 < e.Args.Length)
        {
            _mainWindow.RunUiChecks(e.Args[checkIndex + 1]);
            _mainWindow.Dispose();
            Shutdown();
            return;
        }
        var gamePreviewIndex = Array.IndexOf(e.Args, "--render-game-preview");
        if (gamePreviewIndex >= 0 && gamePreviewIndex + 1 < e.Args.Length)
        {
            _mainWindow.RenderGamePreview(e.Args[gamePreviewIndex + 1],
                day: e.Args.Contains("--day"),
                shared: e.Args.Contains("--shared"));
            _mainWindow.Dispose();
            Shutdown();
            return;
        }
        var tabIndex = Array.IndexOf(e.Args, "--render-tab");
        if (tabIndex >= 0 && tabIndex + 2 < e.Args.Length)
        {
            _mainWindow.RenderTab(int.Parse(e.Args[tabIndex + 1]), e.Args[tabIndex + 2],
                e.Args.Contains("--demo-input"),
                e.Args.Contains("--demo-chords") ? 5 : e.Args.Contains("--demo-chords-thin") ? 2 : 0);
            _mainWindow.Dispose();
            Shutdown();
            return;
        }
        var previewIndex = Array.IndexOf(e.Args, "--render-preview");
        if (previewIndex >= 0 && previewIndex + 1 < e.Args.Length)
        {
            _mainWindow.RenderPreview(e.Args[previewIndex + 1]);
            _mainWindow.Dispose();
            Shutdown();
            return;
        }
        if (e.Args.Contains("--show-stage")) _mainWindow.ShowStages();
        _mainWindow.Show();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开陪伴面板", null, (_, _) => ShowMainWindow());
        menu.Items.Add("退出 GuitarSoul", null, (_, _) => Dispatcher.Invoke(Shutdown));
        using (var stream = GetResourceStream(new Uri("pack://application:,,,/Assets/guitar-soul-icon-v1.ico")).Stream)
        using (var icon = new Icon(stream)) _applicationIcon = (Icon)icon.Clone();
        _tray = new Forms.NotifyIcon
        {
            Text = $"GuitarSoul {version} · Duoduo_127",
            Icon = _applicationIcon,
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow is null) return;
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        _applicationIcon?.Dispose();
        _mainWindow?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
