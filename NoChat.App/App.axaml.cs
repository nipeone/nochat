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
    private DateTime _lastTrayClickTime = DateTime.MinValue;
    private const int DoubleClickMs = 400;

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
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(MainWindow mainWindow)
    {
        try
        {
            using var stream = IconHelper.CreateAppIconStream();
            _trayIcon = new TrayIcon
            {
                Icon = new WindowIcon(stream),
                ToolTipText = "NoChat - 局域网聊天"
            };

            _trayIcon.Clicked += (_, _) =>
            {
                var now = DateTime.UtcNow;
                if ((now - _lastTrayClickTime).TotalMilliseconds <= DoubleClickMs)
                {
                    _lastTrayClickTime = DateTime.MinValue;
                    mainWindow.Show();
                    mainWindow.Activate();
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
}
