using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using NoChat.App.Assets;
using NoChat.App.Logging;
using NoChat.App.Settings;

namespace NoChat.App;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private WindowIcon? _trayIconNormal;
    private WindowIcon? _trayIconAlert;
    private DateTime _lastTrayClickTime = DateTime.MinValue;
    private const int DoubleClickMs = 400;
    private const string DefaultToolTipText = "NoChat - 局域网聊天";
    private DispatcherTimer? _blinkTimer;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            Logging.AppLogger.Init();
        }
        catch { /* 已由 Program 初始化 */ }

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            AppLogger.Error("UI 线程未处理异常", e.Exception);
            e.Handled = true; // 避免未捕获异常导致进程直接退出
        };

        var data = AppSettings.Load();
        RequestedThemeVariant = data.ThemeMode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.Closed += (_, _) =>
            {
                _trayIcon?.Dispose();
                desktop.Shutdown();
            };

            desktop.MainWindow = mainWindow;
            CreateTrayIcon(mainWindow);
            SingleInstanceGuard.StartNotifyListener(() =>
            {
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.NotifyWindowShown();
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
        try
        {
            using (var s0 = IconHelper.CreateAppIconStream(false))
                _trayIconNormal = new WindowIcon(s0);
            using (var s1 = IconHelper.CreateAppIconStream(true))
                _trayIconAlert = new WindowIcon(s1);
            _trayIcon = new TrayIcon
            {
                Icon = _trayIconNormal,
                ToolTipText = DefaultToolTipText
            };

            _trayIcon.Clicked += (_, _) =>
            {
                var now = DateTime.UtcNow;
                if ((now - _lastTrayClickTime).TotalMilliseconds <= DoubleClickMs)
                {
                    _lastTrayClickTime = DateTime.MinValue;
                    mainWindow.ShowAndSelectUnreadChat();
                }
                else
                    _lastTrayClickTime = now;
            };

            var menu = new NativeMenu();
            var showItem = new NativeMenuItem("显示主窗口");
            showItem.Click += (_, _) =>
            {
                mainWindow.Show();
                mainWindow.Activate();
                mainWindow.NotifyWindowShown();
            };
            menu.Add(showItem);
            menu.Add(new NativeMenuItemSeparator());
            var exitItem = new NativeMenuItem("退出");
            exitItem.Click += (_, _) => mainWindow.RequestExit();
            menu.Add(exitItem);
            _trayIcon.Menu = menu;
            _trayIcon.IsVisible = true;
        }
        catch
        {
            // 部分环境可能不支持托盘
        }
    }

    /// <summary>显示托盘提示并开始闪烁（新消息未读时由 MainWindow 调用）</summary>
    public void ShowTrayNotification(string title, string message)
    {
        if (_trayIcon == null) return;
        _trayIcon.ToolTipText = string.IsNullOrEmpty(message) ? title : $"{title}: {message}";
    }

    /// <summary>开始托盘图标闪烁（固定位置切换正常/高亮图标，不隐藏）</summary>
    public void StartTrayBlink()
    {
        if (_trayIcon == null || _trayIconNormal == null || _trayIconAlert == null) return;
        StopTrayBlink();
        var useAlert = false;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) =>
        {
            if (_trayIcon == null || _trayIconNormal == null || _trayIconAlert == null) return;
            useAlert = !useAlert;
            _trayIcon.Icon = useAlert ? _trayIconAlert : _trayIconNormal;
        };
        _blinkTimer.Start();
    }

    /// <summary>停止闪烁并恢复托盘状态（会话已读时由 MainWindow 调用）</summary>
    public void StopTrayBlink()
    {
        _blinkTimer?.Stop();
        _blinkTimer = null;
        if (_trayIcon != null && _trayIconNormal != null)
        {
            _trayIcon.Icon = _trayIconNormal;
            _trayIcon.ToolTipText = DefaultToolTipText;
        }
    }
}
